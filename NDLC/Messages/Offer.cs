using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using System.Linq;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using System.IO;
using NDLC.TLV;
using System.Diagnostics.CodeAnalysis;

namespace NDLC.Messages
{
	public class Offer : FundingInformation, ITLVObject
	{
		[JsonProperty(Order = -2)]
		public ContractInfo[]? ContractInfo { get; set; }
		[JsonConverter(typeof(OracleInfoJsonConverter))]
		[JsonProperty(Order = -1)]
		public OracleInfo? OracleInfo { get; set; }
		[JsonConverter(typeof(FeeRateJsonConverter))]
		[JsonProperty(Order = 102)]
		public FeeRate? FeeRate { get; set; }
		[JsonProperty(Order = 103)]
		public Timeouts? Timeouts { get; set; }
		[JsonProperty(Order = 104, DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string? EventId { get; set; }
		public uint256? ChainHash { get; set; }

		public DiscretePayoffs ToDiscretePayoffs()
		{
			if (ContractInfo is null || ContractInfo.Length is 0)
				throw new InvalidOperationException("contractInfo is required");
			return base.ToDiscretePayoffs(ContractInfo);
		}

		public byte[] ToTLV()
		{
			var ms = new MemoryStream();
			TLVWriter writer = new TLVWriter(ms);
			WriteTLV(writer);
			return ms.ToArray();
		}

		public const int TLVType = 42778;
		public const int TLVContractInfoType = 42768; // A710
		public const int TLVOracleInfoType = 42770;
		public void WriteTLV(TLVWriter writer)
		{
			if (ChainHash is null)
				throw new InvalidOperationException($"{nameof(ChainHash)} is not set");
			if (ContractInfo is null)
				throw new InvalidOperationException($"{nameof(ContractInfo)} is not set");
			if (OracleInfo is null)
				throw new InvalidOperationException($"{nameof(OracleInfo)} is not set");
			if (TotalCollateral is null)
				throw new InvalidOperationException($"{nameof(TotalCollateral)} is not set");
			if (PubKeys?.FundingKey is null)
				throw new InvalidOperationException($"{nameof(PubKeys.FundingKey)} is not set");
			if (PubKeys?.PayoutAddress is null)
				throw new InvalidOperationException($"{nameof(PubKeys.PayoutAddress)} is not set");
			if (FundingInputs is null)
				throw new InvalidOperationException($"{nameof(FundingInputs)} is not set");
			if (ChangeAddress is null)
				throw new InvalidOperationException($"{nameof(ChangeAddress)} is not set");
			if (FeeRate is null)
				throw new InvalidOperationException($"{nameof(FeeRate)} is not set");
			if (Timeouts is null)
				throw new InvalidOperationException($"{nameof(Timeouts)} is not set");
			writer.WriteU16(TLVType);
			writer.WriteByte(0); // contract_flags
			writer.WriteUInt256(ChainHash);
			using (var ciRecord = writer.StartWriteRecord(TLVContractInfoType))
			{
				foreach (var ci in ContractInfo)
				{

					ciRecord.WriteBytes(ci.Outcome.Hash);
					ciRecord.WriteU64((ulong)ci.Payout.Satoshi);
				}
			}
			Span<byte> buf = stackalloc byte[64];
			using (var oracleRecord = writer.StartWriteRecord(TLVOracleInfoType))
			{
				OracleInfo.WriteToBytes(buf);
				oracleRecord.WriteBytes(buf);
			}
			PubKeys.FundingKey.Compress().ToBytes(buf, out _);
			writer.WriteBytes(buf.Slice(0, 33));
			writer.WriteScript(PubKeys.PayoutAddress.ScriptPubKey);
			writer.WriteU64((ulong)TotalCollateral.Satoshi);
			writer.WriteU16((ushort)FundingInputs.Length);
			foreach (var input in FundingInputs)
			{
				input.WriteTLV(writer);
			}
			writer.WriteScript(ChangeAddress.ScriptPubKey);
			writer.WriteU64((ulong)FeeRate.SatoshiPerByte);
			writer.WriteU32((uint)Timeouts.ContractMaturity);
			writer.WriteU32((uint)Timeouts.ContractTimeout);
		}

		public void ReadTLV(TLVReader reader, Network network)
		{
			if (reader.ReadU16() != TLVType)
				throw new FormatException("Invalid TLV type for offer");
			reader.ReadByte(); // contract_flags
			ChainHash = reader.ReadUInt256();
			if (network.GenesisHash != network.GenesisHash)
				throw new FormatException("Invalid ChainHash");
			Span<byte> buf = stackalloc byte[64];
			using (var ciRecord = reader.StartReadRecord())
			{
				if (ciRecord.Type != TLVContractInfoType)
					throw new FormatException("Invalid TLV type for contract info");
				List<ContractInfo> cis = new List<ContractInfo>();
				while (!ciRecord.IsEnd)
				{
					ciRecord.ReadBytes(buf.Slice(0, 32));
					var sats = ciRecord.ReadU64();
					cis.Add(new Messages.ContractInfo(new DiscreteOutcome(buf.Slice(0, 32).ToArray()), Money.Satoshis(sats)));
				}
				ContractInfo = cis.ToArray();
			}
			using (var oracleRecord = reader.StartReadRecord())
			{
				if (oracleRecord.Type != TLVOracleInfoType)
					throw new FormatException("Invalid TLV type for oracle info");
				oracleRecord.ReadBytes(buf.Slice(0, 64));
				OracleInfo = OracleInfo.Create(buf);
			}
			PubKeys = new PubKeyObject();
			reader.ReadBytes(buf.Slice(0, 33));
			PubKeys.FundingKey = new PubKey(buf.Slice(0, 33).ToArray());
			PubKeys.PayoutAddress = reader.ReadScript().GetDestinationAddress(network);
			if (PubKeys.PayoutAddress is null)
				throw new FormatException("Invalid script");
			TotalCollateral = Money.Satoshis(reader.ReadU64());
			var fiCount = reader.ReadU16();
			List<FundingInput> fis = new List<FundingInput>();
			while (fiCount > 0)
			{
				fis.Add(FundingInput.ParseFromTLV(reader, network));
				fiCount--;
			}
			FundingInputs = fis.ToArray();
			ChangeAddress = reader.ReadScript().GetDestinationAddress(network);
			if (ChangeAddress is null)
				throw new FormatException("Invalid script");
			FeeRate = new FeeRate(Money.Satoshis(reader.ReadU64()), 1);
			Timeouts = new Timeouts()
			{
				ContractMaturity = reader.ReadU32(),
				ContractTimeout = reader.ReadU32()
			};
		}

		public static Offer ParseFromTLV(TLVReader reader, Network network)
		{
			var offer = new Offer();
			offer.ReadTLV(reader, network);
			return offer;
		}
		public static Offer ParseFromTLV(string hexOrBase64, Network network)
		{
			var bytes = HexEncoder.IsWellFormed(hexOrBase64) ? Encoders.Hex.DecodeData(hexOrBase64) : Encoders.Base64.DecodeData(hexOrBase64);
			var reader = new TLVReader(new MemoryStream(bytes));
			var offer = new Offer();
			offer.ReadTLV(reader, network);
			return offer;
		}
		public bool SetContractPreimages(params string[] outcomes)
		{
			return SetContractPreimages(outcomes.Select(c => new DiscreteOutcome(c)).ToArray());
		}
		public bool SetContractPreimages(params DiscreteOutcome[] outcomes)
		{
			if (ContractInfo is null)
				return false;
			var unspecifiedOutcomes = outcomes.ToHashSet();
			for (int i = 0; i < ContractInfo.Length; i++)
			{
				if (!unspecifiedOutcomes.TryGetValue(ContractInfo[i].Outcome, out var outcome) ||
					outcome?.OutcomeString is null)
					return false;
				unspecifiedOutcomes.Remove(outcome);
				ContractInfo[i] = new ContractInfo(outcome, ContractInfo[i].Payout);
			}
			if (unspecifiedOutcomes.Count != 0)
				return false;
			return true;
		}

		public uint256 GetTemporaryContractId()
		{
			return new uint256(Hashes.SHA256(ToTLV()));
		}
	}

	public class OracleInfo
	{
		public static OracleInfo Parse(string str)
		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			if (!TryParse(str, out var t) || t is null)
				throw new FormatException("Invalid oracleInfo");
			return t;
		}
		public static bool TryParse(string str, out OracleInfo? oracleInfo)
		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			var bytes = Encoders.Hex.DecodeData(str);
			return TryCreate(bytes, out oracleInfo);
		}

