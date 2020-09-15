using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace NDLC.Secp256k1
{
	public class SchnorrNonce
	{
		public ECXOnlyPubKey PubKey { get; }

		public static bool TryParse(string str, [MaybeNullWhen(false)] out SchnorrNonce schnorrNonce)
		{
			return TryParse(str, Context.Instance, out schnorrNonce);
		}
		public static bool TryParse(string str, Context context, [MaybeNullWhen(false)] out SchnorrNonce schnorrNonce)
		{
			schnorrNonce = null;
			var bytes = Encoders.Hex.DecodeData(str);
			if (bytes.Length != 32)
				return false;
			return TryCreate(bytes, context, out schnorrNonce);
		}
		public static bool TryCreate(ReadOnlySpan<byte> in32, out SchnorrNonce? schnorrNonce)
		{
			return TryCreate(in32, Context.Instance, out schnorrNonce);
		}
		public static bool TryCreate(ReadOnlySpan<byte> in32, Context context, out SchnorrNonce? schnorrNonce)
		{
			schnorrNonce = null;
			if (in32.Length != 32)
				return false;
			schnorrNonce = null;
			if (!context.TryCreateXOnlyPubKey(in32, out var k) || k is null)
				return false;
			schnorrNonce = new SchnorrNonce(k);
			return true;
		}
		public SchnorrNonce(ECXOnlyPubKey pubkey)
		{
			if (pubkey == null)
				throw new ArgumentNullException(nameof(pubkey));
			PubKey = pubkey;
		}

		public void WriteToSpan(Span<byte> out32)
		{
			this.PubKey.WriteToSpan(out32);
		}
		public byte[] ToBytes()
		{
			var buf = new byte[32];
			this.PubKey.WriteToSpan(buf);
			return buf;
		}


		public override bool Equals(object obj)
		{
			SchnorrNonce? item = obj as SchnorrNonce;
			if (item is null)
				return false;
			return PubKey.Equals(item.PubKey);
		}
		public static bool operator ==(SchnorrNonce? a, SchnorrNonce? b)
		{
			if (System.Object.ReferenceEquals(a, b))
				return true;
			if ((a is null) || (b is null))
				return false;
			return a.PubKey == b.PubKey;
		}

		public static bool operator !=(SchnorrNonce a, SchnorrNonce b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return PubKey.GetHashCode();
		}

		public override string ToString()
		{
			Span<byte> b = stackalloc byte[32];
			PubKey.WriteToSpan(b);
			return Encoders.Hex.EncodeData(b);
		}

		public SecpSchnorrSignature CreateSchnorrSignature(ECPrivKey key)
		{
			if (!SecpSchnorrSignature.TryCreate(PubKey.Q.x, key.sec, out var sig) || sig is null)
				throw new InvalidOperationException("Invalid schnorr signature");
			return sig;
		}
	}
}
