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
			if (reader.TokenType != JsonToken.String)
				throw new FormatException("Expected string for dlc outcome");
			if (!DLCOutcome.TryParse((string)reader.Value!, out var o) || o is null)
				throw new FormatException("Invalid dlc outcome");
			return o;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] DLCOutcome value, JsonSerializer serializer)
		{
			if (value is DLCOutcome)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
