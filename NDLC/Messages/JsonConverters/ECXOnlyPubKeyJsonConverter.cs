using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class ECXOnlyPubKeyJsonConverter : JsonConverter<ECXOnlyPubKey>
	{
		public override ECXOnlyPubKey ReadJson(JsonReader reader, Type objectType, [AllowNull] ECXOnlyPubKey existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null!;
			if (reader.TokenType != JsonToken.String)
				throw new JsonObjectException("Unexpected json token for xonly pubkey", reader);
			try
			{
				var data = Encoders.Hex.DecodeData((string)reader.Value!);
				if (ECXOnlyPubKey.TryCreate(data, Context.Instance, out var key) && key is ECXOnlyPubKey)
					return key;
				throw new JsonObjectException("Invalid xonly pubkey", reader);
			}
			catch
			{
				throw new JsonObjectException("Invalid xonly pubkey", reader);
			}
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] ECXOnlyPubKey value, JsonSerializer serializer)
		{
			if (value is ECXOnlyPubKey)
			{
				Span<byte> buf = stackalloc byte[32];
				value.WriteXToSpan(buf);
				writer.WriteValue(Encoders.Hex.EncodeData(buf));
			}
		}
	}
}
