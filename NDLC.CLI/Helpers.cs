using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;

namespace NDLC.CLI
{
	public static class Helpers
	{
		public static PSBT ParsePSBT(this InvocationContext ctx, Network network)
		{
			var psbtStr = ctx.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("psbt")?.Trim();
			if (psbtStr is null)
				throw new CommandOptionRequiredException("psbt");
			if (!PSBT.TryParse(psbtStr, network, out var psbt) || psbt is null)
				throw new CommandException("psbt", "Invalid PSBT");
			return psbt;
		}
		public static string ToString(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteToSpan(buf);
			return Encoders.Hex.EncodeData(buf);
		}
		public static string ToBase58(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteToSpan(buf);
			return Encoders.Base58.EncodeData(buf);
		}
	}
}
