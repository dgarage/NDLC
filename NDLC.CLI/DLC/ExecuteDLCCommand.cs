using NBitcoin;
using NBitcoin.DataEncoders;
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
	public class ExecuteDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("execute", "Execute a DLC with the Oracle's attestation");
			command.Add(new Argument<string>("name", "The name of the DLC"));
			command.Add(new Argument<string>("attestation", "The oracle's attestation"));
			command.Handler = new ExecuteDLCCommand();
			return command;
		}

		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");
			var dlc = await Repository.GetDLC(name);
			if (dlc is null)
				throw new CommandException("name", "This DLC does not exist");
			if (dlc.BuilderState is null || dlc.FundKeyPath is null)
				throw new CommandException("name", "This DLC is not in the right state to get executed");
			var attestation = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("attestation")?.Trim();
			if (attestation is null)
				throw new CommandOptionRequiredException("attestation");
			Key oracleKey;
			try
			{
				oracleKey = new Key(Encoders.Hex.DecodeData(attestation));
			}
			catch
			{
				throw new CommandException("attestation", "Cannot parse the attestation");
			}

			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			var key = await Repository.GetKey(dlc.FundKeyPath);
			try
			{
				context.Console.Out.Write(builder.BuildSignedCET(key, oracleKey).ToHex());
			}
			catch (Exception ex)
			{
				throw new CommandException("attestation", $"Error while building the CET. ({ex.Message})");
			}
		}
	}
}
