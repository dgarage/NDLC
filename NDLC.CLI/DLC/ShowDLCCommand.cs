using NBitcoin.DataEncoders;
using NDLC.CLI.Events;
using NDLC.Messages;
using Newtonsoft.Json;
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
			command.Add(new Option<bool>("--offer", "Show the offer message of the DLC")
			{
				IsRequired = false
			});
			command.Add(new Option<bool>("--accept", "Show the accept message of the DLC")
			{
				IsRequired = false
			});
			command.Add(new Option<bool>("--json", "Output objects in json instead of Base64"));
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

			var shown = ParseShownItem(context);
			if (shown == ShowOption.DLC)
			{
				context.Console.Out.WriteLine($"Name: {dlc.Name}");
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
				if (builder.State.IsInitiator)
				{
					if (dlc.FundKeyPath is null)
					{
						nextStep = State.OfferFund;
					}
					else
					{
						nextStep = State.SignAccept;
					}
				}
				else
				{
					if (dlc.Sign is null)
					{
						nextStep = State.Finalize;
					}
				}
				context.Console.Out.WriteLine($"Next step: {nextStep}");
				context.Console.Out.WriteLine($"Next step explanation:");
				context.Console.Out.Write($"{Explain(nextStep, dlc.Name, builder.State)}");
			}
			else if (shown == ShowOption.Offer)
			{
				if (dlc.Offer is null)
					throw new CommandException("offer", "No offer available for this DLC");
				context.WriteObject(dlc.Offer, Repository.JsonSettings);
			}
			else if (shown == ShowOption.Accept)
			{
				if (dlc.Accept is null)
					throw new CommandException("offer", "No accept message available for this DLC");
				context.WriteObject(dlc.Accept, Repository.JsonSettings);
			}
			else
				throw new NotSupportedException();
		}

		private ShowOption ParseShownItem(InvocationContext context)
		{
			if (context.ParseResult.CommandResult.ValueForOption<bool>("offer"))
				return ShowOption.Offer;
			if (context.ParseResult.CommandResult.ValueForOption<bool>("accept"))
				return ShowOption.Accept;
			return ShowOption.DLC;
		}

		private string Explain(State nextStep, string name, DLCTransactionBuilderState s)
		{
			switch (nextStep)
			{
				case State.OfferFund:
					return $"You need to create a PSBT with your wallet sending {s.Offerer!.Collateral!.ToString(false, false)} BTC to yourself, it must not be broadcasted.{Environment.NewLine}"
						 + $"The address receiving this amount will be the same address where the reward of the DLC will be received.{Environment.NewLine}"
						 + $"Then your can use 'dlc offer {name} \"<PSBT>\"', and give this offer to the other party.";
				case State.SignAccept:
					return $"You need to pass the offer to the acceptor, and the acceptor needs to reply with an accept message.{Environment.NewLine}"
						 + $"Then you need to use `dlc sign {name} \"<accept message>\"`.{Environment.NewLine}"
						 + $"You can get the offer of this dlc with `dlc show --offer {name}`";
				case State.Finalize:
					return $"You need to pass the accept message to the offerer, and the offerer needs to reply with a sign message.{Environment.NewLine}"
						 + $"Then you need to use `dlc finalize {name} \"<sign message>\"`.{Environment.NewLine}"
						 + $"You can get the accept message of this dlc with `dlc show --accept {name}`";
				default:
					throw new NotSupportedException();
			}
		}

		enum ShowOption
		{
			DLC,
			Offer,
			Accept
		}
		enum State
		{
			Unknown,
			OfferFund,
			SignAccept,
			Finalize
		}
	}
}
