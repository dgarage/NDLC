using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class PartialSignatureJsonConverter : JsonConverter<PartialSignature>
	{
		public override PartialSignature ReadJson(JsonReader reader, Type objectType, [AllowNull] PartialSignature existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			Expect(reader, JsonToken.String);
			if (!PartialSignature.TryParse((string)reader.Value!, out var sig) || sig is null)
				throw new FormatException("Unexpected token while parsing partial sig");
			return sig;
		}
		private void Expect(JsonReader reader, JsonToken expected)
		{
			if (expected != reader.TokenType)
				throw new FormatException("Unexpected token while parsing partial sig");
		}
		public override void WriteJson(JsonWriter writer, [AllowNull] PartialSignature value, JsonSerializer serializer)
		{
			if (value is PartialSignature)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
