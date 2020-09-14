using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class DiscretePayoffJsonConverter : JsonConverter<DiscretePayoff>
	{
		public override DiscretePayoff ReadJson(JsonReader reader, Type objectType, [AllowNull] DiscretePayoff existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType != JsonToken.String)
				throw new FormatException("Expected string for discrete payoff");
			if (!DiscretePayoff.TryParse((string)reader.Value!, out var o) || o is null)
				throw new FormatException("Invalid discrete payoff");
			return o;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] DiscretePayoff value, JsonSerializer serializer)
		{
			if (value is DiscretePayoff)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
