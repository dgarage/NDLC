using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;

namespace NDLC.Messages
{
	public class DLCTransactionBuilder
	{
		private readonly Network network;

		class Party
		{
			public PubKey? FundPubKey;
			public Money? Collateral;
			public ECDSASignature? RefundSig;
			public Dictionary<DLCOutcome, AdaptorSignature>? OutcomeSigs;
			public Script? Payout;
			public Coin[]? FundingCoins;
			public Dictionary<OutPoint, List<PartialSignature>>? FundingSigs;
			public Script? Change;
		}

		Party? Acceptor
		{
			get
			{
				return isInitiator ? Remote : Us;
			}
			set
			{
				if (isInitiator)
					Remote = value;
				else
					Us = value;
			}
		}
		Party? Offerer
		{
			get
			{
				return isInitiator ? Us : Remote;
			}
			set
			{
				if (isInitiator)
					Us = value;
				else
					Remote = value;
			}
		}
		Party? Remote;
		Party? Us;

		OracleInfo? OracleInfo;
		FeeRate? FeeRate;
		Timeouts? Timeouts;
		Dictionary<DLCOutcome, Money>? OffererRewards;
		public Transaction? FundingOverride { get; set; }

		public DLCTransactionBuilder(bool isInitiator, Offer? offer, Accept? accept, Sign? sign, Network network)
		{
			this.isInitiator = isInitiator;
			FillStateFrom(offer);
			FillStateFrom(accept);
			FillStateFrom(sign);
			this.network = network;
		}

		public void FillStateFrom(Offer? offer)
		{
			if (offer is null)
				return;
			OracleInfo = offer.OracleInfo;
			Timeouts = offer.Timeouts;
			Offerer ??= new Party();
			if (offer.ContractInfo is ContractInfo[] ci)
			{
				foreach (var i in ci)
				{
					if (i.Outcome is DLCOutcome && i.Sats is Money)
					{
						OffererRewards ??= new Dictionary<DLCOutcome, Money>();
						OffererRewards.Add(i.Outcome, i.Sats);
					}
				}
			}

			Offerer.Collateral = offer.TotalCollateral;
			Offerer.FundPubKey = offer.PubKeys?.FundingKey;
			Offerer.Payout = offer.PubKeys?.PayoutAddress?.ScriptPubKey;
			Offerer.Change = offer.ChangeAddress?.ScriptPubKey;
			Offerer.FundingCoins = offer.FundingInputs?.Select(c => new Coin(c.Outpoint, c.Output))?.ToArray(); ;
			FeeRate = offer.FeeRate;
		}
		public void FillStateFrom(Accept? accept)
		{
			if (accept is null)
				return;
			Acceptor ??= new Party();
			Acceptor.FundPubKey = accept.PubKeys?.FundingKey;
			Acceptor.OutcomeSigs = accept.CetSigs?.OutcomeSigs;
			Acceptor.RefundSig = accept.CetSigs?.RefundSig?.Signature.Signature;
			Acceptor.Collateral = accept.TotalCollateral;
			Acceptor.Payout = accept.PubKeys?.PayoutAddress?.ScriptPubKey;
			Acceptor.Change = accept.ChangeAddress?.ScriptPubKey;
			Acceptor.FundingCoins = accept.FundingInputs?.Select(c => new Coin(c.Outpoint, c.Output))?.ToArray();
		}
		public void FillStateFrom(Sign? sign)
		{
			if (sign is null)
				return;
			Offerer ??= new Party();
			Offerer.OutcomeSigs = sign.CetSigs?.OutcomeSigs;
			Offerer.RefundSig = sign.CetSigs?.RefundSig?.Signature.Signature;
			Offerer.FundingSigs = sign.FundingSigs;
		}

		bool isInitiator;

