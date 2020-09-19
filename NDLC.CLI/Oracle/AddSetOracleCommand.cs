using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository;

namespace NDLC.CLI
{
	public class AddSetOracleCommand : CommandBase
	{
		public static Command CreateCommand(bool set)
		{
			if (set)
			{
				Command command = new Command("set", "Modify an oracle")
				{
					new Argument<string>("name", "The oracle name"),
					new Argument<string>("pubkey", "The oracle pubkey"),
				};
				command.Handler = new AddSetOracleCommand() { Set = true };
				return command;
			}
			else
			{
				Command command = new Command("add", "Add a new oracle")
				{
					new Argument<string>("name", "The oracle name"),
					new Argument<string>("pubkey", "The oracle pubkey"),
				};
				command.Handler = new AddSetOracleCommand();
				return command;
			}
		}
		public bool Set { get; set; }
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			var pubkey = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("pubkey")?.ToLowerInvariant()?.Trim();
			if (pubkey is null)
				throw new CommandOptionRequiredException("pubkey");
			if (!OracleId.TryParse(pubkey, out var oracleId))
				throw new CommandException("pubkey", "Invalid pubkey");

			if (Set)
			{
				var oracle = await GetOracle("name", oracleName);
				await Repository.RemoveOracle(oracle.PubKey);
				await Repository.AddOracle(oracleId.PubKey);
			}
			else
			{
				var oracle = await TryGetOracle(oracleName);
				if (oracle is Oracle)
					throw new CommandException("name", "This oracle already exists");
				await Repository.AddOracle(oracleId.PubKey);
			}
			await NameRepository.SetMapping(Scopes.Oracles, oracleName, oracleId.ToString());
		}
	}
}
