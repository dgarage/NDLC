using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Secp256k1
{
	public class SchnorrNonce
	{
		public readonly FE fe;
		public static bool TryParse(string str, out SchnorrNonce? schnorrNonce)
		{
			schnorrNonce = null;
			var bytes = Encoders.Hex.DecodeData(str);
			if (bytes.Length != 32)
				return false;
			return TryCreate(bytes, out schnorrNonce);
		}
		public static bool TryCreate(ReadOnlySpan<byte> in32, out SchnorrNonce? schnorrNonce)
		{
			schnorrNonce = null;
			if (in32.Length != 32)
				return false;
			schnorrNonce = null;
			if (!FE.TryCreate(in32, out var fe))
				return false;
			schnorrNonce = new SchnorrNonce(fe);
			return true;
		}
		public SchnorrNonce(FE fe)
		{
			this.fe = fe;
		}

		public void WriteToSpan(Span<byte> out32)
		{
			this.fe.WriteToSpan(out32);
		}
		public byte[] ToBytes()
		{
			var buf = new byte[32];
			this.fe.WriteToSpan(buf);
			return buf;
		}

		public override string ToString()
		{
			Span<byte> b = stackalloc byte[32];
			fe.WriteToSpan(b);
			return Encoders.Hex.EncodeData(b);
		}
	}
}
