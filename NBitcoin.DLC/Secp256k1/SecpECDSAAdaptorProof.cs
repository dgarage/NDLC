using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.DLC.Secp256k1
{
	public class SecpECDSAAdaptorProof
	{
		public const int Size = 97;
		public readonly Scalar s, e;
		public readonly GE rp;
		public SecpECDSAAdaptorProof(in GE rp, in Scalar s, in Scalar e)
		{
			this.rp = rp;
			this.s = s;
			this.e = e;
		}

		public static bool TryCreate(ReadOnlySpan<byte> input97, out SecpECDSAAdaptorProof? proof)
		{
			if (!Internals.secp256k1_dleq_deserialize_point(input97, out var rp))
			{
				proof = default;
				return false;
			}
			var s = new Scalar(input97.Slice(33), out var overflow);
			if (overflow != 0)
			{
				proof = default;
				return false;
			}
			var e = new Scalar(input97.Slice(33 + 32), out overflow);
			if (overflow != 0)
			{
				proof = default;
				return false;
			}
			proof = new SecpECDSAAdaptorProof(rp, s, e);
			return true;
		}

		public void WriteToSpan(Span<byte> output97)
		{
			Internals.secp256k1_dleq_serialize_point(output97, rp);
			s.WriteToSpan(output97.Slice(33));
			e.WriteToSpan(output97.Slice(33 + 32));
		}
	}
}
