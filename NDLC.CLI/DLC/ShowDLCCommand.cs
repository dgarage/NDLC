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
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository.DLCState;
using NBitcoin;

namespace NDLC.CLI.DLC
{
	public class ShowDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("show", "Show information about a DLC");

			command.AddOption(new Option<bool>("--json", "Output in json")
			{
				IsRequired = false,
			});
			command.AddOption(new Option<bool>("--psbt", "Output transactions as PSBT")
			{
				IsRequired = false,
			});
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
			command.Add(new Option<bool>("--funding", "Show the funding PSBT of the DLC")
			{
				IsRequired = false
			});
			command.Add(new Option<bool>("--refund", "Show the refund transaction of the DLC.")
			{
				IsRequired = false
			});
			command.Add(new Option<bool>("--abort", "Show the abort PSBT of the DLC")
			{
				IsRequired = false
			});
			command.Handler = new ShowDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");

			var dlc = await GetDLC("name", name);
			if (dlc?.BuilderState is null ||
				dlc?.OracleInfo is null)
				throw new CommandException("name", "This DLC does not exist");

			var oracle = await Repository.GetOracle(dlc.OracleInfo.PubKey);
			string? oracleName = null;

			EventFullName eventName = new EventFullName("???", "???");
			if (oracle != null)
			{
				oracleName = await NameRepository.GetName(Scopes.Oracles, new OracleId(dlc.OracleInfo.PubKey).ToString());
				var ev = await Repository.GetEvent(dlc.OracleInfo);
				eventName = await NameRepository.AsEventRepository().ResolveName(dlc.OracleInfo) ?? eventName;
			}
			var shown = ParseShownItem(context);
			if (shown == ShowOption.DLC)
			{
				context.Console.Out.WriteLine($"Name: {name}");
				var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
				var role = builder.State.IsInitiator ? "Offerer" : "Acceptor";
				if (builder.State.ContractId is uint256)
				{
					var temp = builder.State.TemporaryContractId ? "Temporary " : "";
					context.Console.Out.WriteLine($"{temp}Contract Id: {builder.State.ContractId}");
				}
				context.Console.Out.WriteLine($"Event: {eventName}");
				context.Console.Out.WriteLine($"Role: {role}");
				var nextStep = dlc.GetNextStep(Network);
				context.Console.Out.WriteLine($"Next step: {nextStep}");
				context.Console.Out.WriteLine($"Next step explanation:");
				context.Console.Out.Write($"{Explain(nextStep, name, builder.State)}");
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
					throw new CommandException("accept", "No accept message available for this DLC");
				context.WriteObject(dlc.Accept, Repository.JsonSettings);
			}
			else if (shown == ShowOption.Sign)
			{
				if (dlc.Sign is null)
					throw new CommandException("sign", "No sign message available for this DLC");
				context.WriteObject(dlc.Sign, Repository.JsonSettings);
			}
			else if (shown == ShowOption.Funding)
			{
				try
				{
					var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
					context.WritePSBT(builder.GetFundingPSBT());
				}
				catch
				{
					throw new CommandException("funding", "No funding PSBT ready for this DLC");
				}
			}
			else if (shown == ShowOption.Abort)
			{
				if (dlc.Abort is null)
					throw new CommandException("abort", "No abort PSBT for this DLC");
				context.WritePSBT(dlc.Abort);
			}
			else if (shown == ShowOption.Refund)
			{
				try
				{
					var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
					context.WriteTransaction(builder.BuildRefund(), builder.State.Funding?.FundCoin, Network);
				}
				catch
				{
					throw new CommandException("refund", "No refund PSBT ready for this DLC");
				}
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
			if (context.ParseResult.CommandResult.ValueForOption<bool>("sign"))
				return ShowOption.Sign;
			if (context.ParseResult.CommandResult.ValueForOption<bool>("funding"))
				return ShowOption.Funding;
			if (context.ParseResult.CommandResult.ValueForOption<bool>("refund"))
				return ShowOption.Refund;
			if (context.ParseResult.CommandResult.ValueForOption<bool>("abort"))
				return ShowOption.Abort;
			return ShowOption.DLC;
		}

