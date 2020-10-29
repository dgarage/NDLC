using NBitcoin;
using NBitcoin.Secp256k1;
using NDLC.Messages.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Messages
{
	public static class Serializer
	{
		public static void Configure(JsonSerializerSettings settings, Network network)
		{
			if (network == null)
				throw new ArgumentNullException(nameof(network));
			settings.ContractResolver = new CamelCasePropertyNamesContractResolver()
			{
				NamingStrategy = new CamelCaseNamingStrategy
				{
					ProcessDictionaryKeys = false
				}
			};
			settings.Converters.Add(new NBitcoin.JsonConverters.BitcoinSerializableJsonConverter(network));
			settings.Converters.Add(new NBitcoin.JsonConverters.BitcoinStringJsonConverter(network));
			settings.Converters.Add(new NBitcoin.JsonConverters.OutpointJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.MoneyJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.PSBTJsonConverter(network));
			settings.Converters.Add(new NBitcoin.JsonConverters.FeeRateJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.ScriptJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.SignatureJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.UInt256JsonConverter());
			settings.Converters.Add(new TLVJsonConverter<Offer>(network));
			settings.Converters.Add(new TLVJsonConverter<Accept>(network));
			settings.Converters.Add(new TLVJsonConverter<Sign>(network));
			settings.Converters.Add(new ECXOnlyPubKeyJsonConverter());
			settings.Converters.Add(new OracleInfoJsonConverter());
			settings.Converters.Add(new ContractInfoJsonConverter());
			settings.Converters.Add(new SecpECDSAAdaptorSignatureJsonConverter());
			settings.Converters.Add(new CoinJsonConverter());
			settings.Converters.Add(new NBitcoin.JsonConverters.KeyJsonConverter());
			settings.Converters.Add(new DiscreteOutcomeJsonConverter());
			settings.Converters.Add(new DiscretePayoffJsonConverter());
		}
	}
}
