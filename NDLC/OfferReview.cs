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
			OraclePubKey = Encoders.Hex.EncodeData(offer.OracleInfo.PubKey.ToBytes());
			Nonce = offer.OracleInfo.RValue.ToString();
			OffererPnL = new List<ProfitAndLoss>();
			AcceptorPnL = new List<ProfitAndLoss>();
			AcceptorCollateral = offer.ContractInfo.Select(o => o.Payout - offer.TotalCollateral).Max();
			AcceptorCollateral = Money.Max(Money.Zero, AcceptorCollateral);
			foreach (var ci in offer.ContractInfo)
			{
				var offerer = new ProfitAndLoss(offer.TotalCollateral, ci);
				OffererPnL.Add(offerer);
				AcceptorPnL.Add(new ProfitAndLoss(AcceptorCollateral, offerer));
			}
		}
		public string OraclePubKey { get; set; }
		public string Nonce { get; set; }
		[JsonConverter(typeof(BTCSatsJsonConverter))]
		public Money AcceptorCollateral { get; set; }
		public List<ProfitAndLoss> OffererPnL { get; set; }
		public List<ProfitAndLoss> AcceptorPnL { get; set; }

	}
	public class ProfitAndLoss
	{
		public ProfitAndLoss(Money acceptorCollateral, ProfitAndLoss offererPNL)
		{
			Value = acceptorCollateral - offererPNL.Value;
			IsHash = offererPNL.IsHash;
			Outcome = offererPNL.Outcome;
		}
		public ProfitAndLoss(Money collateral, ContractInfo ci)
		{
			Value = ci.Payout - collateral;
			IsHash = ci.Outcome.OutcomeString is null;
			Outcome = ci.Outcome.ToString();
		}
		public string Outcome { get; set; }
		public bool IsHash { get; set; }
		[JsonConverter(typeof(BTCSatsJsonConverter))]
		public Money Value { get; set; }
	}
}
