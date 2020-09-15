using NBitcoin;
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

namespace NDLC.CLI
{
	public class ShowOracleCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("show", "Show an oracle")
				{
					new Argument<string>("name", "The oracle name")
				};
			command.Add(new Option<bool>("--show-sensitive", "Show sensitive informations (like private keys)"));
			command.Handler = new ShowOracleCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.ToLowerInvariant().Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			var o = await GetOracle("name", oracleName);
			context.Console.Out.WriteLine($"Name: {oracleName}");
			if (o.PubKey is ECXOnlyPubKey)
				context.Console.Out.WriteLine($"Pubkey: {Helpers.ToString(o.PubKey)}");
			if (o.RootedKeyPath is RootedKeyPath)
				context.Console.Out.WriteLine($"Keypath: {o.RootedKeyPath}");
			context.Console.Out.WriteLine($"Has private key: {o.RootedKeyPath is RootedKeyPath}");
			var showSensitive = context.ParseResult.ValueForOption<bool>("show-sensitive");
			if (showSensitive && 
				o.RootedKeyPath is RootedKeyPath &&
				await Repository.GetKey(o.RootedKeyPath) is Key key)
			{
				if (key.PubKey.ToECPubKey().ToXOnlyPubKey(out _) != o.PubKey)
					throw new InvalidOperationException("The private key does not match the pubkey of the oracle");
				context.Console.Out.WriteLine($"Key: {key.ToHex()}");
			}
		}
	}
}