		public Transaction BuildRefund()
		{
			if (Timeouts is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (Offerer?.Collateral is null || Acceptor?.Collateral is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (Offerer?.Payout is null || Acceptor?.Payout is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");

			var funding = BuildFunding();
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = Timeouts.ContractTimeout;
			tx.Inputs.Add(new OutPoint(funding.GetHash(), 0), sequence: 0xFFFFFFFE);
			tx.Outputs.Add(Offerer.Collateral, Offerer.Payout);
			tx.Outputs.Add(Acceptor.Collateral, Acceptor.Payout);
			return tx;
		}

		public Key? FundingKey { get; set; }
		public Offer Offer(PSBTFundingTemplate fundingTemplate, OracleInfo oracleInfo, ContractInfo[] contractInfo, Timeouts timeouts)
		{
			if (!isInitiator)
				throw new InvalidOperationException("The acceptor can't initiate an offer");
			var fundingKey = this.FundingKey ?? new Key();
			Offer offer = new Offer()
			{
				OracleInfo = oracleInfo,
				ContractInfo = contractInfo,
				Timeouts = timeouts
			};
			offer.FillFromTemplateFunding(fundingTemplate, fundingKey.PubKey);
			this.FundingKey = fundingKey;
			this.OracleInfo = oracleInfo;
			FillStateFrom(offer);
			return offer;
		}
		public Accept Accept(Offer offer,
							PSBTFundingTemplate fundingTemplate)
		{
			if (isInitiator)
				throw new InvalidOperationException("The initiator can't accept");
			if (fundingTemplate == null)
				throw new ArgumentNullException(nameof(fundingTemplate));
			this.FillStateFrom(offer);
			this.FundingKey = this.FundingKey ?? new Key();
			Accept accept = new Accept();
			accept.FillFromTemplateFunding(fundingTemplate, FundingKey.PubKey);
			FillStateFrom(accept);
			accept.CetSigs = CreateCetSigs();
			accept.EventId = "14965dcd10a1f0464c4c5cf7f2e9c67b2bcc6f8a971ccc647d7fec1c885f3afe";
			//accept.EventId = CalculateEventId();
			return accept;
		}

		//private string CalculateEventId()
		//{
		//	Span<byte> buf = stackalloc byte[64 + 4 + 8];
		//	offer.OracleInfo.WriteToBytes(buf);
		//	offer.Timeouts.WriteToBytes(buf);
		//}

		public void VerifySign(Accept accept)
		{
			if (!isInitiator)
				throw new InvalidOperationException("The acceptor can't sign");
			FillStateFrom(accept);
			AssertRemoteSigs();
		}
		public Sign EndSign(PSBT signedFunding)
		{
			Sign sign = new Sign();
			sign.CetSigs = CreateCetSigs();
			sign.FundingSigs = new Dictionary<OutPoint, List<PartialSignature>>();
			foreach (var input in signedFunding.Inputs)
			{
				if (!sign.FundingSigs.TryGetValue(input.PrevOut, out var sigs))
				{
					sigs = new List<PartialSignature>();
					sign.FundingSigs.Add(input.PrevOut, sigs);
				}
				foreach (var sig in input.PartialSigs)
				{
					sigs.Add(new PartialSignature(sig.Key, sig.Value));
				}
			}
			return sign;
		}

		private void AssertRemoteSigs()
		{
			if (!VerifyRemoteCetSigs())
				throw new InvalidOperationException("Invalid remote CET");
			if (!VerifyRemoteRefundSignature())
				throw new InvalidOperationException("Invalid remote refund signature");
		}

		public void VerifySign(Sign sign)
		{
			if (sign == null)
				throw new ArgumentNullException(nameof(sign));
			FillStateFrom(sign);
			AssertRemoteSigs();
		}

		public PSBT CombineFunding(PSBT signedFunding)
		{
			if (signedFunding == null)
				throw new ArgumentNullException(nameof(signedFunding));
			var partiallySigned = BuildFundingPSBT();
			var fullySigned = partiallySigned.Combine(signedFunding);
			fullySigned.AssertSanity();
			// This check if sigs are good!
			AssertSegwit(fullySigned.Clone().Finalize().ExtractTransaction());
			return fullySigned;
		}

		private void AssertSegwit(Transaction transaction)
		{
			foreach (var input in transaction.Inputs)
			{
				if (input.WitScript is null || input.WitScript == WitScript.Empty)
				{
					throw new InvalidOperationException("The funding transaction should not have non segwit inputs");
				}
			}
		}

		private CetSigs CreateCetSigs()
		{
			if (FundingKey is null || OffererRewards is null)
				throw new InvalidOperationException("Invalid state for creating CetSigs");
			var refund = BuildRefund();
			var signature = refund.SignInput(FundingKey, GetFundCoin());
			var cetSig = new CetSigs()
			{
				OutcomeSigs = OffererRewards
							  .Select(o => (o.Key, SignCET(FundingKey, o.Key)))
							  .ToDictionary(kv => kv.Key, kv => kv.Item2),
				RefundSig = new PartialSignature(FundingKey.PubKey, signature)
			};
			return cetSig;
		}

		private AdaptorSignature SignCET(Key key, DLCOutcome outcome)
		{
			if (OracleInfo is null)
				throw new InvalidOperationException("Invalid state for signing CET");
			var cet = BuildCET(outcome);
			var hash = cet.GetSignatureHash(GetFundCoin());
			if (!OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
				throw new InvalidOperationException("TryComputeSigpoint failed");
			if (!key.ToECPrivKey().TrySignAdaptor(hash.ToBytes(), sigpoint, out var sig, out var proof) || sig  is null || proof is null)
				throw new InvalidOperationException("TrySignAdaptor failed");
			return new AdaptorSignature(sig, proof);
		}

		public PSBT BuildFundingPSBT()
		{
			var psbt = PSBT.FromTransaction(BuildFunding(), this.network);
			foreach (var coin in Remote?.FundingCoins ?? Array.Empty<Coin>())
			{
				psbt.AddCoins(coin);
			}
			foreach (var coin in Us?.FundingCoins ?? Array.Empty<Coin>())
			{
				psbt.AddCoins(coin);
			}
			AddFundingSigs(Remote, psbt);
			AddFundingSigs(Us, psbt);
			return psbt;
		}

		private void AddFundingSigs(Party? party, PSBT psbt)
		{
			if (party?.FundingSigs is Dictionary<OutPoint, List<PartialSignature>> sigs1)
			{
				foreach (var kv in sigs1)
				{
					var input = psbt.Inputs.FindIndexedInput(kv.Key);
					foreach (var sig in kv.Value)
					{
						input.PartialSigs.Add(sig.PubKey, sig.Signature);
					}
				}
			}
		}

		public Transaction BuildFunding()
		{
			if (FundingOverride is Transaction)
				return FundingOverride;
			if (Offerer?.FundingCoins is null ||
				Acceptor?.FundingCoins is null ||
				FeeRate is null ||
				Offerer.Collateral is null ||
				Acceptor.Collateral is null)
				throw new InvalidOperationException("We did not received enough data to create the funding");
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = 0;
			var fundingScript = GetFundingScript();
			var p2wsh = fundingScript.WitHash.ScriptPubKey;
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
			tx.Outputs[1].Value -= parts[0];
			tx.Outputs[2].Value -= parts[0];

			var futureFee = FeeRate.GetFee(169);
			parts = futureFee.Split(2).ToArray();
			tx.Outputs[1].Value -= parts[0];
			tx.Outputs[2].Value -= parts[0];
			tx.Outputs[0].Value += futureFee;
			return tx;
		}

		public Transaction BuildSignedCET(Key oracleSecret)
		{
			if (OffererRewards is null ||
				Remote?.OutcomeSigs is null ||
				Remote?.FundPubKey is null ||
				this.FundingKey is null ||
				OracleInfo is null)
				throw new InvalidOperationException("Invalid state for building the signed CET");
			foreach (var outcome in OffererRewards.Select(o => o.Key))
			{
				if (!OracleInfo.TryComputeSigpoint(outcome, out var sigPoint) || sigPoint is null)
					continue;
				if (oracleSecret.PubKey.ToECPubKey() != sigPoint)
					continue;
				var cet = BuildCET(outcome);
				if (!Remote.OutcomeSigs.TryGetValue(outcome, out var encryptedSig))
					continue;
				var ecdsaSig = encryptedSig.Signature.AdaptECDSA(oracleSecret.ToECPrivKey());
				var builder = network.CreateTransactionBuilder();
				builder.AddCoins(GetFundCoin());
				builder.AddKnownSignature(Remote.FundPubKey, new TransactionSignature(ecdsaSig.ToDER(), SigHash.All), GetFundCoin().Outpoint);
				builder.AddKeys(this.FundingKey);
				builder.SignTransactionInPlace(cet);
				if (!builder.Verify(cet, out var err))
					throw new InvalidOperationException("This CET is not fully signed");
				return cet;
			}
			throw new InvalidOperationException("This oracle key is not valid");
		}

		private Script GetFundingScript()
		{
			if (Offerer?.FundPubKey is null || Acceptor?.FundPubKey is null)
				throw new InvalidOperationException("We did not received enough data to create the funding script");
			return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, Offerer.FundPubKey, Acceptor.FundPubKey);
		}

		public Transaction BuildCET(DLCOutcome outcome)
		{
			if (Timeouts is null ||
				Offerer?.Collateral is null ||
				Acceptor?.Collateral is null ||
				Offerer?.Payout is null ||
				Acceptor?.Payout is null ||
				Timeouts is null ||
				OffererRewards is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (!OffererRewards.TryGetValue(outcome, out var offererPayout) || offererPayout is null)
				throw new InvalidOperationException("Invalid outcome");
			var funding = BuildFunding();
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = Timeouts.ContractMaturity;
			tx.Inputs.Add(new OutPoint(funding.GetHash(), 0), sequence: 0xFFFFFFFE);

			var collateral = Offerer.Collateral + Acceptor.Collateral;
			tx.Outputs.Add(offererPayout, Offerer.Payout);
			tx.Outputs.Add(collateral - offererPayout, Acceptor.Payout);
			foreach (var output in tx.Outputs.ToArray())
			{
				if (output.Value < Money.Satoshis(1000))
					tx.Outputs.Remove(output);
			}
			return tx;
		}

		public bool VerifyRemoteCetSigs()
		{
			if (Remote?.FundPubKey is null || Remote?.OutcomeSigs is null || OracleInfo is null)
				throw new InvalidOperationException("We did not received enough data to verify the sigs");

			foreach (var outcome in OffererRewards.Select(o => o.Key))
			{
				if (!Remote.OutcomeSigs.TryGetValue(outcome, out var outcomeSig))
					return false;

				if (!OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
					return false;
				var ecPubKey = Remote.FundPubKey.ToECPubKey();
				var fundingCoin = GetFundCoin();
				var msg = BuildCET(outcome).GetSignatureHash(fundingCoin).ToBytes();
				if (!ecPubKey.SigVerify(outcomeSig.Signature, outcomeSig.Proof, msg, sigpoint))
					return false;
			}
			return true;
		}

		private ScriptCoin GetFundCoin()
		{
			return BuildFunding().Outputs.AsCoins().First().ToScriptCoin(GetFundingScript());
		}

		public bool VerifyRemoteRefundSignature()
		{
			if (Remote?.RefundSig is null || Remote?.FundPubKey is null)
				throw new InvalidOperationException("We did not received enough data to verify refund signature");
			var refund = BuildRefund();
			if (!Remote.FundPubKey.Verify(refund.GetSignatureHash(GetFundCoin()), Remote.RefundSig))
				return false;
			return true;
		}
	}
}
