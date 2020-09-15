using NBitcoin;
using NBitcoin.BuilderExtensions;
using NBitcoin.Payment;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	class StartDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("start", "Start the DLC by providing the signatures to the funding PSBT." +
				" If you are the offerer, this command returns a sign message you need to give to the acceptor" +
				" If you are the acceptor, this command returns a fully signed funding transaction");
			command.AddOption(new Option<bool>("--json", "Output in json")
			{
				IsRequired = false,
			});
			command.AddOption(new Option<bool>("--psbt", "Output the fully signed funding transaction as PSBT")
			{
				IsRequired = false,
			});
			command.AddArgument(new Argument<string>("name", "The name of the DLC"));
			command.AddArgument(new Argument<string>("signedpsbt", "The partially signed funding"));
			command.Handler = new StartDLCCommand();
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
			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			var psbt = context.ParsePSBT("signedpsbt", Network);
			
			if (!builder.State.IsInitiator)
			{
				if (dlc.GetNextStep(Network) != Repository.DLCState.DLCNextStep.AcceptorNeedStart)
					throw new CommandException("name", "The DLC is not in the required state to start");
				var fullySigned = builder.Finalize(psbt);
				dlc.BuilderState = builder.ExportStateJObject();
				await Repository.SaveDLC(dlc);
				context.WriteTransaction(fullySigned, Network);
			}
			else
			{
				if (dlc.GetNextStep(Network) != Repository.DLCState.DLCNextStep.OffererNeedStart ||
				dlc.FundKeyPath is null)
					throw new CommandException("name", "The DLC is not in the required state to start");
				var key = await Repository.GetKey(dlc.FundKeyPath);
				try
				{
					var sign = builder.Sign2(key, psbt);
					dlc.Sign = JObject.FromObject(sign, JsonSerializer.Create(Repository.JsonSettings));
					dlc.BuilderState = builder.ExportStateJObject();
					await Repository.SaveDLC(dlc);
					context.WriteObject(sign, Repository.JsonSettings);
				}
				catch (Exception ex)
				{
					throw new CommandException("signedpsbt", $"Invalid PSBT. ({ex.Message})");
				}
			}
		}
	}
}
