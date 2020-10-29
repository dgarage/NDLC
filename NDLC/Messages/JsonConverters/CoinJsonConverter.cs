using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NDLC.Messages.JsonConverters
{
	public class CoinJsonConverter : JsonConverter<Coin>
	{
		class CoinObj
		{
			public Script? ScriptPubKey { get; set; }
			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
			public Script? RedeemScript { get; set; }
			public Money? Value { get; set; }
			public OutPoint? OutPoint { get; set; }
		}
		public override Coin ReadJson(JsonReader reader, Type objectType, [AllowNull] Coin existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType is JsonToken.Null)
				return null!;
			var obj = serializer.Deserialize<CoinObj>(reader);
			if (obj?.OutPoint is null || obj.Value is null || obj.ScriptPubKey is null)
				throw new JsonObjectException("Invalid coin object", reader);
			var c = new Coin(obj.OutPoint, new TxOut(obj.Value, obj.ScriptPubKey));
			if (obj.RedeemScript is Script)
				c = c.ToScriptCoin(obj.RedeemScript);
			return c;
		}

		public override void WriteJson(JsonWriter writer, [AllowNull] Coin value, JsonSerializer serializer)
		{
			if (value is Coin)
			{
				if (value.Outpoint is null || value.Amount is null || value.ScriptPubKey is null)
					throw new InvalidOperationException("Invalid coin object");
				serializer.Serialize(writer, new CoinObj()
				{
					OutPoint = value.Outpoint,
					ScriptPubKey = value.ScriptPubKey,
					RedeemScript = (value as ScriptCoin)?.Redeem,
					Value = value.Amount
				});
			}
		}
	}
}
