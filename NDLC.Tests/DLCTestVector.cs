using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NDLC.Tests
{
	public class DLCTestVector
	{
		public class ExpectedTransactions
		{
			public Transaction FundingTx { get; set; }
			public Transaction[] Cets { get; set; }
			public Transaction RefundTx { get; set; }
		}

		public ExpectedTransactions UnsignedTxs { get; set; }
		public ExpectedTransactions SignedTxs { get; set; }

		public static async IAsyncEnumerable<DLCTestVector> ReadVectors(string file = "Data/dlc_test.json")
		{
			var content = await File.ReadAllTextAsync(file);
			var root = JArray.Parse(content);
			List<DLCTestVector> result = new List<DLCTestVector>();
			foreach (var obj in root)
			{
				yield return new DLCTestVector((JObject)obj);
			}
		}
		public DLCTestVector(JObject testVector)
		{
			TestInputs = testVector["testInputs"];
			Offer = new Offer();
			Offer.OracleInfo = OracleInfo.Parse(
				TestInputs["params"]["oracleInfo"]["publicKey"].Value<string>() +
				TestInputs["params"]["oracleInfo"]["nonce"].Value<string>());
			var ci = new List<ContractInfo>();
			foreach (var jObj in (JArray)TestInputs["params"]["contractInfo"])
			{
				var info = new ContractInfo(new DiscreteOutcome(jObj["preImage"].Value<string>()), Money.Satoshis(jObj["localPayout"].Value<long>()));
				ci.Add(info);
				Assert.Equal(jObj["outcome"].Value<string>(), Encoders.Hex.EncodeData(info.Outcome.Hash));
			}
			Offer.ContractInfo = ci.ToArray();
			Offer.FeeRate = new FeeRate(Money.Satoshis(TestInputs["params"]["feeRate"].Value<long>()), 1);
			Offer.ChainHash = Network.RegTest.GenesisHash;
			Offer.Timeouts = new Timeouts()
			{
				ContractMaturity = TestInputs["params"]["contractMaturityBound"].Value<uint>(),
				ContractTimeout = TestInputs["params"]["contractTimeout"].Value<uint>()
			};
			ExpectedOfferTLV = testVector["offer"].Value<string>();
			ExpectedAcceptTLV = testVector["accept"].Value<string>();
			ExpectedSignTLV = testVector["sign"].Value<string>();
			JsonSerializerSettings settings = new JsonSerializerSettings();
			NBitcoin.JsonConverters.Serializer.RegisterFrontConverters(settings, Network.RegTest);
			UnsignedTxs = testVector["unsignedTxs"].ToObject<ExpectedTransactions>(JsonSerializer.Create(settings));
			SignedTxs = testVector["signedTxs"].ToObject<ExpectedTransactions>(JsonSerializer.Create(settings));
			FillFundingInformation(Offer, "offerParams");
			OfferPrivateKey = new Key(Encoders.Hex.DecodeData(TestInputs["offerParams"]["fundingPrivKey"].Value<string>()));
			Offer.PubKeys.FundingKey = OfferPrivateKey.PubKey;
			AcceptPrivateKey = new Key(Encoders.Hex.DecodeData(TestInputs["acceptParams"]["fundingPrivKey"].Value<string>()));
		}

		public JToken TestInputs { get; set; }
		public void FillFundingInformation(FundingInformation o, string jsonPath)
		{
			o.TotalCollateral = Money.Satoshis(TestInputs[jsonPath]["collateral"].Value<long>());
			o.ChangeAddress = BitcoinAddress.Create(TestInputs[jsonPath]["changeAddress"].Value<string>(), Network.RegTest);
			o.PubKeys = new PubKeyObject();
			o.PubKeys.PayoutAddress = BitcoinAddress.Create(TestInputs[jsonPath]["payoutAddress"].Value<string>(), Network.RegTest);

			List<FundingInput> fundingInputs = new List<FundingInput>();
			foreach (var jObj in (JArray)TestInputs[jsonPath]["fundingInputTxs"])
			{
				var input = ((JObject)jObj);
				var fi = new FundingInput(
								Transaction.Parse(input["tx"].Value<string>(), Network.RegTest),
								input["idx"].Value<uint>(), Sequence.Final);
				fundingInputs.Add(fi);
				var scriptWitness = input["scriptWitness"] is null ? null : new WitScript(Encoders.Hex.DecodeData(input["scriptWitness"].Value<string>()));
				if (scriptWitness is null)
				{
					fi.SetRecommendedMaxWitnessLength();
				}
				else
				{
					fi.MaxWitnessLength = input["maxWitnessLen"].Value<int>();
				}
				var redeemScript = input["redeemScript"] is null ? null : new Script(Encoders.Hex.DecodeData(input["redeemScript"].Value<string>()));
				if (redeemScript is Script)
				{
					fi.RedeemScript = redeemScript;
				}
			}
			o.FundingInputs = fundingInputs.ToArray();
		}

		public Key OfferPrivateKey { get; set; }
		public Offer Offer { get; set; }
		public string ExpectedOfferTLV { get; set; }
		public string ExpectedSignTLV { get; set; }
		public string ExpectedAcceptTLV { get; set; }
		public Key AcceptPrivateKey { get; private set; }
	}
}
