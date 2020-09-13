using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.Messages.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NDLC
{
	public class OfferReview
	{
		public OfferReview(Offer offer)
		{
			if (offer?.OracleInfo is null)
				throw new ArgumentException("OracleInfo is missing", nameof(offer));
			if (offer.ContractInfo is null)
				throw new ArgumentException("ContractInfo is missing", nameof(offer));
			if (offer.ContractInfo.Length is 0)
				throw new ArgumentException("ContractInfo is empty", nameof(offer));
			if (offer.TotalCollateral is null)
				throw new ArgumentException("TotalCollateral is missing", nameof(offer));
			if (offer.Timeouts is null)
				throw new ArgumentException("Timeout is missing", nameof(offer));
			OraclePubKey = Encoders.Hex.EncodeData(offer.OracleInfo.PubKey.ToBytes());
			Nonce = offer.OracleInfo.RValue.ToString();
			OffererPayoffs = new List<Payoff>();
			AcceptorPayoffs = new List<Payoff>();

			var offererPnL = offer.ToDiscretePayoffs();
			var acceptorPnL = offererPnL.Inverse();
			AcceptorCollateral = acceptorPnL.CalculateMinimumCollateral();
			for (int i = 0; i < offererPnL.Count; i++)
			{
				OffererPayoffs.Add(new Payoff(offererPnL[i]));
				AcceptorPayoffs.Add(new Payoff(acceptorPnL[i]));
			}
			Timeouts = offer.Timeouts;
		}
		public string OraclePubKey { get; set; }
		public string Nonce { get; set; }
		[JsonConverter(typeof(BTCSatsJsonConverter))]
		public Money AcceptorCollateral { get; set; }
		public List<Payoff> OffererPayoffs { get; set; }
		public List<Payoff> AcceptorPayoffs { get; set; }
		public Timeouts Timeouts { get; set; }

		public class Payoff
		{
			public Payoff(DiscretePayoff payoff)
			{
				Reward = payoff.Reward;
				if (payoff.Outcome.OutcomeString is null)
					throw new ArgumentException(message: "The outcome string should not be empty");
				Outcome = payoff.Outcome.OutcomeString;
			}
			public Payoff(Money collateral, ContractInfo ci)
			{
				Reward = ci.Payout - collateral;
				if (ci.Outcome.OutcomeString is null)
					throw new ArgumentException(message: "The outcome string should not be empty");
				Outcome = ci.Outcome.OutcomeString;
			}
			public string Outcome { get; set; }
			[JsonConverter(typeof(BTCSatsJsonConverter))]
			public Money Reward { get; set; }
		}
	}
}
