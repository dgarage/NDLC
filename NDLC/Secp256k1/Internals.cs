using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Secp256k1
{
	internal static class Internals
	{
		internal static void secp256k1_dleq_serialize_point(Span<byte> buf33, in GE p)
		{
			var y = p.y.Normalize();
			buf33[0] = (byte)(y.IsOdd ? 1 : 0);
			var x = p.x.Normalize();
			x.WriteToSpan(buf33.Slice(1));
		}

		internal static bool secp256k1_dleq_deserialize_point(ReadOnlySpan<byte> buf33, out GE p)
		{
			if (!FE.TryCreate(buf33.Slice(1), out var x))
			{
				p = default;
				return false;
			}
			if (buf33[0] > 1)
			{
				p = default;
				return false;
			}
			if (!GE.TryCreateXOVariable(x, buf33[0] == 1, out p))
				return false;
			return true;
		}
	}
}
