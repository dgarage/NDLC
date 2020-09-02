using NBitcoin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC
{
	public class PSBTFundingTemplate
	{
		public static bool TryParse(PSBT psbt, out PSBTFundingTemplate? template)
		{
			template = null;
			if (psbt == null)
				throw new ArgumentNullException(nameof(psbt));
			var fundingOutput = psbt.Outputs
								.FirstOrDefault(o => o.ScriptPubKey == Constants.FundingPlaceholder);
			var payoutOutput = psbt.Outputs
								.FirstOrDefault(o => o.ScriptPubKey != Constants.FundingPlaceholder && o.Value == Constants.PayoutAmount);
			var changeOutput = psbt.Outputs
								.FirstOrDefault(o => o.ScriptPubKey != Constants.FundingPlaceholder && o.Value != Constants.PayoutAmount);
			if (fundingOutput is null || payoutOutput is null)
				return false;
			var coins = psbt.Inputs.Select(o => o.GetSignableCoin()).ToList().AsReadOnly();
			if (coins.Any(c => c is null))
				return false;
			if (!psbt.TryGetEstimatedFeeRate(out var estimated) || estimated is null)
				throw new InvalidOperationException("This should never happen");
			template = new PSBTFundingTemplate(changeOutput?.ScriptPubKey, fundingOutput.Value, payoutOutput.ScriptPubKey, estimated, coins, psbt.Network);
			return true;
		}
		public static bool TryParse(string psbt, Network network, out PSBTFundingTemplate? template)
		{
			if (psbt == null)
				throw new ArgumentNullException(nameof(psbt));
			template = null;
			if (!PSBT.TryParse(psbt, network, out var psbtObj) || psbtObj is null)
				return false;
			return TryParse(psbtObj, out template);
		}
		public PSBTFundingTemplate(Script? change, Money collateral, Script payoutAddress, FeeRate feeRate, IReadOnlyCollection<Coin> fundingCoins, Network network)
		{
			Change = change ?? throw new ArgumentNullException(nameof(change));
			Collateral = collateral ?? throw new ArgumentNullException(nameof(collateral));
			PayoutAddress = payoutAddress ?? throw new ArgumentNullException(nameof(payoutAddress));
			FeeRate = feeRate ?? throw new ArgumentNullException(nameof(feeRate));
			FundingCoins = fundingCoins ?? throw new ArgumentNullException(nameof(fundingCoins));
			Network = network ?? throw new ArgumentNullException(nameof(network));
		}
		public Network Network { get; }
		public IReadOnlyCollection<Coin> FundingCoins { get; }
		public Script? Change { get; }
		public Money Collateral { get; }
		public Script PayoutAddress { get; }
		public FeeRate FeeRate { get; }

		public static PSBTFundingTemplate Parse(PSBT psbt)
		{
			if (psbt == null)
				throw new ArgumentNullException(nameof(psbt));
			if (!TryParse(psbt, out var t) || t is null)
				throw new FormatException("Invalid PSBT Funding template");
			return t;
		}
	}
}
