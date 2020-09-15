using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	class ExtractDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("extract", "Extract attestation from a CET." +
				" If successfull, this command returns the attestation of outcome of the event's DLC.");
			command.AddArgument(new Argument<string>("name", "The name of the DLC"));
			command.AddArgument(new Argument<string>("transaction", "A fully signed CET"));
			command.Handler = new ExtractDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");
			var dlc = await Repository.GetDLC(name);
			if (dlc?.OracleInfo is null || dlc?.BuilderState is null)
				throw new CommandException("name", "This DLC does not exist");
			var str = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("transaction")?.Trim();
			if (str is null)
				throw new CommandOptionRequiredException("transaction");
			if (!Transaction.TryParse(str, Network, out var tx) || tx is null)
				throw new CommandException("transaction", "Cannot parse the transaction");
			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			try
			{
				var key = builder.ExtractAttestation(tx);
				await Repository.AddAttestation(dlc.OracleInfo, key);
				context.Console.Out.Write(key.ToHex());
			}
			catch (Exception ex)
			{
				throw new CommandException("transaction", $"Impossible to extract the attestation from this transaction. {ex.Message}");
			}
		}
	}
}
