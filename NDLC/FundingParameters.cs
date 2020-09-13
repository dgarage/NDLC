using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NDLC
{
	public class FundingParty
	{
		public FundingParty(Money collateral, Coin[]? fundingCoins, Script? change, PubKey fundPubKey)
		{
			Collateral = collateral;
			FundingCoins = fundingCoins ?? Array.Empty<Coin>();
			Change = change;
			FundPubKey = fundPubKey;
		}
		public Money Collateral { get; }
		public Coin[] FundingCoins { get; }
		public Script? Change { get; set; }
		public PubKey FundPubKey { get; set; }
	}
	public class FundingParameters
	{
		private readonly Transaction? transactionOverride;

		public FundingParameters(FundingParty offerer, FundingParty acceptor, FeeRate feeRate, Transaction? transactionOverride)
		{
			Offerer = offerer;
			Acceptor = acceptor;
			FeeRate = feeRate;
			this.transactionOverride = transactionOverride;
		}

		public FundingParty Offerer { get; set; }
		public FundingParty Acceptor { get; set; }
		public FeeRate FeeRate { get; set; }
		public FundingPSBT Build(Network network)
		{
			var fundingScript = GetFundingScript();
			var p2wsh = fundingScript.WitHash.ScriptPubKey;
			Transaction? tx = null;
			if (transactionOverride is null)
			{
				tx = network.CreateTransaction();
				tx.Version = 2;
				tx.LockTime = 0;
				foreach (var input in Offerer.FundingCoins)
				{
					tx.Inputs.Add(input.Outpoint, Script.Empty);
				}
				foreach (var input in Acceptor.FundingCoins)
				{
					tx.Inputs.Add(input.Outpoint, Script.Empty);
				}
				foreach (var input in tx.Inputs)
					input.Sequence = 0xffffffff;
				tx.Outputs.Add(Offerer.Collateral + Acceptor.Collateral, p2wsh);
				var totalInput = Offerer.FundingCoins.Select(s => s.Amount).Sum();
				if (Offerer.Change is Script change)
				{
					tx.Outputs.Add(totalInput - Offerer.Collateral, change);
				}

				totalInput = Acceptor.FundingCoins.Select(s => s.Amount).Sum();

				if (Acceptor.Change is Script change2)
				{
					tx.Outputs.Add(totalInput
								- Acceptor.Collateral, change2);
				}

				var expectedFee = FeeRate.GetFee(700);
				var parts = expectedFee.Split(2).ToArray();
				tx.Outputs[1].Value -= parts[1];
				tx.Outputs[2].Value -= parts[1];

				var futureFee = FeeRate.GetFee(169);
				parts = futureFee.Split(2).ToArray();
				tx.Outputs[1].Value -= parts[1];
				tx.Outputs[2].Value -= parts[1];
				tx.Outputs[0].Value += futureFee;
			}
			else
			{
				tx = transactionOverride;
			}
			var psbt = PSBT.FromTransaction(tx, network);
			psbt.AddCoins(Offerer.FundingCoins);
			psbt.AddCoins(Acceptor.FundingCoins);
			return new FundingPSBT(psbt, new ScriptCoin(tx, 0, fundingScript));
		}
		static readonly Comparer<PubKey> LexicographicComparer = Comparer<PubKey>.Create((a, b) => Comparer<string>.Default.Compare(a.ToHex(), b.ToHex()));
		private Script GetFundingScript()
		{
			if (Offerer?.FundPubKey is null || Acceptor?.FundPubKey is null)
				throw new InvalidOperationException("We did not received enough data to create the funding script");
			var keys = new[] { Offerer.FundPubKey, Acceptor.FundPubKey };
			Array.Sort(keys, LexicographicComparer);
			return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, keys);
		}
	}

	public class FundingPSBT
	{
		public FundingPSBT(PSBT pSBT, Coin fundCoin)
		{
			PSBT = pSBT;
			FundCoin = fundCoin;
		}

		public PSBT PSBT { get; set; }
		public Coin FundCoin { get; set; }
	}
}
