using NBitcoin.DLC.Messages.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.DLC.Messages
{
	public class Sign
	{
		public CetSigs? CetSigs { get; set; }

		[JsonConverter(typeof(FundingSigsJsonConverter))]
		public Dictionary<OutPoint, Script[]>? FundingSigs { get; set; }

		[JsonExtensionData]
		public Dictionary<string,JToken>? AdditionalData { get; set; }
	}
}
