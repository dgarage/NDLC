using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	public class ExecuteDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("execute", "Execute a DLC with the Oracle's attestation." +
				" If successful, this command returns a fully signed Contract Execution Transaction, that you can broadcast.");
			command.AddOption(new Option<bool>("--json", "Output in json")
			{
				IsRequired = false,
			});
			command.AddOption(new Option<bool>("--psbt", "Output the CET as a PSBT")
			{
				IsRequired = false,
			});
			command.Add(new Argument<string>("name", "The name of the DLC"));
			command.Add(new Argument<string>("attestation", "The oracle's attestation")
			{
				Arity =	ArgumentArity.ZeroOrOne
			});
			command.Handler = new ExecuteDLCCommand();
			return command;
		}

		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");
			var dlc = await GetDLC("name", name);
			if (dlc?.OracleInfo is null)
				throw new CommandException("name", "This DLC does not exist");
			if (dlc.BuilderState is null || dlc.FundKeyPath is null)
				throw new CommandException("name", "This DLC is not in the right state to get executed");

			var evt = await Repository.GetEvent(dlc.OracleInfo.PubKey, dlc.OracleInfo.RValue);

			var attestation = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("attestation")?.Trim();
			Key oracleKey;
			try
			{
				if (attestation is null)
				{
					var k = evt?.Attestations?.Select(o => o.Value).FirstOrDefault();
					if (k is null)
						throw new CommandOptionRequiredException("attestation");
					oracleKey = k;
				}
				else
				{
					oracleKey = new Key(Encoders.Hex.DecodeData(attestation));
				}
			}
			catch (CommandOptionRequiredException)
			{
				throw;
			}
			catch
			{
				throw new CommandException("attestation", "Cannot parse the attestation");
			}

			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			var key = await Repository.GetKey(dlc.FundKeyPath);
			try
			{
				var execution = builder.Execute(key, oracleKey);
				if (evt is Repository.Event)
				{
					await Repository.AddAttestation(dlc.OracleInfo, oracleKey);
				}
				context.WriteTransaction(execution.CET, builder.State.Funding?.FundCoin, Network);
			}
			catch (Exception ex)
			{
				throw new CommandException("attestation", $"Error while building the CET. ({ex.Message})");
			}
		}
	}
}
