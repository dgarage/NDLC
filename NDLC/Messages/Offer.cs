using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using NBitcoin.Policy;
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

namespace NDLC.Messages
{
	public class Offer : FundingInformation
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

		public override void FillFromTemplateFunding(PSBTFundingTemplate fundingTemplate, PubKey fundingKey)
		{
			FeeRate = fundingTemplate.FeeRate;
			base.FillFromTemplateFunding(fundingTemplate, fundingKey);
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
			oracleInfo = null;
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			var bytes = Encoders.Hex.DecodeData(str);
			if (bytes.Length != 64)
				return false;
			if (!ECXOnlyPubKey.TryCreate(bytes.AsSpan().Slice(0, 32), Context.Instance, out var pubkey) || pubkey is null)
				return false;
			if (!SchnorrNonce.TryCreate(bytes.AsSpan().Slice(32), out var rValue) || rValue is null)
				return false;
			oracleInfo = new OracleInfo(pubkey, rValue);
			return true;
		}
		public OracleInfo(ECXOnlyPubKey pubKey, SchnorrNonce rValue)
		{
			if (pubKey == null)
				throw new ArgumentNullException(nameof(pubKey));
			if (rValue == null)
				throw new ArgumentNullException(nameof(rValue));
			RValue = rValue;
			PubKey = pubKey;
		}
		public SchnorrNonce RValue { get; }
		public ECXOnlyPubKey PubKey { get; }

		public bool TryComputeSigpoint(DLCOutcome outcome, out ECPubKey? sigpoint)
		{
			return PubKey.TryComputeSigPoint(outcome.Hash, RValue, out sigpoint);
		}
		public void WriteToBytes(Span<byte> out64)
		{
			PubKey.WriteXToSpan(out64);
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
		public LockTime ContractMaturity { get; set; }
		[JsonConverter(typeof(LocktimeJsonConverter))]
		public LockTime ContractTimeout { get; set; }

		public void WriteToBytes(Span<byte> out8)
		{
			throw new NotImplementedException();
		}
	}
	public class PubKeyObject
	{
		public PubKey? FundingKey { get; set; }
		public BitcoinAddress? PayoutAddress { get; set; }
	}
	public class FundingInput
	{
		[JsonConverter(typeof(NBitcoin.JsonConverters.OutpointJsonConverter))]
		public OutPoint Outpoint { get; set; } = OutPoint.Zero;
		public TxOut Output { get; set; } = new TxOut();
	}
	public class ContractInfo
	{
		[JsonProperty("sha256")]
		[JsonConverter(typeof(DLCOutcomeJsonConverter))]
		public DLCOutcome? Outcome { get; set; }
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money? Sats { get; set; }

		public static ContractInfo[] CreateContract(params (DLCOutcome outcome, Money payout)[] rewards)
		{
			List<ContractInfo> info = new List<ContractInfo>();
			foreach (var r in rewards)
			{
				info.Add(new ContractInfo()
				{
					Outcome = r.outcome,
					Sats = r.payout
				});
			}
			return info.ToArray();
		}
	}
}
