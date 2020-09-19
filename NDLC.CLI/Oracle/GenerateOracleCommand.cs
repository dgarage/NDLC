using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository;

namespace NDLC.CLI
{
	public class GenerateOracleCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("generate", "Generate a new oracle (the private key will be stored locally)")
				{
					new Argument<string>("name", "The oracle name")
				};
			command.Handler = new GenerateOracleCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.ToLowerInvariant().Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			if (await TryGetOracle(oracleName) is Oracle)
				throw new CommandException("name", "This oracle already exists");
			var key = await Repository.CreatePrivateKey();
			var pubkey = key.PrivateKey.PubKey.ToECPubKey().ToXOnlyPubKey(out _);
			await NameRepository.SetMapping(Scopes.Oracles, oracleName, Encoders.Hex.EncodeData(pubkey.ToBytes()));
			await Repository.AddOracle(pubkey, key.KeyPath);
			context.Console.Out.Write(Helpers.ToString(pubkey));
		}
	}
}