		private static bool TryCreate(ReadOnlySpan<byte> input64, [MaybeNullWhen(false)] out OracleInfo oracleInfo)
		{
			oracleInfo = null;
			if (input64.Length != 64)
				return false;
			if (!ECXOnlyPubKey.TryCreate(input64.Slice(0, 32), Context.Instance, out var pubkey) || pubkey is null)
				return false;
			if (!SchnorrNonce.TryCreate(input64.Slice(32), out var rValue) || rValue is null)
				return false;
			oracleInfo = new OracleInfo(pubkey, rValue);
			return true;
		}

		public static OracleInfo Create(ReadOnlySpan<byte> input64)
		{
			if (TryCreate(input64, out var oracleInfo))
				return oracleInfo;
			throw new FormatException("Invalid oracle info bytes");
		}
		public OracleInfo(ECXOnlyPubKey pubKey, SchnorrNonce rValue)
		{
			if (pubKey == null)
				throw new ArgumentNullException(nameof(pubKey));
			if (rValue is null)
				throw new ArgumentNullException(nameof(rValue));
			RValue = rValue;
			PubKey = pubKey;
		}
		public SchnorrNonce RValue { get; }
		public ECXOnlyPubKey PubKey { get; }

		public bool TryComputeSigpoint(DiscreteOutcome outcome, out ECPubKey? sigpoint)
		{
			return PubKey.TryComputeSigPoint(outcome.Hash, RValue, out sigpoint);
		}
		public void WriteToBytes(Span<byte> out64)
		{
			PubKey.WriteToSpan(out64);
			RValue.WriteToSpan(out64.Slice(32));
		}

