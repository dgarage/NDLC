using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using static NDLC.DLCTransactionBuilderState;

namespace NDLC.Messages
{
	public class DLCTransactionBuilder
	{
		private readonly Network network;
		DLCTransactionBuilderState s = new DLCTransactionBuilderState();
		public Transaction? FundingOverride { get; set; }


		public DLCTransactionBuilder(string state, Network network)
		{
			this.network = network;
			ImportState(state);
		}

		public DLCTransactionBuilder(bool isInitiator, Offer? offer, Accept? accept, Sign? sign, Network network)
			: this(isInitiator, offer, accept, sign, null, network)
		{

		}
		public DLCTransactionBuilder(bool isInitiator, Offer? offer, Accept? accept, Sign? sign, Transaction? transactionOverride, Network network)
		{
			this.s.IsInitiator = isInitiator;
			FillStateFrom(offer);
			FillStateFrom(accept);
			FillStateFrom(sign);
			this.network = network;

			if (s.Offerer?.Collateral is null ||
				s.Offerer?.FundPubKey is null ||
				s.Acceptor?.Collateral is null ||
				s.Acceptor?.FundPubKey is null ||
				s.FeeRate is null)
				return;
			var offerer = new FundingParty(
				s.Offerer.Collateral,
				GetCoins(offer),
				offer?.ChangeAddress?.ScriptPubKey,
				s.Offerer.FundPubKey);
			var acceptor = new FundingParty(
				s.Acceptor.Collateral,
				GetCoins(accept),
				accept?.ChangeAddress?.ScriptPubKey,
				s.Acceptor.FundPubKey);
			s.Funding = new FundingParameters(offerer, acceptor, s.FeeRate, transactionOverride).Build(network);
		}

		public void FillStateFrom(Offer? offer)
		{
			if (offer is null)
				return;
			if (s.IsInitiator)
			{
				s.OffererChange = offer.ChangeAddress?.ScriptPubKey;
				s.OffererCoins = GetCoins(offer);
			}
			s.OracleInfo = offer.OracleInfo;
			s.Timeouts = offer.Timeouts;
			s.Offerer ??= new Party();

			if (offer.ContractInfo is ContractInfo[] c &&
				c.Length > 0 &&
				offer.TotalCollateral is Money)
			{
				s.OffererPayoffs = DiscretePayoffs.CreateFromContractInfo(offer.ContractInfo, offer.TotalCollateral);
			}
			s.Offerer.Collateral = offer.TotalCollateral;
			s.Offerer.FundPubKey = offer.PubKeys?.FundingKey;
			s.Offerer.PayoutDestination = offer.PubKeys?.PayoutAddress?.ScriptPubKey;
			s.FeeRate = offer.FeeRate;
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
			s.Acceptor ??= new Party();
			s.Acceptor.FundPubKey = accept.PubKeys?.FundingKey;
			s.Acceptor.OutcomeSigs = accept.CetSigs?.OutcomeSigs.ToDictionary(kv => kv.Key, kv => kv.Value.Signature);
			s.Acceptor.RefundSig = accept.CetSigs?.RefundSig?.Signature.Signature;
			s.Acceptor.Collateral = accept.TotalCollateral;
			s.Acceptor.PayoutDestination = accept.PubKeys?.PayoutAddress?.ScriptPubKey;
		}

		public void FillStateFrom(Sign? sign)
		{
			if (sign is null)
				return;
			s.Offerer ??= new Party();
			s.Offerer.OutcomeSigs = sign.CetSigs?.OutcomeSigs.ToDictionary(kv => kv.Key, kv => kv.Value.Signature);
			s.Offerer.RefundSig = sign.CetSigs?.RefundSig?.Signature.Signature;
		}

