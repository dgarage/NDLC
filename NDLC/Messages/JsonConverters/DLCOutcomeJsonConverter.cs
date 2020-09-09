using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class DLCOutcomeJsonConverter : JsonConverter<DiscreteOutcome>
	{
		public override DiscreteOutcome ReadJson(JsonReader reader, Type objectType, [AllowNull] DiscreteOutcome existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType != JsonToken.String)
				throw new FormatException("Expected string for dlc outcome");
			if (!DiscreteOutcome.TryParse((string)reader.Value!, out var o) || o is null)
				throw new FormatException("Invalid dlc outcome");
			return o;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] DiscreteOutcome value, JsonSerializer serializer)
		{
			if (value is DiscreteOutcome)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
