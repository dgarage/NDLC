using NBitcoin.DLC.Messages.JsonConverters;
using NBitcoin.Policy;
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
		public string? OracleInfo { get; set; }
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
