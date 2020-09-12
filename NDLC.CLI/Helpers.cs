using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI
{
	public static class Helpers
	{
		public static string ToString(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteToSpan(buf);
			return Encoders.Hex.EncodeData(buf);
		}
		public static string ToBase58(ECXOnlyPubKey pubKey)
		{
			var buf = new byte[32];
			pubKey.WriteToSpan(buf);
			return Encoders.Base58.EncodeData(buf);
		}

		public static bool TryParse(string str, out DiscretePayoff? payoff)
		{
			payoff = null;
			str = str.Trim();
			var i = str.LastIndexOf(':');
			if (i == -1)
				return false;

			var outcome = str.Substring(0, i);
			var reward = str.Substring(i + 1);
			if (!Money.TryParse(reward, out var btc) || btc is null)
				return false;
			payoff = new DiscretePayoff(new DiscreteOutcome(outcome), reward);
			return true;
		}
	}
}
