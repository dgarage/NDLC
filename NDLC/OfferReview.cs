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
			OffererPnL = new List<ProfitAndLoss>();
			AcceptorPnL = new List<ProfitAndLoss>();

			var offererPnL = offer.CalculatePnL();
			var acceptorPnL = offererPnL.Inverse();
			AcceptorCollateral = acceptorPnL.CalculateCollateral();
			for (int i = 0; i < offererPnL.Count; i++)
			{
				OffererPnL.Add(new ProfitAndLoss(offererPnL[i]));
				AcceptorPnL.Add(new ProfitAndLoss(acceptorPnL[i]));
			}
			Timeouts = offer.Timeouts;
		}
		public string OraclePubKey { get; set; }
		public string Nonce { get; set; }
		[JsonConverter(typeof(BTCSatsJsonConverter))]
		public Money AcceptorCollateral { get; set; }
		public List<ProfitAndLoss> OffererPnL { get; set; }
		public List<ProfitAndLoss> AcceptorPnL { get; set; }
		public Timeouts Timeouts { get; set; }
	}
	public class ProfitAndLoss
	{
		public ProfitAndLoss(PnLOutcome pnl)
		{
			Reward = pnl.Reward;
			IsHash = pnl.Outcome.OutcomeString is null;
			Outcome = pnl.Outcome.ToString();
		}
		public ProfitAndLoss(Money collateral, ContractInfo ci)
		{
			Reward = ci.Payout - collateral;
			IsHash = ci.Outcome.OutcomeString is null;
			Outcome = ci.Outcome.ToString();
		}
		public string Outcome { get; set; }
		public bool IsHash { get; set; }
		[JsonConverter(typeof(BTCSatsJsonConverter))]
		public Money Reward { get; set; }
	}
}
