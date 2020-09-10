using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.CLI.JsonConverters
{
	public class MnemonicJsonConverter : JsonConverter<Mnemonic>
	{
		public override Mnemonic ReadJson(JsonReader reader, Type objectType, [AllowNull] Mnemonic existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType is JsonToken.Null)
				return null!;
			if (reader.TokenType != JsonToken.String)
				throw new JsonObjectException("Invalid json token type for Mnemonic", reader);
			var mnemonic = new Mnemonic((string)reader.Value!);
			return mnemonic;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Mnemonic value, JsonSerializer serializer)
		{
			if (value is Mnemonic)
				writer.WriteValue(value.ToString());
		}
	}
}
