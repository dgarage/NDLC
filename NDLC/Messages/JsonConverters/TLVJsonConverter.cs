using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.TLV;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	class TLVJsonConverter<T> : JsonConverter<T?> where T : class, ITLVObject, new()
	{
		Network network;
		public TLVJsonConverter(Network network)
		{
			this.network = network;
		}
		public override T? ReadJson(JsonReader reader, Type objectType, [AllowNull] T? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType is JsonToken.Null)
				return null;
			if (!(reader.Value is string))
				return null;
			var obj = new T();
			var base64 = (string)reader.Value;
			var bytes = Encoders.Base64.DecodeData(base64);
			var r = new TLVReader(new MemoryStream(bytes));
			obj.ReadTLV(r, network);
			return obj;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] T? value, JsonSerializer serializer)
		{
			if (value is T)
			{
				MemoryStream ms = new MemoryStream();
				TLVWriter tlv = new TLVWriter(ms);
				value.WriteTLV(tlv);
				ms.Position = 0;
				writer.WriteValue(Encoders.Base64.EncodeData(ms.ToArray()));
			}
		}
	}
}
