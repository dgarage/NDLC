using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class OracleInfoJsonConverter : JsonConverter<OracleInfo>
	{
		public override OracleInfo ReadJson(JsonReader reader, Type objectType, [AllowNull] OracleInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType != JsonToken.String)
				throw new FormatException("Expected string for oracleInfo");
			if (!OracleInfo.TryParse((string)reader.Value!, out var oracle) || oracle is null)
				throw new FormatException("Invalid oracleInfo string");
			return oracle;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] OracleInfo value, JsonSerializer serializer)
		{
			if (value is OracleInfo)
			{
				writer.WriteValue(value.ToString());
			}
		}
	}
}
