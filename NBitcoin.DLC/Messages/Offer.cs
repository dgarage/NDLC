using NBitcoin.DataEncoders;
using NBitcoin.DLC.Messages.JsonConverters;
using NBitcoin.Policy;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;

namespace NBitcoin.DLC.Messages
{
	public class Offer
	{
		public ContractInfo[]? ContractInfo { get; set; }
		[JsonConverter(typeof(OracleInfoJsonConverter))]
		public OracleInfo? OracleInfo { get; set; }
		public PubKeyObject? PubKeys { get; set; }
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money? TotalCollateral { get; set; }
		public FundingInput[]? FundingInputs { get; set; }
		public BitcoinAddress? ChangeAddress { get; set; }
		[JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate? FeeRate { get; set; }
		public Timeouts? Timeouts { get; set; }
		[JsonExtensionData]
		public Dictionary<string, JToken>? AdditionalData { get; set; }
	}

	public class OracleInfo
	{
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
			var rValue = new uint256(bytes.AsSpan().Slice(32));
			oracleInfo = new OracleInfo(pubkey, rValue);
			return true;
		}
		public OracleInfo(ECXOnlyPubKey pubKey, uint256 rValue)
		{
			if (pubKey == null)
				throw new ArgumentNullException(nameof(pubKey));
			if (rValue == null)
				throw new ArgumentNullException(nameof(rValue));
			RValue = rValue;
			PubKey = pubKey;
		}
		public uint256 RValue { get; }
		public ECXOnlyPubKey PubKey { get; }

		public void WriteToBytes(Span<byte> out64)
		{
			PubKey.WriteXToSpan(out64);
			RValue.ToBytes(out64.Slice(32));
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
		[JsonConverter(typeof(NBitcoin.JsonConverters.UInt256JsonConverter))]
		public uint256 SHA256 { get; set; } = uint256.Zero;
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money Sats { get; set; } = Money.Zero;
	}
}
