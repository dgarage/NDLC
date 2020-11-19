using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.TLV;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace NDLC.Messages
{
	public class FundingInformation
	{
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money? TotalCollateral { get; set; }
		public PubKeyObject? PubKeys { get; set; }
		public FundingInput[]? FundingInputs { get; set; }
		public BitcoinAddress? ChangeAddress { get; set; }

		public DiscretePayoffs ToDiscretePayoffs(ContractInfo[] contractInfo)
		{
			if (contractInfo is null || contractInfo.Length is 0)
				throw new ArgumentException("contractInfo is required", nameof(contractInfo));
			DiscretePayoffs payoffs = new DiscretePayoffs();
			foreach (var ci in contractInfo)
			{
				payoffs.Add(new DiscretePayoff(ci.Outcome, ci.Payout - TotalCollateral));
			}
			return payoffs;
		}
		public static byte[] MaxWitnessLengthKey = new byte[] { 0x38, 0x63, 0x18, 0x20, 0x37, 0x21 };
		public PSBT CreateSetupPSBT(Network network)
		{
			if (FundingInputs is null || PubKeys is null)
				throw new InvalidOperationException("Funding inputs or pubkeys are null");
			Transaction tx = network.CreateTransaction();
			foreach (var input in FundingInputs)
			{
				var c = input.AsCoin();
				tx.Inputs.Add(c.Outpoint);
			}
			var total = FundingInputs.Select(c => c.AsCoin().Amount).Sum();
			tx.Outputs.Add(TotalCollateral, PubKeys.PayoutAddress);
			tx.Outputs.Add(TotalCollateral - total, ChangeAddress);
			var psbt = PSBT.FromTransaction(tx, network);
			foreach (var input in FundingInputs)
			{
				var c = input.AsCoin();
				var psbtInput = psbt.Inputs.FindIndexedInput(c.Outpoint);
				psbtInput.NonWitnessUtxo = input.InputTransaction;
				psbtInput.RedeemScript = input.RedeemScript;
				if (input.MaxWitnessLength is int)
					psbtInput.Unknown.Add(MaxWitnessLengthKey, Utils.ToBytes((uint)input.MaxWitnessLength, true));
			}
			return psbt;
		}
	}

}
