using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI
{
	public static class Helpers
	{
		public static string ToString(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteXToSpan(buf);
			return Encoders.Hex.EncodeData(buf);
		}
		public static string ToBase58(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteXToSpan(buf);
			return Encoders.Base58.EncodeData(buf);
		}
	}
}
