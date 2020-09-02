using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace NDLC.Messages.JsonConverters
{
	public class FeeRateJsonConverter : JsonConverter<FeeRate>
	{
		public override FeeRate ReadJson(JsonReader reader, Type objectType, [AllowNull] FeeRate existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType != JsonToken.Integer)
				throw new FormatException("Unexpected json token for feerate (expected integer)");
			return new FeeRate(Money.Satoshis((long)reader.Value!), 1);
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] FeeRate value, JsonSerializer serializer)
		{
			if (value is FeeRate)
				writer.WriteValue((long)value.SatoshiPerByte);
		}
	}
}
