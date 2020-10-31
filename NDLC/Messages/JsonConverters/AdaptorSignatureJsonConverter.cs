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
	public class AdaptorSignatureJsonConverter : JsonConverter<AdaptorSignature>
	{
		public override AdaptorSignature ReadJson(JsonReader reader, Type objectType, [AllowNull] AdaptorSignature existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] AdaptorSignature value, JsonSerializer serializer)
		{
			if (value is AdaptorSignature sig)
			{
				writer.WriteStartObject();
				writer.WritePropertyName("adaptorSignature");
				writer.WriteValue(Encoders.Hex.EncodeData(sig.Signature.ToBytes()));
				writer.WritePropertyName("proof");
				writer.WriteValue(Encoders.Hex.EncodeData(sig.Proof.ToBytes()));
				writer.WriteEndObject();
			}
		}
	}
}
