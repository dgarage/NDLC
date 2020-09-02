using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NDLC.Messages.JsonConverters
{
	class FundingSigsJsonConverter : JsonConverter<Dictionary<OutPoint, List<PartialSignature>>>
	{
		public override Dictionary<OutPoint, List<PartialSignature>> ReadJson(JsonReader reader, Type objectType, [AllowNull] Dictionary<OutPoint, List<PartialSignature>> existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			Expect(reader, JsonToken.StartObject);
			var result = new Dictionary<OutPoint, List<PartialSignature>>();
			reader.Read();
			while (reader.TokenType != JsonToken.EndObject)
			{
				Expect(reader, JsonToken.PropertyName);
				if (!OutPoint.TryParse((string)reader.Value!, out var h) || h is null)
					throw new FormatException("Unexpected token while parsing funding sigs");
				reader.Read();
				Expect(reader, JsonToken.StartArray);
				reader.Read();
				List<PartialSignature> sigs = new List<PartialSignature>();
				while (reader.TokenType != JsonToken.EndArray)
				{
					Expect(reader, JsonToken.String);
					if (!PartialSignature.TryParse((string)reader.Value!, out var sig) || sig is null)
						throw new FormatException("Unexpected partial sig while parsing funding sigs");
					sigs.Add(sig);
					reader.Read();
				}
				result.TryAdd(h, sigs);
				reader.Read();
			}
			return result;
		}

		private void Expect(JsonReader reader, JsonToken expected)
		{
			if (expected != reader.TokenType)
				throw new FormatException("Unexpected token while parsing funding sigs");
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Dictionary<OutPoint, List<PartialSignature>> value, JsonSerializer serializer)
		{
			if (value is Dictionary<OutPoint, List<PartialSignature>>)
			{
				writer.WriteStartObject();
				foreach (var kv in value)
				{
					writer.WritePropertyName(Encoders.Hex.EncodeData(kv.Key.ToBytes()));
					writer.WriteStartArray();
					foreach (var v in kv.Value)
					{
						writer.WriteValue(v.ToString());
					}
					writer.WriteEndArray();
				}
				writer.WriteEndObject();
			}
		}
	}
}
