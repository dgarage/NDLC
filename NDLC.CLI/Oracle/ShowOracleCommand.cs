using NBitcoin;
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
	public class ShowOracleCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.ToLowerInvariant().Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			var o = await Repository.GetOracle(oracleName);
			if (o is null)
				throw new CommandException("name", "This oracle does not exists");
			context.Console.Out.WriteLine($"Name: {o.Name}");
			if (o.PubKey is ECXOnlyPubKey)
				context.Console.Out.WriteLine($"Pubkey: {Helpers.ToString(o.PubKey)}");
			if (o.RootedKeyPath is RootedKeyPath)
				context.Console.Out.WriteLine($"Keypath: {o.RootedKeyPath}");
			context.Console.Out.WriteLine($"Has private key: {o.RootedKeyPath is RootedKeyPath || o.ExternalKey is Key}");
			var showSensitive = context.ParseResult.ValueForOption<bool>("show-sensitive");
			if (showSensitive && await Repository.GetKey(o) is Key key)
			{
				if (key.PubKey.ToECPubKey().ToXOnlyPubKey(out _) != o.PubKey)
					throw new InvalidOperationException("The private key does not match the pubkey of the oracle");
				context.Console.Out.WriteLine($"Key: {key.ToHex()}");
			}
		}
	}
}
