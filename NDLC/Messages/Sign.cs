using NBitcoin;
using NDLC.Messages.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Messages
{
	public class Sign
	{
		public CetSigs? CetSigs { get; set; }

		[JsonConverter(typeof(FundingSigsJsonConverter))]
		public Dictionary<OutPoint, List<PartialSignature>>? FundingSigs { get; set; }

		[JsonExtensionData]
		public Dictionary<string,JToken>? AdditionalData { get; set; }
	}
}
