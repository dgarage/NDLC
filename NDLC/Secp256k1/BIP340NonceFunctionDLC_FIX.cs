using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NDLC.Secp256k1
{
	public class BIP340NonceFunctionDLC_FIX : INonceFunctionHardened
	{
		static RandomNumberGenerator rand = new RNGCryptoServiceProvider();
		ReadOnlyMemory<byte> data32;
		public BIP340NonceFunctionDLC_FIX(ReadOnlyMemory<byte> auxData32)
		{
			if (auxData32.Length != 0 && auxData32.Length != 32)
				throw new ArgumentException("auxData32 should be 0 or 32 bytes", nameof(auxData32));
			this.data32 = auxData32;
		}
		public BIP340NonceFunctionDLC_FIX(bool random)
		{
			if (random)
			{
				var a = new byte[32];
				rand.GetBytes(a);
				this.data32 = a;
			}
		}

		static byte[] TAG_BIP0340AUX = ASCIIEncoding.ASCII.GetBytes("BIP340/aux");
		public readonly static byte[] ALGO_BIP340 = ASCIIEncoding.ASCII.GetBytes("BIP340/nonce\0\0\0\0");
		public readonly static byte[] TAG_BIP340 = ASCIIEncoding.ASCII.GetBytes("BIP340/nonce");
		public bool TryGetNonce(Span<byte> nonce32, ReadOnlySpan<byte> msg32, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> xonly_pk32, ReadOnlySpan<byte> algo16)
		{
			int i = 0;
			Span<byte> masked_key = stackalloc byte[32];
			using NBitcoin.Secp256k1.SHA256 sha = new NBitcoin.Secp256k1.SHA256();
			if (algo16.Length != 16)
				return false;

			if (data32.Length == 32)
			{
				sha.InitializeTagged(TAG_BIP0340AUX);
				sha.Write(data32.Span);
				sha.GetHash(masked_key);
				for (i = 0; i < 32; i++)
				{
					masked_key[i] ^= key32[i];
				}
			}

			// * Tag the hash with algo16 which is important to avoid nonce reuse across
			// * algorithms. If this nonce function is used in BIP-340 signing as defined
			// * in the spec, an optimized tagging implementation is used. */

			if (algo16.SequenceCompareTo(ALGO_BIP340) == 0)
			{
				sha.InitializeTagged(TAG_BIP340);
			}
			else
			{
				int algo16_len = 16;
				/* Remove terminating null bytes */
				while (algo16_len > 0 && algo16[algo16_len - 1] == 0)
				{
					algo16_len--;
				}
				sha.InitializeTagged(algo16.Slice(0, algo16_len));
			}

			//* Hash (masked-)key||pk||msg using the tagged hash as per the spec */
			if (data32.Length == 32)
			{
				sha.Write(masked_key);
			}
			else
			{
				sha.Write(key32);
			}
			sha.Write(xonly_pk32);
			sha.Write(msg32);
			sha.GetHash(nonce32);
			return true;
		}
	}
}
