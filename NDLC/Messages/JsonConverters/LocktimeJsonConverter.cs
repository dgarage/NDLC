using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class LocktimeJsonConverter : JsonConverter<LockTime>
	{
		public override LockTime ReadJson(JsonReader reader, Type objectType, [AllowNull] LockTime existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType != JsonToken.Integer)
				throw new FormatException("Expected integer for locktime");
			return new LockTime((uint)(long)reader.Value!);
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] LockTime value, JsonSerializer serializer)
		{
			writer.WriteValue(value.Value);
		}
	}
}
