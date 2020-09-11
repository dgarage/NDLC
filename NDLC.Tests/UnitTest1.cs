using NBitcoin.DataEncoders;
using NBitcoin.Logging;
using NBitcoin.Secp256k1;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using NDLC.Secp256k1;
using System.Text;
using NBitcoin;

namespace NDLC.Tests
{
	public class UnitTest1
	{
		private readonly ITestOutputHelper Log;

		public RFC6979HMACSHA256 Random;

		public UnitTest1(ITestOutputHelper outputHelper)
		{
			this.Log = outputHelper;
			Random = new RFC6979HMACSHA256();
			Span<byte> seed = stackalloc byte[32];
			RandomUtils.GetBytes(seed);
			Log.WriteLine("Random seed: " + Encoders.Hex.EncodeData(seed));
			Random.Initialize(seed);
		}

		ECMultGenContext ecmult_gen_ctx = ECMultGenContext.Instance;
		ECMultContext ecmult_ctx = ECMultContext.Instance;

		[Fact]
		public void CanExtractGetKeyFromSchnorrSig()
		{
			for (int i = 0; i < 40; i++)
			{
				var priv = new Key();
				var msg1 = RandomUtils.GetBytes(32);
				var nonce = RandomUtils.GetBytes(32);
				priv.ToECPrivKey().TrySignBIP140DLC_FIX(msg1, new PrecomputedNonceFunctionHardened(nonce), out var sig1);
				var msg2 = RandomUtils.GetBytes(32);
				priv.ToECPrivKey().TrySignBIP140DLC_FIX(msg2, new PrecomputedNonceFunctionHardened(nonce), out var sig2);

				var privkey = priv.PubKey.ToECPubKey().ExtractPrivateKey(msg1, sig1, msg2, sig2);
				Assert.Equal(priv.ToHex(), Encoders.Hex.EncodeData(privkey.ToBytes()));
			}
		}

