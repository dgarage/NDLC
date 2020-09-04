using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	class OutcomeSigsJsonConverter : JsonConverter<Dictionary<DLCOutcome, AdaptorSignature>>
	{
		public override Dictionary<DLCOutcome, AdaptorSignature> ReadJson(JsonReader reader, Type objectType, [AllowNull] Dictionary<DLCOutcome, AdaptorSignature> existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			Expect(reader, JsonToken.StartArray);
			var result = new Dictionary<DLCOutcome, AdaptorSignature>();
			reader.Read();
			while (reader.TokenType != JsonToken.EndArray)
			{
				Expect(reader, JsonToken.StartObject);
				reader.Read();
				Expect(reader, JsonToken.PropertyName);
				var bytes = Encoders.Hex.DecodeData((string)reader.Value!);
				if (bytes.Length != 32)
					throw new JsonObjectException("Unexpected token while parsing outcome sigs", reader);
				var h = new DLCOutcome(bytes);
				reader.Read();
				Expect(reader, JsonToken.String);
				if (!AdaptorSignature.TryParse((string)reader.Value!, out var sig) || sig is null)
					throw new JsonObjectException("Unexpected token while parsing outcome sigs", reader);
				if (!result.TryAdd(h, sig))
					throw new JsonObjectException("duplicate outcome sig", reader);
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

		public override void WriteJson(JsonWriter writer, [AllowNull] Dictionary<DLCOutcome, AdaptorSignature> value, JsonSerializer serializer)
		{
			if (value is Dictionary<DLCOutcome, AdaptorSignature>)
			{
				writer.WriteStartArray();
				foreach (var kv in value)
				{
					writer.WriteStartObject();
					writer.WritePropertyName(Encoders.Hex.EncodeData(kv.Key.Hash));
					writer.WriteValue(kv.Value.ToString());
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}
		}
	}
}
