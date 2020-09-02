using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	class OutcomeSigsJsonConverter : JsonConverter<Dictionary<uint256, AdaptorSignature>>
	{
		public override Dictionary<uint256, AdaptorSignature> ReadJson(JsonReader reader, Type objectType, [AllowNull] Dictionary<uint256, AdaptorSignature> existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			Expect(reader, JsonToken.StartArray);
			var result = new Dictionary<uint256, AdaptorSignature>();
			reader.Read();
			while (reader.TokenType != JsonToken.EndArray)
			{
				Expect(reader, JsonToken.StartObject);
				reader.Read();
				Expect(reader, JsonToken.PropertyName);
				if (!uint256.TryParse((string)reader.Value!, out var h) || h is null)
					throw new FormatException("Unexpected token while parsing outcome sigs");
				reader.Read();
				Expect(reader, JsonToken.String);
				if (!AdaptorSignature.TryParse((string)reader.Value!, out var sig) || sig is null)
					throw new FormatException("Unexpected token while parsing outcome sigs");
				if (!result.TryAdd(h, sig))
					throw new FormatException("duplicate outcome sig");
				reader.Read();
				reader.Read();
			}
			return result;
		}

		private void Expect(JsonReader reader, JsonToken expected)
		{
			if (expected != reader.TokenType)
				throw new FormatException("Unexpected token while parsing outcome sigs");
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Dictionary<uint256, AdaptorSignature> value, JsonSerializer serializer)
		{
			if (value is Dictionary<uint256, AdaptorSignature>)
			{
				writer.WriteStartArray();
				foreach (var kv in value)
				{
					writer.WriteStartObject();
					writer.WritePropertyName(kv.Key.ToString());
					writer.WriteValue(kv.Value.ToString());
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}
		}
	}
}
