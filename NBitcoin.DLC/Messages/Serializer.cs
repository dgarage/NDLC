using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.DLC.Messages
{
	public static class Serializer
	{
		public static void Configure(JsonSerializerSettings settings, Network network)
		{
			if (network == null)
				throw new ArgumentNullException(nameof(network));
			settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
			settings.Converters.Add(new NBitcoin.JsonConverters.BitcoinSerializableJsonConverter(network));
			settings.Converters.Add(new NBitcoin.JsonConverters.BitcoinStringJsonConverter(network));
			settings.Converters.Add(new NBitcoin.JsonConverters.OutpointJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.ScriptJsonConverter());
		}
	}
}
