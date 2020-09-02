using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC
{
	public class Constants
	{
		public static Money PayoutAmount = Money.Satoshis(1111m);
		public static Script FundingPlaceholder = BitcoinAddress.Create("1DLCFundingAddressxxxxxxxxy2BvHew", Network.Main).ScriptPubKey;
	}
}
