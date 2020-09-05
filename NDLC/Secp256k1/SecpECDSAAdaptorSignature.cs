using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace NDLC.Secp256k1
{
	public class SecpECDSAAdaptorSignature
	{
		public const int Size = 65;
		public readonly GE r;
		public readonly Scalar sp;
		internal SecpECDSAAdaptorSignature(in GE r, in Scalar sp)
		{
			this.r = r;
			this.sp = sp;
		}

		public Scalar GetRAsScalar()
		{
			Span<byte> buf = stackalloc byte[65];
			WriteToSpan(buf);
			return new Scalar(buf.Slice(1), out _);
		}

		public static bool TryCreate(ReadOnlySpan<byte> input65, out SecpECDSAAdaptorSignature? sig)
		{
			if (!Internals.secp256k1_dleq_deserialize_point(input65, out var r))
			{
				sig = default;
				return false;
			}
			var sp = new Scalar(input65.Slice(33), out var overflow);
			if (overflow != 0)
			{
				sig = default;
				return false;
			}
			var sigr = new Scalar(input65.Slice(1), out overflow);
			if (overflow != 0)
			{
				sig = default;
				return false;
			}
			sig = new SecpECDSAAdaptorSignature(r, sp);
			return true;
		}

		public SecpECDSASignature AdaptECDSA(ECPrivKey key)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));
			var s = key.sec.Inverse();
			s = s * sp;
			var high = s.IsHigh;
			s.CondNegate(high ? 1 : 0, out s);
			var sig = new SecpECDSASignature(this.r.x.ToScalar(), s, true);
			s = default;
			return sig;
		}

		public void WriteToSpan(Span<byte> output65)
		{
			Internals.secp256k1_dleq_serialize_point(output65, r);
			sp.WriteToSpan(output65.Slice(33));
		}

		public bool TryExtractSecret(SecpECDSASignature sig, ECPubKey adaptor, out ECPrivKey? secret)
		{
			secret = null;
			if (this.GetRAsScalar() != sig.r)
				return false;
			var adaptor_secret = sig.s.Inverse();
			adaptor_secret = adaptor_secret * sp;

			/* Deal with ECDSA malleability */
			var adaptor_expected_gej = adaptor.ctx.EcMultGenContext.MultGen(adaptor_secret);
			var adaptor_expected_ge = adaptor_expected_gej.ToGroupElement();
			var adaptor_expected = new ECPubKey(adaptor_expected_ge, adaptor.ctx);
			if (adaptor != adaptor_expected)
			{
				adaptor_secret = adaptor_secret.Negate();
			}
			secret = adaptor.ctx.CreateECPrivKey(adaptor_secret);
			return true;
		}
	}
}