		private string Explain(DLCNextStep nextStep, string name, DLCTransactionBuilderState s)
		{
			switch (nextStep)
			{
				case DLCNextStep.Setup:
					return $"You need to create the setup PSBT with your wallet sending {s.Us!.Collateral!.ToString(false, false)} BTC to yourself, it must not be broadcasted.{Environment.NewLine}"
						 + $"The address receiving this amount will be the same address where the reward of the DLC will be received.{Environment.NewLine}"
						 + $"Then your can use 'dlc setup {name} \"<PSBT>\"', and give this message to the other party.";
				case DLCNextStep.CheckSigs when s.IsInitiator:
					return $"You need to pass the offer to the other party, and the other party will need to accept by sending you back a signed message.{Environment.NewLine}"
						 + $"Then you need to use `dlc checksigs \"<signed message>\"`.{Environment.NewLine}"
						 + $"You can get the offer of this dlc with `dlc show --offer {name}`";
				case DLCNextStep.CheckSigs when !s.IsInitiator:
					return $"You need to pass the accept message to the other party, and the other party needs to reply with a signed message.{Environment.NewLine}"
						 + $"Then you need to use `dlc checksigs \"<signed message>\"`.{Environment.NewLine}"
						 + $"You can get the accept message of this dlc with `dlc show --accept {name}`";
				case DLCNextStep.Fund when s.IsInitiator:
					return $"You need to partially sign a PSBT funding the DLC. You can can get the PSBT with `dlc show --funding {name}`.{Environment.NewLine}" +
						   $"Then you need to use `dlc start {name} \"<PSBT>\"` and send the signed message to the other party.";
				case DLCNextStep.Fund when !s.IsInitiator:
					return $"You need to partially sign a PSBT funding the DLC. You can can get the PSBT with `dlc show --funding {name}`.{Environment.NewLine}" +
						   $"Then you need to use `dlc start {name} \"<PSBT>\"` and broadcast the resulting transaction.";
				case DLCNextStep.Done when s.IsInitiator:
					return $"Make sure the other party actually start the DLC by broadcasting the funding transaction.{Environment.NewLine}" +
					   	   $"IF THE OTHER PARTY DOES NOT RESPOND and doesn't broadcast the funding in reasonable delay. YOU MUST ABORT this DLC by signing and broadcasting the abort transaction `dlc show --abort {name}`.{Environment.NewLine}" +
						   $"The abort transaction spend the coins you used for your collateral back to yourself.{Environment.NewLine}" +
						   $"This will prevent a malicious party to start the contract without your involvement when he knows the outcome.{Environment.NewLine}{Environment.NewLine}" +
						   $"When the Oracle attests the event, you can settle this contract by running `dlc execute \"<attestation>\"` and broadcasting the transaction.{Environment.NewLine}{Environment.NewLine}" +
						   $"If the Oracle never attests the event you can get a refund later by broadcasting `dlc show --refund \"{name}\"`.{Environment.NewLine}{Environment.NewLine}";
				case DLCNextStep.Done when !s.IsInitiator:
					return $"You need to fully sign and broadcast the funding transaction. You can get the PSBT with `dlc show --funding`.{Environment.NewLine}" +
						   $"When the Oracle attests the event, you can settle this contract by running `dlc execute \"<attestation>\"` and broadcasting the transaction.{Environment.NewLine}{Environment.NewLine}" +
						   $"If the Oracle never attests the event you can get a refund later by broadcasting `dlc show --refund \"{name}\"`."; ;
				default:
					throw new NotSupportedException();
			}
		}

		enum ShowOption
		{
			DLC,
			Offer,
			Accept,
			Sign,
			Abort,
			Funding,
			Refund
		}
	}
}