		[Fact]
		public void dleq_tests()
		{
			for (int i = 0; i < 1000; i++)
			{
				dleq_tests_core();
			}
		}
		void dleq_tests_core()
		{
			Scalar s, e;
			byte[] algo16 = new byte[16];
			Scalar sk = rand_scalar();
			GE gen2 = rand_point();
			GE p1, p2;
			Assert.True(ecmult_gen_ctx.secp256k1_dleq_proof(algo16, sk, gen2, out s, out e));
			ecmult_gen_ctx.secp256k1_dleq_pair(sk, gen2, out p1, out p2);
			Assert.True(ecmult_ctx.secp256k1_dleq_verify(algo16, s, e, p1, gen2, p2));

			{
				byte[] algo16_tmp = new byte[16];
				algo16_tmp.AsSpan().Fill(1);
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16_tmp, s, e, p1, gen2, p2));
			}
			{
				Scalar tmp = new Scalar(1);
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16, tmp, e, p1, gen2, p2));
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16, s, tmp, p1, gen2, p2));
			}
			{
				GE p_tmp = rand_point();
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16, s, e, p_tmp, gen2, p2));
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16, s, e, p1, p_tmp, p2));
				Assert.False(ecmult_ctx.secp256k1_dleq_verify(algo16, s, e, p1, gen2, p_tmp));
			}
		}

		private Scalar rand_scalar()
		{
			Span<byte> buf32 = stackalloc byte[32];
			Random.Generate(buf32);
			return new Scalar(buf32);
		}

		private GE rand_point()
		{
			Scalar x = rand_scalar();
			GEJ pointj = ECMultGenContext.Instance.MultGen(x);
			return pointj.ToGroupElement();
		}

		void rand_flip_bit(Span<byte> array)
		{
			array[secp256k1_rand_int(array.Length)] ^= (byte)(1 << secp256k1_rand_int(8));
		}

		private int secp256k1_rand_int(int n)
		{
			Span<byte> bytes = stackalloc byte[8];
			Random.Generate(bytes);
			var i = MemoryMarshal.Cast<byte, int>(bytes)[0];
			if (i < 0)
				i = -i;
			return i % n;
		}

		[Fact]
		public void adaptor_tests()
		{
			for (int i = 0; i < 1000; i++)
			{
				adaptor_tests_core();
			}
		}
		void adaptor_tests_core()
		{
			var secKey = Context.Instance.CreateECPrivKey(rand_scalar());
			Span<byte> msg = stackalloc byte[32];
			Random.Generate(msg);
			var adaptor_secret = Context.Instance.CreateECPrivKey(rand_scalar());

			var pubkey = secKey.CreatePubKey();
			var adaptor = adaptor_secret.CreatePubKey();
			Assert.True(secKey.TrySignAdaptor(msg, adaptor, out var adaptor_sig, out var adaptor_proof));
			{
				/* Test adaptor_sig_serialize roundtrip */
				Span<byte> adaptor_sig_tmp = stackalloc byte[65];
				Span<byte> adaptor_sig_tmp2 = stackalloc byte[65];
				adaptor_sig.WriteToSpan(adaptor_sig_tmp);
				Assert.True(SecpECDSAAdaptorSignature.TryCreate(adaptor_sig_tmp, out var adaptor_sig2));
				adaptor_sig2.WriteToSpan(adaptor_sig_tmp2);
				Assert.True(adaptor_sig_tmp.SequenceEqual(adaptor_sig_tmp2));
			}

			///* Test adaptor_sig_verify */
			Assert.True(pubkey.SigVerify(adaptor_sig, adaptor_proof, msg, adaptor));
			{
				Span<byte> adaptor_sig_tmp = stackalloc byte[65];
				adaptor_sig.WriteToSpan(adaptor_sig_tmp);
				rand_flip_bit(adaptor_sig_tmp);
				if (SecpECDSAAdaptorSignature.TryCreate(adaptor_sig_tmp, out var sigg))
				{
					Assert.False(pubkey.SigVerify(sigg, adaptor_proof, msg, adaptor));
				}
			}
			Assert.False(adaptor.SigVerify(adaptor_sig, adaptor_proof, msg, adaptor));
			{
				Span<byte> msg_tmp = stackalloc byte[32];
				msg.CopyTo(msg_tmp);
				rand_flip_bit(msg_tmp);
				Assert.False(pubkey.SigVerify(adaptor_sig, adaptor_proof, msg_tmp, adaptor));
			}
			Assert.False(pubkey.SigVerify(adaptor_sig, adaptor_proof, msg, pubkey));
			{
				Span<byte> adaptor_proof_tmp = stackalloc byte[97];
				adaptor_proof.WriteToSpan(adaptor_proof_tmp);
				rand_flip_bit(adaptor_proof_tmp);
				if (SecpECDSAAdaptorProof.TryCreate(adaptor_proof_tmp, out var proof))
				{
					Assert.False(pubkey.SigVerify(adaptor_sig, proof, msg, adaptor));
				}
			}

			var sig = adaptor_sig.AdaptECDSA(adaptor_secret);
			///* Test adaptor_adapt */
			Assert.True(pubkey.SigVerify(sig, msg));
			{
				/* Test adaptor_extract_secret */
				Assert.True(adaptor_sig.TryExtractSecret(sig, adaptor, out var adaptor_secret2));
				Assert.Equal(adaptor_secret, adaptor_secret2);
			}
		}

		string ToC(Span<byte> b)
		{
			StringBuilder str = new StringBuilder();
			str.Append("{");
			foreach (var bb in b)
			{
				str.Append($"0x{bb.ToString("X1")}, ");
			}
			str.Append("};");
			return str.ToString();
		}
		string ToC(ECPrivKey key)
		{
			byte[] bytes = new byte[32];
			key.WriteToSpan(bytes);
			return ToC(bytes);
		}
	}
}
