using NBitcoin;
using NBitcoin.BuilderExtensions;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	class CounterSignDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("countersign", "Sign and fund the DLC the other party accepted.");
			command.AddArgument(new Argument<string>("name", "The name of the DLC"));
			command.AddArgument(new Argument<string>("psbt", "The signed funding PSBT"));
			command.Handler = new CounterSignDLCCommand();
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
			if (!builder.State.IsInitiator)
				throw new CommandException("name", "This command can only be used by the initiator of the DLC");
			if (dlc.GetNextStep(Network) != Repository.DLCState.DLCNextStep.OffererSignFunding ||
				dlc.FundKeyPath is null)
				throw new CommandException("name", "The DLC is not in the required state to countersign");

			var psbt = context.ParsePSBT(Network);

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
				throw new CommandException("psbt", $"Invalid PSBT. ({ex.Message})");
			}
		}
	}
}