		public override string ToString()
		{
			Span<byte> buf = stackalloc byte[64];
			WriteToBytes(buf);
			return Encoders.Hex.EncodeData(buf);
		}
	}

	public class Timeouts
	{
		[JsonConverter(typeof(LocktimeJsonConverter))]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
		public LockTime ContractMaturity { get; set; }
		[JsonConverter(typeof(LocktimeJsonConverter))]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
		public LockTime ContractTimeout { get; set; }
	}
	public class PubKeyObject
	{
		public PubKey? FundingKey { get; set; }
		public BitcoinAddress? PayoutAddress { get; set; }
	}
	public class FundingInput
	{
		public FundingInput()
		{

		}
		public FundingInput(PSBTInput input)
			:this(input.GetCoin() ?? throw new InvalidOperationException("The PSBT is missing witness_utxo"))
		{
			if (input.Unknown.TryGetValue(FundingInformation.MaxWitnessLengthKey, out var v))
			{
				MaxWitnessLength = (int)Utils.ToUInt32(v, true);
			}
			if (input.RedeemScript is Script)
			{
				RedeemScript = input.RedeemScript;
			}
			if (input.NonWitnessUtxo is Transaction)
			{
				InputTransaction = input.NonWitnessUtxo;
			}
			Index = input.Index;
			Sequence = input.PSBT.GetOriginalTransaction().Inputs[input.Index].Sequence;
			if (MaxWitnessLength is null)
				this.SetRecommendedMaxWitnessLength();
		}
		public FundingInput(Transaction tx, uint index, Sequence sequence)
		{
			InputTransaction = tx;
			Index = index;
			Sequence = sequence;
		}
		public FundingInput(Coin c)
		{
			Outpoint = c.Outpoint;
			Output = c.TxOut;
		}

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public Transaction? InputTransaction { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public uint? Index { get; set; }
		[JsonConverter(typeof(NBitcoin.JsonConverters.OutpointJsonConverter))]
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public OutPoint? Outpoint { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public TxOut? Output { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(NBitcoin.JsonConverters.SequenceJsonConverter))]
		public Sequence? Sequence { get; private set; }
		[JsonConverter(typeof(NBitcoin.JsonConverters.ScriptJsonConverter))]
		public Script? RedeemScript { get; set; }
		public int? MaxWitnessLength { get; set; }
		public Coin AsCoin()
		{
			Coin c;
			if (Outpoint is null || Output is null)
			{
				if (InputTransaction is null || Index is null || Sequence is null)
				{
					throw new InvalidOperationException("Funding input is missing some information");
				}
				c = new Coin(InputTransaction, Index.Value);
			}
			else
			{
				c = new Coin(Outpoint, Output);
			}
			if (RedeemScript is Script)
			{
				if (RedeemScript.Hash.ScriptPubKey != c.ScriptPubKey)
					throw new InvalidOperationException("The redeem script is not matching the scriptPubKey");
				if (!RedeemScript.IsScriptType(ScriptType.P2WSH))
					throw new InvalidOperationException("The redeem script should be P2WSH");
			}
			else if (c.IsMalleable)
			{
				throw new InvalidOperationException("This input is not segwit");
			}
			return c;
		}

		public static FundingInput ParseFromTLV(TLVReader reader, Network network)
		{
			FundingInput input = new FundingInput();
			input.ReadTLV(reader, network);
			return input;
		}

		public void SetRecommendedMaxWitnessLength()
		{
			var coin = AsCoin();
			if (coin.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
			{
				MaxWitnessLength = 108;
			}
			else
				throw new InvalidOperationException("Recommend Max witness length unknown");
		}

		public const int TLVType = 42772;
		public void WriteTLV(TLVWriter writer)
		{
			if (this.Index is null)
				throw new InvalidOperationException($"{nameof(Index)} is not set");
			if (this.Sequence is null)
				throw new InvalidOperationException($"{nameof(Sequence)} is not set");
			if (this.MaxWitnessLength is null)
				throw new InvalidOperationException($"{nameof(MaxWitnessLength)} is not set");
			if (this.InputTransaction is null)
				throw new InvalidOperationException($"{nameof(InputTransaction)} is not set");
			using var record = writer.StartWriteRecord(TLVType);
			var txBytes = this.InputTransaction.ToBytes();
			record.WriteU16((ushort)txBytes.Length);
			record.WriteBytes(txBytes);
			record.WriteU32(this.Index.Value);
			record.WriteU32(this.Sequence.Value);
			record.WriteU16((ushort)MaxWitnessLength.Value); // max_witness_len
			record.WriteScript(RedeemScript); // redeemscript_len
		}
		private void ReadTLV(TLVReader reader, Network network)
		{
			using var record = reader.StartReadRecord();
			if (record.Type != TLVType)
				throw new FormatException("Invalid TLVType for FundingInput");
			var len = record.ReadU16();
			var buf = new byte[len];
			record.ReadBytes(buf);
			InputTransaction = Transaction.Load(buf, network);
			Index = record.ReadU32();
			Sequence = record.ReadU32();
			MaxWitnessLength = record.ReadU16();
			len = record.ReadU16();
			if (len != 0)
			{
				var script = new byte[len];
				record.ReadBytes(script);
				RedeemScript = Script.FromBytesUnsafe(script);
			}
		}
	}
	public class ContractInfo
	{
		public ContractInfo(DiscreteOutcome outcome, Money payout)
		{
			Payout = payout;
			Outcome = outcome;
		}
		public DiscreteOutcome Outcome { get; }
		public Money Payout { get; }
	}
}
