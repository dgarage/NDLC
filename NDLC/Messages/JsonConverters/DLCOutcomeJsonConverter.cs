using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class DLCOutcomeJsonConverter : JsonConverter<DLCOutcome>
	{
		public override DLCOutcome ReadJson(JsonReader reader, Type objectType, [AllowNull] DLCOutcome existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType is JsonToken.Null)
				return null!;
			if (reader.TokenType != JsonToken.String)
				throw new JsonObjectException("Unexpected token type for DLCOutcome", reader);
			try
			{
				var bytes = Encoders.Hex.DecodeData((string)reader.Value!);
				return new DLCOutcome(bytes);
			}
			catch
			{
				throw new JsonObjectException("Invalid DLCOutcome", reader);
			}
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] DLCOutcome value, JsonSerializer serializer)
		{
			if (value is DLCOutcome)
				writer.WriteValue(Encoders.Hex.EncodeData(value.Hash));
		}
	}
}