		public Transaction BuildRefund()
		{
			if (s.Timeouts is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (s.Offerer?.Collateral is null || s.Acceptor?.Collateral is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (s.Offerer?.PayoutDestination is null || s.Acceptor?.PayoutDestination is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = s.Timeouts.ContractTimeout;
			tx.Inputs.Add(new OutPoint(s.Funding.PSBT.GetGlobalTransaction().GetHash(), 0), sequence: 0xFFFFFFFE);
			tx.Outputs.Add(s.Offerer.Collateral, s.Offerer.PayoutDestination);
			tx.Outputs.Add(s.Acceptor.Collateral, s.Acceptor.PayoutDestination);
			return tx;
		}

		public Offer Offer(PSBTFundingTemplate fundingTemplate, OracleInfo oracleInfo, DiscretePayoffs offererPnLs, Timeouts timeouts)
		{
			using var tx = StartTransaction();
			if (!s.IsInitiator)
				throw new InvalidOperationException("The acceptor can't initiate an offer");
			var fundingKey = GetOrCreateFundingKey();
			Offer offer = new Offer()
			{
				OracleInfo = oracleInfo,
				TotalCollateral = offererPnLs.CalculateCollateral(),
				ContractInfo = offererPnLs.ToContractInfo(),
				Timeouts = timeouts
			};
			offer.FillFromTemplateFunding(fundingTemplate, fundingKey.PubKey, network);
			this.s.OracleInfo = oracleInfo;
			FillStateFrom(offer);
			tx.Commit();
			return offer;
		}

		private Key GetOrCreateFundingKey()
		{
			if (s.FundKey is Key k)
				return k;
			k = new Key();
			UseFundKey(k);
			return k;
		}

		public Accept Accept(Offer offer,
							PSBTFundingTemplate fundingTemplate)
		{
			using var tx = StartTransaction();
			if (s.IsInitiator)
				throw new InvalidOperationException("The initiator can't accept");
			if (fundingTemplate == null)
				throw new ArgumentNullException(nameof(fundingTemplate));
			if (offer.PubKeys?.FundingKey is null ||
				offer.TotalCollateral is null ||
				offer.FeeRate is null)
				throw new InvalidOperationException("Offer is missing some informations");
			this.FillStateFrom(offer);
			var fundKey = GetOrCreateFundingKey();
			Accept accept = new Accept();
			accept.FillFromTemplateFunding(fundingTemplate, fundKey.PubKey, network);
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
				fundKey.PubKey
				);
			s.Funding = new FundingParameters(offerer, acceptor, offer.FeeRate, FundingOverride).Build(network);
			accept.CetSigs = CreateCetSigs();
			accept.EventId = offer.EventId;
			tx.Commit();
			return accept;
		}
		public bool AllowUnexpectedCollateral { get; set; }
		public void Sign1(Accept accept)
		{
			using var tx = StartTransaction();
			if (!s.IsInitiator)
				throw new InvalidOperationException("The acceptor can't sign");
			if (accept.PubKeys?.FundingKey is null)
				throw new InvalidOperationException("The accept message is missing some information");
			if (s.Offerer?.Collateral is null || s.OffererPayoffs is null || s.Offerer?.FundPubKey is null || s.FeeRate is null)
				throw new InvalidOperationException("Invalid state");
			if (accept?.CetSigs?.OutcomeSigs is null)
				throw new InvalidOperationException("Outcome sigs missing");
			FillStateFrom(accept);

			var collateral = accept.TotalCollateral;
			var expectedCollateral = s.OffererPayoffs.Inverse().CalculateCollateral();
			if (collateral is null)
			{
				collateral = expectedCollateral;
			}
			else if (!AllowUnexpectedCollateral && collateral != expectedCollateral)
			{
				throw new InvalidOperationException("Unexpected collateral");
			}
			var acceptor = new FundingParty(
			collateral,
			accept.FundingInputs.Select(c => new Coin(c.Outpoint, c.Output)).ToArray(),
			accept.ChangeAddress?.ScriptPubKey,
			accept.PubKeys.FundingKey);
			var offerer = new FundingParty(
				s.Offerer.Collateral,
				s.OffererCoins,
				s.OffererChange,
				s.Offerer.FundPubKey
				);
			s.Funding = new FundingParameters(offerer, acceptor, s.FeeRate, FundingOverride).Build(network);
			AssertRemoteSigs(accept.CetSigs.OutcomeSigs);
			s.OffererCoins = null;
			s.OffererChange = null;
			tx.Commit();
		}
		public Sign Sign2(PSBT signedFunding)
		{
			using var tx = StartTransaction();
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			s.Funding.PSBT = s.Funding.PSBT.Combine(signedFunding);
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
			tx.Commit();
			return sign;
		}

		private void AssertRemoteSigs(Dictionary<DiscreteOutcome, AdaptorSignature> cetSigs)
		{
			if (!VerifyRemoteCetSigs(cetSigs))
				throw new InvalidOperationException("Invalid remote CET");
			if (!VerifyRemoteRefundSignature())
				throw new InvalidOperationException("Invalid remote refund signature");
		}

		public void Finalize1(Sign sign)
		{
			using var tx = StartTransaction();
			if (sign == null)
				throw new ArgumentNullException(nameof(sign));
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			if (sign?.CetSigs?.OutcomeSigs is null)
				throw new InvalidOperationException("Outcome sigs missing");
			FillStateFrom(sign);
			AssertRemoteSigs(sign.CetSigs.OutcomeSigs);
			if (sign.FundingSigs is Dictionary<OutPoint, List<PartialSignature>> sigs1)
			{
				foreach (var kv in sigs1)
				{
					var input = s.Funding.PSBT.Inputs.FindIndexedInput(kv.Key);
					foreach (var sig in kv.Value)
					{
						input.PartialSigs.Add(sig.PubKey, sig.Signature);
					}
				}
			}
			tx.Commit();
		}

		public Transaction Finalize(PSBT signedFunding)
		{
			using var txx = StartTransaction();
			if (signedFunding == null)
				throw new ArgumentNullException(nameof(signedFunding));
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			s.Funding.PSBT = s.Funding.PSBT.Combine(signedFunding);
			s.Funding.PSBT.AssertSanity();
			// This check if sigs are good!
			s.Funding.PSBT = s.Funding.PSBT.Finalize();
			var tx = s.Funding.PSBT.ExtractTransaction();
			AssertSegwit(tx);
			txx.Commit();
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
			if (s.FundKey is null || s.OffererPayoffs is null || s.Funding is null)
				throw new InvalidOperationException("Invalid state for creating CetSigs");
			var refund = BuildRefund();
			var signature = refund.SignInput(s.FundKey, s.Funding.FundCoin);
			var cetSig = new CetSigs()
			{
				OutcomeSigs = s.OffererPayoffs
							  .Select(o => (o.Outcome, SignCET(s.FundKey, o.Outcome)))
							  .ToDictionary(kv => kv.Outcome, kv => kv.Item2),
				RefundSig = new PartialSignature(s.FundKey.PubKey, signature)
			};
			return cetSig;
		}

		private AdaptorSignature SignCET(Key key, DiscreteOutcome outcome)
		{
			if (s.OracleInfo is null || s.Funding is null)
				throw new InvalidOperationException("Invalid state for signing CET");
			var cet = BuildCET(outcome);
			var hash = cet.GetSignatureHash(s.Funding.FundCoin);
			if (!s.OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
				throw new InvalidOperationException("TryComputeSigpoint failed");
			if (!key.ToECPrivKey().TrySignAdaptor(hash.ToBytes(), sigpoint, out var sig, out var proof) || sig  is null || proof is null)
				throw new InvalidOperationException("TrySignAdaptor failed");
			return new AdaptorSignature(sig, proof);
		}

		public Transaction BuildSignedCET(Key oracleSecret)
		{
			if (s.OffererPayoffs is null ||
				s.Remote?.OutcomeSigs is null ||
				s.Remote?.FundPubKey is null ||
				s.FundKey is null ||
				s.OracleInfo is null ||
				s.Funding is null)
				throw new InvalidOperationException("Invalid state for building the signed CET");
			foreach (var outcome in s.OffererPayoffs.Select(o => o.Outcome))
			{
				if (!s.OracleInfo.TryComputeSigpoint(outcome, out var sigPoint) || sigPoint is null)
					continue;
				if (oracleSecret.PubKey.ToECPubKey() != sigPoint)
					continue;
				var cet = BuildCET(outcome);
				if (!s.Remote.OutcomeSigs.TryGetValue(outcome, out var encryptedSig))
					continue;
				var ecdsaSig = encryptedSig.AdaptECDSA(oracleSecret.ToECPrivKey());
				var builder = network.CreateTransactionBuilder();
				builder.AddCoins(s.Funding.FundCoin);
				builder.AddKnownSignature(s.Remote.FundPubKey, new TransactionSignature(ecdsaSig.ToDER(), SigHash.All), s.Funding.FundCoin.Outpoint);
				builder.AddKeys(s.FundKey);
				builder.SignTransactionInPlace(cet);
				if (!builder.Verify(cet, out var err))
					throw new InvalidOperationException("This CET is not fully signed");
				return cet;
			}
			throw new InvalidOperationException("This oracle key is not valid");
		}

		private Script GetFundingScript()
		{
			if (s.Offerer?.FundPubKey is null || s.Acceptor?.FundPubKey is null)
				throw new InvalidOperationException("We did not received enough data to create the funding script");
			return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, s.Offerer.FundPubKey, s.Acceptor.FundPubKey);
		}

		public Transaction BuildCET(DiscreteOutcome outcome)
		{
			if (s.Timeouts is null ||
				s.Offerer?.Collateral is null ||
				s.Acceptor?.Collateral is null ||
				s.Offerer?.PayoutDestination is null ||
				s.Acceptor?.PayoutDestination is null ||
				s.Timeouts is null ||
				s.OffererPayoffs is null ||
				s.Funding is null)
				throw new InvalidOperationException("We did not received enough data to create the refund");
			if (!s.OffererPayoffs.TryGetValue(outcome, out var offererReward) || offererReward is null)
				throw new InvalidOperationException("Invalid outcome");
			Transaction tx = network.CreateTransaction();
			tx.Version = 2;
			tx.LockTime = s.Timeouts.ContractMaturity;
			tx.Inputs.Add(s.Funding.FundCoin.Outpoint, sequence: 0xFFFFFFFE);
			var collateral = s.Offerer.Collateral + s.Acceptor.Collateral;
			tx.Outputs.Add(offererReward + s.Offerer.Collateral, s.Offerer.PayoutDestination);
			tx.Outputs.Add(s.Acceptor.Collateral - offererReward, s.Acceptor.PayoutDestination);
			foreach (var output in tx.Outputs.ToArray())
			{
				if (output.Value < Money.Satoshis(1000))
					tx.Outputs.Remove(output);
			}
			return tx;
		}

		public bool VerifyRemoteCetSigs(Dictionary<DiscreteOutcome, AdaptorSignature> cetSigs)
		{
			if (s.Remote?.FundPubKey is null || s.OracleInfo is null || s.Funding is null)
				throw new InvalidOperationException("We did not received enough data to verify the sigs");

			foreach (var outcome in s.OffererPayoffs.Select(o => o.Outcome))
			{
				if (!cetSigs.TryGetValue(outcome, out var outcomeSig))
					return false;

				if (!s.OracleInfo.TryComputeSigpoint(outcome, out var sigpoint) || sigpoint is null)
					return false;
				var ecPubKey = s.Remote.FundPubKey.ToECPubKey();
				var msg = BuildCET(outcome).GetSignatureHash(s.Funding.FundCoin).ToBytes();
				if (!ecPubKey.SigVerify(outcomeSig.Signature, outcomeSig.Proof, msg, sigpoint))
					return false;
			}
			return true;
		}
		public bool VerifyRemoteRefundSignature()
		{
			if (s.Remote?.RefundSig is null || s.Remote?.FundPubKey is null || s.Funding is null)
				throw new InvalidOperationException("We did not received enough data to verify refund signature");
			var refund = BuildRefund();
			if (!s.Remote.FundPubKey.Verify(refund.GetSignatureHash(s.Funding.FundCoin), s.Remote.RefundSig))
				return false;
			return true;
		}

		public Transaction GetFundingTransaction()
		{
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			return s.Funding.PSBT.GetGlobalTransaction();
		}
		public PSBT GetFundingPSBT()
		{
			if (s.Funding is null)
				throw new InvalidOperationException("Invalid state");
			return s.Funding.PSBT;
		}

		public void UseFundKey(Key myKey)
		{
			s.FundKey = myKey;
			s.Us ??= new Party();
			s.Us.FundPubKey = myKey.PubKey;
		}

		public string ExportState()
		{
			return JsonConvert.SerializeObject(s, SerializerSettings);
		}

		JsonSerializerSettings? _SerializerSettings;
		JsonSerializerSettings SerializerSettings
		{
			get
			{
				if (_SerializerSettings is null)
				{
					var settings = new JsonSerializerSettings();
					Messages.Serializer.Configure(settings, network);
					settings.Converters.Add(new DLCOutcomeJsonConverter());
					_SerializerSettings = settings;
				}
				return _SerializerSettings;
			}
		}

		void ImportState(string state)
		{
			s = JsonConvert.DeserializeObject<DLCTransactionBuilderState>(state, SerializerSettings) ?? throw new InvalidOperationException("Error, this should never happen");
		}

		StateTransaction StartTransaction()
		{
			return new StateTransaction(this);
		}
		class StateTransaction : IDisposable
		{
			string original;
			public StateTransaction(DLCTransactionBuilder builder)
			{
				Builder = builder;
				original = builder.ExportState();
			}

			public DLCTransactionBuilder Builder { get; }

			bool committed;
			public void Commit()
			{
				committed = true;
			}
			public void Dispose()
			{
				if (!committed)
				{
					Builder.ImportState(original);
				}
			}
		}
	}
}
