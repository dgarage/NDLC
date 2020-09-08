using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class BTCSatsJsonConverter : JsonConverter<Money>
	{
		class BTCSats
		{
			public BTCSats(Money money)
			{
				Sats = money.Satoshi;
				BTC = money.ToString(false, false);
			}
			[JsonProperty("sats")]
			public long Sats { get; set; }
			[JsonProperty("BTC")]
			public string BTC { get; set; }
		}
		public override Money ReadJson(JsonReader reader, Type objectType, [AllowNull] Money existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Money value, JsonSerializer serializer)
		{
			if (value is Money)
			{
				serializer.Serialize(writer, new BTCSats(value));
			}
		}
	}
}
