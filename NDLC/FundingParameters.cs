using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NDLC
{
	public class FundingParty
	{
		public FundingParty(Money collateral, FundingInput[]? fundingInputs, Script? change, PubKey fundPubKey, VSizes vSizes)
		{
			Collateral = collateral;
			FundingInputs = fundingInputs ?? Array.Empty<FundingInput>();
			Change = change;
			FundPubKey = fundPubKey;
			VSizes = vSizes;
		}
		public Money Collateral { get; }
		public FundingInput[] FundingInputs { get; }
		public Script? Change { get; set; }
		public PubKey FundPubKey { get; set; }
		public VSizes VSizes { get; set; }
	}
	public class FundingParameters
	{
		private readonly Transaction? transactionOverride;
		public FundingParameters(
			FundingParty offerer,
			FundingParty acceptor,
			FeeRate feeRate,
			Transaction? transactionOverride)
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
			var offererCoins = Offerer.FundingInputs.Select(f => f.AsCoin()).ToArray();
			var acceptorCoins = Acceptor.FundingInputs.Select(f => f.AsCoin()).ToArray();
			var fundingScript = GetFundingScript();
			var p2wsh = fundingScript.WitHash.ScriptPubKey;
			Transaction? tx = null;
			if (transactionOverride is null)
			{
				tx = network.CreateTransaction();
				tx.Version = 2;
				tx.LockTime = 0;
				foreach (var input in Offerer.FundingInputs.Concat(Acceptor.FundingInputs))
				{
					var txin = tx.Inputs.Add(input.AsCoin().Outpoint, Script.Empty);
					if (input.RedeemScript is Script)
						txin.ScriptSig = new Script(Op.GetPushOp(input.RedeemScript.ToBytes()));
				}
				foreach (var input in tx.Inputs)
					input.Sequence = 0xffffffff;
				tx.Outputs.Add(Offerer.Collateral + Acceptor.Collateral, p2wsh);
				var totalInput = offererCoins.Select(s => s.Amount).Sum();
				if (Offerer.Change is Script change)
				{
					tx.Outputs.Add(totalInput - Offerer.Collateral, change);
				}

				totalInput = acceptorCoins.Select(s => s.Amount).Sum();

				if (Acceptor.Change is Script change2)
				{
					tx.Outputs.Add(totalInput
								- Acceptor.Collateral, change2);
				}

				tx.Outputs[1].Value -= FeeRate.GetFee(Offerer.VSizes.Funding);
				tx.Outputs[2].Value -= FeeRate.GetFee(Acceptor.VSizes.Funding);

				var offererFee = FeeRate.GetFee(Offerer.VSizes.CET);
				var acceptorFee = FeeRate.GetFee(Acceptor.VSizes.CET);
				tx.Outputs[1].Value -= offererFee;
				tx.Outputs[2].Value -= acceptorFee;
				tx.Outputs[0].Value += offererFee + acceptorFee;
			}
			else
			{
				tx = transactionOverride;
			}
			var psbt = PSBT.FromTransaction(tx, network);
			foreach (var input in Offerer.FundingInputs.Concat(Acceptor.FundingInputs))
			{
				var txin = psbt.Inputs.FindIndexedInput(input.AsCoin().Outpoint);
				txin.RedeemScript = input.RedeemScript;
				txin.NonWitnessUtxo = input.InputTransaction;
				txin.WitnessUtxo = input.Output;
			}
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
		public FundingPSBT(PSBT pSBT, ScriptCoin fundCoin)
		{
			PSBT = pSBT;
			FundCoin = fundCoin;
		}

		public PSBT PSBT { get; set; }
		public ScriptCoin FundCoin { get; set; }
	}
}
