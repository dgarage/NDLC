using NBitcoin;
using NBitcoin.Crypto;
using NDLC.Messages;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC
{
	public class DLCTransactionBuilderState
	{
		public class Party
		{
			public PubKey? FundPubKey { get; set; } 
			public Money? Collateral { get; set; }
			public ECDSASignature? RefundSig { get; set; }
			public Dictionary<DLCOutcome, SecpECDSAAdaptorSignature>? OutcomeSigs;
			public Script? Payout;
		}
		public bool IsInitiator { get; set; }
		public Party? Acceptor { get; set; }
		public Party? Offerer { get; set; }

		[JsonIgnore]
		public Party? Remote
		{
			get
			{
				return IsInitiator ? Acceptor : Offerer;
			}
			set
			{
				if (IsInitiator)
					Remote = value;
				else
					Us = value;
			}
		}
		[JsonIgnore]
		public Party? Us
		{
			get
			{
				return IsInitiator ? Offerer : Acceptor;
			}
			set
			{
				if (IsInitiator)
					Us = value;
				else
					Remote = value;
			}
		}

		public Coin[]? OffererCoins { get; set; }
		public Script? OffererChange { get; set; }
		public OracleInfo? OracleInfo { get; set; }
		public FeeRate? FeeRate { get; set; }
		public Timeouts? Timeouts { get; set; }
		public PnLOutcomes? OffererPnLOutcomes { get; set; }
		public FundingPSBT? Funding { get; set; }
	}
}
