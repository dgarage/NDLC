using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Protocol;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NBitcoin.DLC.Secp256k1
{
	public static class Extensions
	{
		public static ECPubKey ToECPubKey(this PubKey pubkey)
		{
			Context.Instance.TryCreatePubKey(pubkey.ToBytes(), out var r);
			if (r is null)
				throw new InvalidOperationException("should never happen");
			return r;
		}

		internal static byte[] TAG_BIP0340Challenge = ASCIIEncoding.ASCII.GetBytes("BIP340/challenge");
		public static bool TryComputeSigPoint(this ECXOnlyPubKey pubkey, ReadOnlySpan<byte> msg32, SchnorrNonce rx, out ECPubKey? sigpoint)
		{
			if (rx == null)
				throw new ArgumentNullException(nameof(rx));
			if (msg32.Length != 32)
				throw new ArgumentException("Msg should be 32 bytes", nameof(msg32));
			sigpoint = null;
			Span<byte> buf = stackalloc byte[32];
			Span<byte> pk_buf = stackalloc byte[32];
			pubkey.WriteXToSpan(pk_buf);
			/* tagged hash(r.x, pk.x, msg32) */
			using var sha = new SHA256();
			sha.InitializeTagged(TAG_BIP0340Challenge);
			rx.fe.WriteToSpan(buf);
			sha.Write(buf);
			sha.Write(pk_buf);
			sha.Write(msg32);
			sha.GetHash(buf);
			if (!pubkey.TryMultTweak(buf, out var pubkey_ge) || pubkey_ge is null)
				return false;
			if (!GE.TryCreateXQuad(rx.fe, out var rx_ge))
				return false;
			var pubkey_gej = pubkey_ge.Q.ToGroupElementJacobian();
			var sigpoint_gej = pubkey_gej + rx_ge;
			var sigpoint_ge = sigpoint_gej.ToGroupElement();
			sigpoint = new ECPubKey(sigpoint_ge, pubkey.ctx);
			return true;
		}
		public static bool SigVerify(this ECPubKey pubKey, SecpECDSAAdaptorSignature sig, ReadOnlySpan<byte> msg32, ECPubKey adaptor, SecpECDSAAdaptorProof proof)
		{
			if (pubKey == null)
				throw new ArgumentNullException(nameof(pubKey));
			if (adaptor == null)
				throw new ArgumentNullException(nameof(adaptor));
			if (msg32.Length < 32)
				throw new ArgumentException(paramName: nameof(msg32), message: "msg32 should be at least 32 bytes");
			if (sig == null)
				throw new ArgumentNullException(nameof(sig));
			var adaptor_ge = adaptor.Q;
			if (!secp256k1_dleq_verify(pubKey.ctx.EcMultContext, "ECDSAAdaptorSig", proof.s, proof.e, proof.rp, adaptor_ge, sig.r))
			{
				return false;
			}

			/* 2. return x_coord(R') == x_coord(s'⁻¹(H(m) * G + x_coord(R) * X)) */
			var q = pubKey.Q;
			var msg = new Scalar(msg32);
			if (!pubKey.ctx.secp256k1_ecdsa_adaptor_sig_verify_helper(sig.r.x.ToScalar(), sig.sp, q, msg, out var rhs))
			{
				return false;
			}
			var lhs = proof.rp.ToGroupElementJacobian();
			rhs = rhs.Negate();
			lhs = lhs.AddVariable(rhs, out _);
			return lhs.IsInfinity;
		}
		public static bool TrySignAdaptor(this ECPrivKey key, ReadOnlySpan<byte> msg32, ECPubKey adaptor, out SecpECDSAAdaptorSignature? adaptorSignature, out SecpECDSAAdaptorProof? proof)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));
			if (adaptor == null)
				throw new ArgumentNullException(nameof(adaptor));
			if (msg32.Length < 32)
				throw new ArgumentException(paramName: nameof(msg32), message: "msg32 should be at least 32 bytes");
			var adaptor_ge = adaptor.Q;
			var seckey32 = key.sec;
			SHA256 sha = new SHA256();
			sha.Write(msg32.Slice(0, 32));
			Span<byte> buf33 = stackalloc byte[33];
			Internals.secp256k1_dleq_serialize_point(buf33, adaptor_ge);
			sha.Write(buf33);
			sha.GetHash(buf33);
			Span<byte> nonce32 = stackalloc byte[32];
			nonce_function_dleq(buf33, key.sec, "ECDSAAdaptorNon", nonce32);
			var k = new Scalar(nonce32);
			if (k.IsZero)
			{
				adaptorSignature = default;
				proof = default;
				return false;
			}
			var rpj = key.ctx.EcMultGenContext.MultGen(k);
			/* 2. R = k*Y; */
			var rj = adaptor_ge.MultConst(k, 256);
			/* 4. [sic] proof = DLEQ_prove((G,R'),(Y, R)) */
			if (!key.ctx.EcMultGenContext.secp256k1_dleq_proof("ECDSAAdaptorSig", k, adaptor_ge, out var dleq_proof_s, out var dleq_proof_e))
			{
				adaptorSignature = default;
				proof = default;
				return false;
			}

			/* 5. s' = k⁻¹(H(m) + x_coord(R)x) */
			var r = rj.ToGroupElement();
			var msg = new Scalar(msg32);
			if (!secp256k1_ecdsa_adaptor_sign_helper(msg, k, r, key.sec, out var sp))
			{
				k = default;
				adaptorSignature = default;
				proof = default;
				return false;
			}

			/* 6. return (R, R', s', proof) */
			var rp = rpj.ToGroupElement();
			proof = new SecpECDSAAdaptorProof(rp, dleq_proof_s, dleq_proof_e);
			adaptorSignature = new SecpECDSAAdaptorSignature(r, sp);
			k = default;
			return true;
		}


		static bool secp256k1_ecdsa_adaptor_sig_verify_helper(this Context ctx, in Scalar sigr, in Scalar sigs, in GE pubkey, in Scalar message, out GE result)
		{
			if (sigr.IsZero || sigs.IsZero)
			{
				result = default;
				return false;
			}

			var sn = sigs.InverseVariable();
			var u1 = sn * message;
			var u2 = sn * sigr;
			var pubkeyj = pubkey.ToGroupElementJacobian();
			var pr = ctx.EcMultContext.Mult(pubkeyj, u2, u1);
			
			if (pr.IsInfinity)
			{
				result = default;
				return false;
			}
			result = pr.ToGroupElement();
			return true;
		}



		/* 5. s' = k⁻¹(H(m) + x_coord(R)x) */
		static bool secp256k1_ecdsa_adaptor_sign_helper(in Scalar message, in Scalar k, in GE r, in Scalar sk, out Scalar sigs)
		{
			if (!r.x.TryGetScalar(out var sigr))
			{
				sigs = default;
				return false;
			}
			var n = sigr * sk;
			n = n + message;
			sigs = k.Inverse();
			sigs = sigs * n;
			n = default;
			return !sigs.IsZero;
		}

		public static Scalar ToScalar(this in FE fe)
		{
			if (!TryGetScalar(fe, out var r))
				throw new InvalidOperationException("Impossible to get scalar of this signature, this should never happen");
			return r;
		}

		public static bool TryGetScalar(this in FE fe, out Scalar scalar)
		{
			Span<byte> s = stackalloc byte[32];
			fe.Normalize().WriteToSpan(s);
			var sc = new Scalar(s, out int overflow);
			if (overflow != 0)
			{
				scalar = default;
				return false;
			}
			scalar = sc;
			return true;
		}

		/* p1 = x*G, p2 = x*gen2, constant time */
		public static void secp256k1_dleq_pair(this ECMultGenContext ecmult_gen_ctx, in Scalar sk, in GE gen2, out GE p1, out GE p2)
		{
			var p1j = ecmult_gen_ctx.MultGen(sk);
			p1 = p1j.ToGroupElement();
			var p2j = gen2.MultConst(sk, 256);
			p2 = p2j.ToGroupElement();
		}

		public static bool secp256k1_dleq_proof(this ECMultGenContext ecmult_gen_ctx, string algo16, in Scalar sk, in GE gen2, out Scalar s, out Scalar e)
		{
			Span<byte> algobytes = stackalloc byte[16];
			Encoding.ASCII.GetBytes(algo16, algobytes);
			return secp256k1_dleq_proof(ecmult_gen_ctx, algobytes, sk, gen2, out s, out e);
		}

		/* TODO: allow signing a message by including it in the challenge hash */
		public static bool secp256k1_dleq_proof(this ECMultGenContext ecmult_gen_ctx, ReadOnlySpan<byte> algo16, in Scalar sk, in GE gen2, out Scalar s, out Scalar e)
		{
			Span<byte> buf32 = stackalloc byte[32];
			Span<byte> key32 = stackalloc byte[32];
			Span<byte> nonce32 = stackalloc byte[32];
			secp256k1_dleq_pair(ecmult_gen_ctx, sk, gen2, out var p1, out var p2);
			/* Everything that goes into the challenge hash must go into the nonce as well... */
			NBitcoin.Secp256k1.SHA256 sha = new SHA256();
			sha.HashPoint(gen2);
			sha.HashPoint(p1);
			sha.HashPoint(p2);
			sha.GetHash(buf32);
			sk.WriteToSpan(key32);
			nonce_function_dleq(buf32, key32, algo16, nonce32);
			var k = new Scalar(nonce32, out var overflow);
			if (overflow != 0)
			{
				s = e = default;
				return false;
			}
			var r1j = ecmult_gen_ctx.MultGen(k);
			var r1 = r1j.ToGroupElement();
			var r2j = gen2.MultConst(k, 256);
			var r2 = r2j.ToGroupElement();

			secp256k1_dleq_challenge_hash(algo16, gen2, r1, r2, p1, p2, out e);
			s = e * sk;
			s = s + k;
			k = default;
			return true;
		}

		public static bool secp256k1_dleq_verify(this ECMultContext ecmult_ctx, string algo16, in Scalar s, in Scalar e, in GE p1, in GE gen2, in GE p2)
		{
			Span<byte> algobytes = stackalloc byte[16];
			Encoding.ASCII.GetBytes(algo16, algobytes);
			return secp256k1_dleq_verify(ecmult_ctx, algobytes, s, e, p1, gen2, p2);
		}
		public static bool secp256k1_dleq_verify(this ECMultContext ecmult_ctx, ReadOnlySpan<byte> algo16, in Scalar s, in Scalar e, in GE p1, in GE gen2, in GE p2)
		{
			var p1j = p1.ToGroupElementJacobian();
			var p2j = p2.ToGroupElementJacobian();
			var e_neg = e.Negate();
			/* R1 = s*G  - e*P1 */
			var r1j = ecmult_ctx.Mult(p1j, e_neg, s);
			/* R2 = s*gen2 - e*P2 */
			var tmpj = ecmult_ctx.Mult(p2j, e_neg, Scalar.Zero);
			var gen2j = gen2.ToGroupElementJacobian();
			var r2j = ecmult_ctx.Mult(gen2j, s, Scalar.Zero);
			r2j = r2j.AddVariable(tmpj, out _);
			var r1 = r1j.ToGroupElement();
			var r2 = r2j.ToGroupElement();
			secp256k1_dleq_challenge_hash(algo16, gen2, r1, r2, p1, p2, out var e_expected);
			e_expected = e_expected.Add(e_neg);
			return e_expected.IsZero;
		}

		static StringBuilder builder = new StringBuilder();
		private static void secp256k1_dleq_challenge_hash(ReadOnlySpan<byte> algo16, in GE gen2, in GE r1, in GE r2, in GE p1, in GE p2, out Scalar e)
		{
			Span<byte> buf32 = stackalloc byte[32];
			using SHA256 sha = new SHA256();
			sha.InitializeTagged(algo16.Slice(0, algo16_len(algo16)));
			sha.HashPoint(gen2);
			sha.HashPoint(r1);
			sha.HashPoint(r2);
			sha.HashPoint(p1);
			sha.HashPoint(p2);
			sha.GetHash(buf32);
			e = new Scalar(buf32);
		}

		static void nonce_function_dleq(ReadOnlySpan<byte> msg32, in Scalar key, string algo16, Span<byte> nonce32)
		{
			Span<byte> keybytes = stackalloc byte[32];
			key.WriteToSpan(keybytes);
			Span<byte> algobytes = stackalloc byte[16];
			Encoding.ASCII.GetBytes(algo16, algobytes);
			nonce_function_dleq(msg32, keybytes, algobytes, nonce32);
		}
		/* Modified bip340 nonce function */
		static void nonce_function_dleq(ReadOnlySpan<byte> msg32, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> algo16, Span<byte> nonce32)
		{
			using var sha = new SHA256();
			sha.InitializeTagged(algo16.Slice(0, algo16_len(algo16)));
			sha.Write(key32.Slice(0, 32));
			sha.Write(msg32.Slice(0, 32));
			sha.GetHash(nonce32);
		}

		/* Remove terminating NUL bytes */
		static int algo16_len(ReadOnlySpan<byte> algo16)
		{
			int algo16_len = 16;
			/* Remove terminating null bytes */
			while (algo16_len > 0 && algo16[algo16_len - 1] == 0)
			{
				algo16_len--;
			}
			return algo16_len;
		}

		internal static void HashPoint(this SHA256 sha, in GE p)
		{
			Span<byte> buf33 = stackalloc byte[33];
			Internals.secp256k1_dleq_serialize_point(buf33, p);
			sha.Write(buf33);
		}
	}
}
