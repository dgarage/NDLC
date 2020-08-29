using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NBitcoin.DLC.Messages.JsonConverters
{
	class FundingSigsJsonConverter : JsonConverter<Dictionary<OutPoint, Script[]>>
	{
		public override Dictionary<OutPoint, Script[]> ReadJson(JsonReader reader, Type objectType, [AllowNull] Dictionary<OutPoint, Script[]> existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			Expect(reader, JsonToken.StartObject);
			var result = new Dictionary<OutPoint, Script[]>();
			reader.Read();
			while (reader.TokenType != JsonToken.EndObject)
			{
				Expect(reader, JsonToken.PropertyName);
				if (!OutPoint.TryParse((string)reader.Value!, out var h) || h is null)
					throw new FormatException("Unexpected token while parsing funding sigs");
				reader.Read();
				Expect(reader, JsonToken.StartArray);
				reader.Read();
				List<Script> sigs = new List<Script>();
				while (reader.TokenType != JsonToken.EndArray)
				{
					Expect(reader, JsonToken.String);
					var bytes = Encoders.Hex.DecodeData((string)reader.Value!);
					var sig = new Script(bytes);
					sigs.Add(sig);
					reader.Read();
				}
				result.TryAdd(h, sigs.ToArray());
				reader.Read();
			}
			return result;
		}

		private void Expect(JsonReader reader, JsonToken expected)
		{
			if (expected != reader.TokenType)
				throw new FormatException("Unexpected token while parsing funding sigs");
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Dictionary<OutPoint, Script[]> value, JsonSerializer serializer)
		{
			if (value is Dictionary<OutPoint, Script[]>)
			{
				writer.WriteStartObject();
				foreach (var kv in value)
				{
					writer.WritePropertyName(Encoders.Hex.EncodeData(kv.Key.ToBytes()));
					writer.WriteStartArray();
					foreach (var v in kv.Value)
					{
						writer.WriteValue(v.ToHex());
					}
					writer.WriteEndArray();
				}
				writer.WriteEndObject();
			}
		}
	}
}
