using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
			public Dictionary<OutPoint, List<PartialSignature>>? FundingSigs;
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

		Coin[]? OffererCoins;
		Script? OffererChange;

		OracleInfo? OracleInfo;
		FeeRate? FeeRate;
		Timeouts? Timeouts;
		Dictionary<DLCOutcome, Money>? OffererRewards;
		List<(DLCOutcome Outcome, Money Value)>? OffererRewardsList;
		public FundingPSBT? Funding;

		public Transaction? FundingOverride { get; set; }

		public DLCTransactionBuilder(bool isInitiator, Offer? offer, Accept? accept, Sign? sign, Network network)
			: this(isInitiator, offer, accept, sign, null, network)
		{

		}
		public DLCTransactionBuilder(bool isInitiator, Offer? offer, Accept? accept, Sign? sign, Transaction? transactionOverride, Network network)
		{
			this.isInitiator = isInitiator;
			FillStateFrom(offer);
			FillStateFrom(accept);
			FillStateFrom(sign);
			this.network = network;

			if (Offerer?.Collateral is null ||
				Offerer?.FundPubKey is null ||
				Acceptor?.Collateral is null ||
				Acceptor?.FundPubKey is null ||
				FeeRate is null)
				return;
			var offerer = new FundingParty(
				Offerer.Collateral,
				GetCoins(offer),
				offer?.ChangeAddress?.ScriptPubKey,
				Offerer.FundPubKey);
			var acceptor = new FundingParty(
				Acceptor.Collateral,
				GetCoins(accept),
				accept?.ChangeAddress?.ScriptPubKey,
				Acceptor.FundPubKey);
			Funding = new FundingParameters(offerer, acceptor, FeeRate, transactionOverride).Build(network);
		}

		public void FillStateFrom(Offer? offer)
		{
			if (offer is null)
				return;
			if (isInitiator)
			{
				OffererChange = offer.ChangeAddress?.ScriptPubKey;
				OffererCoins = GetCoins(offer);
			}
			OracleInfo = offer.OracleInfo;
			Timeouts = offer.Timeouts;
			Offerer ??= new Party();
			OffererRewards = new Dictionary<DLCOutcome, Money>();
			OffererRewardsList = new List<(DLCOutcome Outcomes, Money Value)>();
			if (offer.ContractInfo is ContractInfo[] ci)
			{
				foreach (var i in ci)
				{
					if (i.Outcome is DLCOutcome && i.Sats is Money)
					{
						OffererRewards.Add(i.Outcome, i.Sats);
						OffererRewardsList.Add((i.Outcome, i.Sats));
					}
				}
			}

			Offerer.Collateral = offer.TotalCollateral;
			Offerer.FundPubKey = offer.PubKeys?.FundingKey;
			Offerer.Payout = offer.PubKeys?.PayoutAddress?.ScriptPubKey;
			FeeRate = offer.FeeRate;
		}
		static Coin[] GetCoins(FundingInformation? fi)
		{
			if (fi?.FundingInputs is null)
				return Array.Empty<Coin>();
			return fi.FundingInputs.Select(c => new Coin(c.Outpoint, c.Output)).ToArray();
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
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = Timeouts.ContractTimeout;
			tx.Inputs.Add(new OutPoint(Funding.PSBT.GetGlobalTransaction().GetHash(), 0), sequence: 0xFFFFFFFE);
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
			offer.FillFromTemplateFunding(fundingTemplate, fundingKey.PubKey, network);
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
			if (offer.PubKeys?.FundingKey is null ||
				offer.TotalCollateral is null ||
				offer.FeeRate is null)
				throw new InvalidOperationException("Offer is missing some informations");
			this.FillStateFrom(offer);
			this.FundingKey = this.FundingKey ?? new Key();
			Accept accept = new Accept();
			accept.FillFromTemplateFunding(fundingTemplate, FundingKey.PubKey, network);
			FillStateFrom(accept);

			var offerer = new FundingParty(
				offer.TotalCollateral,
				offer.FundingInputs.Select(c => new Coin(c.Outpoint, c.Output)).ToArray(),
				offer.ChangeAddress?.ScriptPubKey,
				offer.PubKeys.FundingKey);
			var acceptor = new FundingParty(
				fundingTemplate.Collateral,
				fundingTemplate.FundingCoins.ToArray(),
				fundingTemplate.Change,
				this.FundingKey.PubKey
				);
			Funding = new FundingParameters(offerer, acceptor, offer.FeeRate, FundingOverride).Build(network);
			accept.CetSigs = CreateCetSigs();
			accept.EventId = offer.EventId;
			return accept;
		}

		public void Sign1(Accept accept)
		{
			if (!isInitiator)
				throw new InvalidOperationException("The acceptor can't sign");
			if (accept.TotalCollateral is null ||
				accept.PubKeys?.FundingKey is null)
				throw new InvalidOperationException("The accept message is missing some information");
			if (Offerer?.Collateral is null || Offerer?.FundPubKey is null || FeeRate is null)
				throw new InvalidOperationException("Invalid state");
			FillStateFrom(accept);
			var acceptor = new FundingParty(
			accept.TotalCollateral,
			accept.FundingInputs.Select(c => new Coin(c.Outpoint, c.Output)).ToArray(),
			accept.ChangeAddress?.ScriptPubKey,
			accept.PubKeys.FundingKey);
			var offerer = new FundingParty(
				Offerer.Collateral,
				OffererCoins,
				OffererChange,
				Offerer.FundPubKey
				);
			Funding = new FundingParameters(offerer, acceptor, FeeRate, FundingOverride).Build(network);
			OffererCoins = null;
			OffererChange = null;
			AssertRemoteSigs();
		}
		public Sign Sign2(PSBT signedFunding)
		{
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			Funding.PSBT = Funding.PSBT.Combine(signedFunding);
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

		public void Finalize1(Sign sign)
		{
			if (sign == null)
				throw new ArgumentNullException(nameof(sign));
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			FillStateFrom(sign);
			AssertRemoteSigs();
			AddFundingSigs(Remote, Funding.PSBT);
		}

		public Transaction Finalize(PSBT signedFunding)
		{
			if (signedFunding == null)
				throw new ArgumentNullException(nameof(signedFunding));
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			Funding.PSBT = Funding.PSBT.Combine(signedFunding);
			Funding.PSBT.AssertSanity();
			// This check if sigs are good!
			Funding.PSBT = Funding.PSBT.Finalize();
			var tx = Funding.PSBT.ExtractTransaction();
			AssertSegwit(tx);
			return tx;
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
			if (FundingKey is null || OffererRewards is null || Funding is null)
				throw new InvalidOperationException("Invalid state for creating CetSigs");
			var refund = BuildRefund();
			var signature = refund.SignInput(FundingKey, Funding.FundingCoin);
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
			if (OracleInfo is null || Funding is null)
				throw new InvalidOperationException("Invalid state for signing CET");
			var cet = BuildCET(outcome);
			var hash = cet.GetSignatureHash(Funding.FundingCoin);
			if (!OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
				throw new InvalidOperationException("TryComputeSigpoint failed");
			if (!key.ToECPrivKey().TrySignAdaptor(hash.ToBytes(), sigpoint, out var sig, out var proof) || sig  is null || proof is null)
				throw new InvalidOperationException("TrySignAdaptor failed");
			return new AdaptorSignature(sig, proof);
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

		public Transaction BuildSignedCET(Key oracleSecret)
		{
			if (OffererRewards is null ||
				Remote?.OutcomeSigs is null ||
				Remote?.FundPubKey is null ||
				this.FundingKey is null ||
				OracleInfo is null ||
				Funding is null)
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
				builder.AddCoins(Funding.FundingCoin);
				builder.AddKnownSignature(Remote.FundPubKey, new TransactionSignature(ecdsaSig.ToDER(), SigHash.All), Funding.FundingCoin.Outpoint);
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
				OffererRewards is null ||
				Funding is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (!OffererRewards.TryGetValue(outcome, out var offererPayout) || offererPayout is null)
				throw new InvalidOperationException("Invalid outcome");
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = Timeouts.ContractMaturity;
			tx.Inputs.Add(Funding.FundingCoin.Outpoint, sequence: 0xFFFFFFFE);
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
			if (Remote?.FundPubKey is null || Remote?.OutcomeSigs is null || OracleInfo is null || Funding is null)
				throw new InvalidOperationException("We did not received enough data to verify the sigs");

			foreach (var outcome in OffererRewards.Select(o => o.Key))
			{
				if (!Remote.OutcomeSigs.TryGetValue(outcome, out var outcomeSig))
					return false;

				if (!OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
					return false;
				var ecPubKey = Remote.FundPubKey.ToECPubKey();
				var msg = BuildCET(outcome).GetSignatureHash(Funding.FundingCoin).ToBytes();
				if (!ecPubKey.SigVerify(outcomeSig.Signature, outcomeSig.Proof, msg, sigpoint))
					return false;
			}
			return true;
		}
		public bool VerifyRemoteRefundSignature()
		{
			if (Remote?.RefundSig is null || Remote?.FundPubKey is null || Funding is null)
				throw new InvalidOperationException("We did not received enough data to verify refund signature");
			var refund = BuildRefund();
			if (!Remote.FundPubKey.Verify(refund.GetSignatureHash(Funding.FundingCoin), Remote.RefundSig))
				return false;
			return true;
		}

		public Transaction GetFundingTransaction()
		{
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			return Funding.PSBT.GetGlobalTransaction();
		}
		public PSBT GetFundingPSBT()
		{
			if (Funding is null)
				throw new InvalidOperationException("Invalid state");
			return Funding.PSBT;
		}
	}
}
