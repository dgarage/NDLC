using System.Diagnostics.CodeAnalysis;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;

namespace NDLC.Infrastructure
{
	public class OracleId
	{
		public ECXOnlyPubKey PubKey { get; }
		public OracleId(ECXOnlyPubKey pubkey)
		{
			this.PubKey = pubkey;
		}

		public static implicit operator OracleId(ECXOnlyPubKey pubkey)
		{
			return new OracleId(pubkey);
		}

		public static bool TryParse(string str, [MaybeNullWhen(false)] out OracleId id)
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
