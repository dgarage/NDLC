using NBitcoin;
using Newtonsoft.Json;

namespace NDLC.Messages
{
	public class FundingInformation
	{
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money? TotalCollateral { get; set; }
		public PubKeyObject? PubKeys { get; set; }
		public FundingInput[]? FundingInputs { get; set; }
		public BitcoinAddress? ChangeAddress { get; set; }
	}

}
