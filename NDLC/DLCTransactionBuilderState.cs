using NBitcoin;
using NBitcoin.Crypto;
using NDLC.Messages;
using NDLC.Messages.JsonConverters;
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
			public VSizes? VSizes { get; set; }

			public SecpECDSAAdaptorSignature[]? OutcomeSigs;
			public Script? PayoutDestination;
		}
		public bool IsInitiator { get; set; }
		public uint256? ContractId { get; set; }
		public bool TemporaryContractId { get; set; } = true;
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
					Acceptor = value;
				else
					Offerer = value;
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
					Offerer = value;
				else
					Acceptor = value;
			}
		}
		public FundingInput[]? OffererInputs { get; set; }
		public Script? OffererChange { get; set; }
		public OracleInfo? OracleInfo { get; set; }
		public FeeRate? FeeRate { get; set; }
		public Timeouts? Timeouts { get; set; }
		public DiscretePayoffs? OffererPayoffs { get; set; }
		public FundingPSBT? Funding { get; set; }
	}
}
