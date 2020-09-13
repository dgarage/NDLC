using NDLC.CLI.Events;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	public class ShowDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("show", "Show information about a DLC");
			command.Add(new Argument<string>("name", "The name of the DLC")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Handler = new ShowDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");

			var dlc = await Repository.GetDLC(name);
			if (dlc?.BuilderState is null ||
				dlc?.OracleInfo is null)
				throw new CommandException("name", "This DLC does not exist");

			var oracle = await Repository.GetOracle(dlc.OracleInfo.PubKey);
			var oracleName = oracle?.Name;
			string? eventName = null;
			if (oracle != null)
			{
				var ev = await Repository.GetEvent(dlc.OracleInfo.PubKey, dlc.OracleInfo.RValue);
				eventName = ev?.Name;
			}

			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			var role = builder.State.IsInitiator ? "Offerer" : "Acceptor";
			if (oracleName is string && eventName is string)
			{
				context.Console.Out.WriteLine($"Event: {new EventFullName(oracleName, eventName)}");
			}
			else
			{
				context.Console.Out.WriteLine($"Event: ?");
			}
			context.Console.Out.WriteLine($"Role: {role}");
			State nextStep = State.Unknown;
			if (builder.State.IsInitiator && dlc.FundKeyPath is null)
			{
				nextStep = State.OfferFund;
			}
			context.Console.Out.WriteLine($"Next step: {nextStep}");
			context.Console.Out.WriteLine($"Next step explanation:");
			context.Console.Out.Write($"{Explain(nextStep, name, builder.State)}");
		}

		private string Explain(State nextStep, string name, DLCTransactionBuilderState s)
		{
			switch (nextStep)
			{
				case State.OfferFund:
					return $"You need to create a PSBT with your wallet sending {s.Offerer!.Collateral!.ToString(false, false)} BTC to yourself must not be broadcasted.{Environment.NewLine}"
						 + $"The address receiving this amount will be the same address where the reward of the DLC will be received.{Environment.NewLine}"
						 + $"Then your can use 'dlc offer {name} \"<PSBT>\"', and give this offer to the other party.";
				default:
					throw new NotSupportedException();
			}
		}

		enum State
		{
			Unknown,
			OfferFund
		}
	}
}
