using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class SecpECDSAAdaptorSignatureJsonConverter : JsonConverter<SecpECDSAAdaptorSignature>
	{
		public override SecpECDSAAdaptorSignature ReadJson(JsonReader reader, Type objectType, [AllowNull] SecpECDSAAdaptorSignature existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null!;
			if (reader.TokenType != JsonToken.String)
				throw new JsonObjectException("Unexpected json token for adaptor signature", reader);
			try
			{
				var data = Encoders.Hex.DecodeData((string)reader.Value!);
				if (SecpECDSAAdaptorSignature.TryCreate(data, out var sig) && sig is SecpECDSAAdaptorSignature)
					return sig;
				throw new JsonObjectException("Invalid adaptor signature", reader);
			}
			catch
			{
				throw new JsonObjectException("Invalid adaptor signature", reader);
			}
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] SecpECDSAAdaptorSignature value, JsonSerializer serializer)
		{
			if (value is SecpECDSAAdaptorSignature)
			{
				Span<byte> buf = stackalloc byte[65];
				value.WriteToSpan(buf);
				writer.WriteValue(Encoders.Hex.EncodeData(buf));
			}
		}
	}
}
