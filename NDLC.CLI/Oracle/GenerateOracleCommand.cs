using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class GenerateOracleCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.ToLowerInvariant().Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			if (await Repository.OracleExists(oracleName))
				throw new CommandException("name", "This oracle already exists");
			var key = await Repository.CreatePrivateKey();
			var pubkey = key.PrivateKey.PubKey.ToECPubKey().ToXOnlyPubKey(out _);
			await Repository.SetOracle(oracleName, pubkey, key.KeyPath);
			context.Console.Out.Write(Helpers.ToString(pubkey));
		}
	}
}
