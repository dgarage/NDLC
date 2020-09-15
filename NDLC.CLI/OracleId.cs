using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.CLI
{
	public class OracleId
	{
		public ECXOnlyPubKey PubKey { get; }
		public OracleId(ECXOnlyPubKey pubkey)
		{
			this.PubKey = pubkey;
		}
		public static bool TryParse(string str, [MaybeNullWhenAttribute(false)] out OracleId id)
		{
			id = null;
			if (!HexEncoder.IsWellFormed(str))
				return false;
			var bytes = Encoders.Hex.DecodeData(str);
			if (!ECXOnlyPubKey.TryCreate(bytes, Context.Instance, out var k) || k is null)
				return false;
			id = new OracleId(k);
			return true;
		}
		public override string ToString()
		{
			return Encoders.Hex.EncodeData(PubKey.ToBytes());
		}
	}
}
