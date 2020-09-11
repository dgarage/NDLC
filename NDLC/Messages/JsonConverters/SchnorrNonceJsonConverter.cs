using NBitcoin.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class SchnorrNonceJsonConverter : JsonConverter<SchnorrNonce>
	{
		public override SchnorrNonce ReadJson(JsonReader reader, Type objectType, [AllowNull] SchnorrNonce existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType is JsonToken.Null)
				return null!;
			if (reader.TokenType != JsonToken.String)
				throw new JsonObjectException("Invalid token type for schnorr nonce", reader);
			if (!SchnorrNonce.TryParse((string)reader.Value!, out var n) || n is null)
				throw new JsonObjectException("Invalid schnorr nonce", reader);
			return n;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] SchnorrNonce value, JsonSerializer serializer)
		{
			if (value is SchnorrNonce)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
