using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Secp256k1
{
	public class PrecomputedNonceFunctionHardened : INonceFunctionHardened
	{
		private readonly byte[] nonce;

		public PrecomputedNonceFunctionHardened(byte[] nonce)
		{
			this.nonce = nonce;
		}
		public bool TryGetNonce(Span<byte> nonce32, ReadOnlySpan<byte> msg32, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> xonly_pk32, ReadOnlySpan<byte> algo16)
		{
			nonce.AsSpan().CopyTo(nonce32);
			return true;
		}
	}
}
