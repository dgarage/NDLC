using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.Secp256k1;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using NBitcoin;
using Xunit.Abstractions;
using NBitcoin.Logging;
using Xunit.Sdk;
using NBitcoin.Crypto;

namespace NDLC.Tests
{
	public class BitcoinSTests
	{
		public BitcoinSTests(ITestOutputHelper testOutputHelper)
		{
			this.testOutputHelper = testOutputHelper;
		}
		static JsonSerializerSettings _Settings;
		static JsonSerializerSettings Settings
		{
			get
			{
				if (_Settings is null)
				{
					var settings = new JsonSerializerSettings();
					settings.Formatting = Formatting.Indented;
					Messages.Serializer.Configure(settings, Network.RegTest);
					_Settings = settings;
				}
				return _Settings;
			}
		}
		static JsonSerializerSettings _TestnetSettings;
		private readonly ITestOutputHelper testOutputHelper;

		static JsonSerializerSettings TestnetSettings
		{
			get
			{
				if (_TestnetSettings is null)
				{
					var settings = new JsonSerializerSettings();
					settings.Formatting = Formatting.Indented;
					Messages.Serializer.Configure(settings, Network.TestNet);
					_TestnetSettings = settings;
				}
				return _TestnetSettings;
			}
		}

		private static void RemoveSigs(Transaction cet)
		{
			foreach (var input in cet.Inputs)
				input.WitScript = WitScript.Empty;
		}

		[Fact]
		public void CanComputeSigPoint()
		{
			for (int i = 0; i < 100; i++)
			{
				var oracleKey = Context.Instance.CreateECPrivKey(RandomUtils.GetBytes(32));
				var msg = RandomUtils.GetBytes(32);
				var kValue = Context.Instance.CreateECPrivKey(RandomUtils.GetBytes(32));
				var nonce = kValue.CreateSchnorrNonce();
				var sig = oracleKey.SignBIP340(msg, new PrecomputedNonceFunctionHardened(kValue.ToBytes()));
				Assert.Equal(sig.rx, nonce.PubKey.Q.x);
				Assert.True(oracleKey.CreateXOnlyPubKey().TryComputeSigPoint(msg, nonce, out var sigPoint));
				Assert.Equal(sigPoint.Q, Context.Instance.CreateECPrivKey(sig.s).CreatePubKey().Q);
			}
		}
		[Fact]
		public void CanConvertContractInfoToPayoff()
		{
			var payoffs = new DiscretePayoffs();
			payoffs.Add(new DiscreteOutcome("a"), Money.Coins(5.0m));
			payoffs.Add(new DiscreteOutcome("b"), Money.Coins(-5.0m));
			payoffs.Add(new DiscreteOutcome("c"), Money.Coins(-2.0m));
			Assert.Equal(Money.Coins(5.0m), payoffs.CalculateMinimumCollateral());
			var ci = payoffs.ToContractInfo(payoffs.CalculateMinimumCollateral());
			Assert.Equal(Money.Coins(10.0m), ci[0].Payout);
			Assert.Equal(Money.Coins(0m), ci[1].Payout);
			Assert.Equal(Money.Coins(3.0m), ci[2].Payout);

			payoffs = DiscretePayoffs.CreateFromContractInfo(ci, Money.Coins(5.0m));
			Assert.Equal(Money.Coins(5.0m), payoffs[0].Reward);
			Assert.Equal(Money.Coins(-5.0m), payoffs[1].Reward);
			Assert.Equal(Money.Coins(-2.0m), payoffs[2].Reward);
		}

		[Fact]
		public void CanGenerateSchnorrNonce()
		{
			for (int i = 0; i < 30; i++)
			{
				var privKey = new Key().ToECPrivKey();
				var nonce = privKey.CreateSchnorrNonce();
				var msg = RandomUtils.GetBytes(32);
				privKey.TrySignBIP340(msg, new PrecomputedNonceFunctionHardened(privKey.ToBytes()), out var sig);
				//Assert.Equal(sig.rx, nonce.fe);
				Assert.True(privKey.CreateXOnlyPubKey().SigVerifyBIP340(sig, msg));
			}
		}

		private static T Parse<T>(string file, JsonSerializerSettings settings = null)
		{
			var content = File.ReadAllText(file);
			return JsonConvert.DeserializeObject<T>(content, settings ?? Settings);
		}

		[Fact]
		public void testAdaptorSign()
		{

			byte[] msg = toByteArray("024BDD11F2144E825DB05759BDD9041367A420FAD14B665FD08AF5B42056E5E2");
			byte[] adaptor = toByteArray("038D48057FC4CE150482114D43201B333BF3706F3CD527E8767CEB4B443AB5D349");
			byte[] seckey = toByteArray("90AC0D5DC0A1A9AB352AFB02005A5CC6C4DF0DA61D8149D729FF50DB9B5A5215");
			String expectedAdaptorSig = "00CBE0859638C3600EA1872ED7A55B8182A251969F59D7D2DA6BD4AFEDF25F5021A49956234CBBBBEDE8CA72E0113319C84921BF1224897A6ABD89DC96B9C5B208";
			String expectedAdaptorProof = "00B02472BE1BA09F5675488E841A10878B38C798CA63EFF3650C8E311E3E2EBE2E3B6FEE5654580A91CC5149A71BF25BCBEAE63DEA3AC5AD157A0AB7373C3011D0FC2592A07F719C5FC1323F935569ECD010DB62F045E965CC1D564EB42CCE8D6D";

			byte[] resultArr = adaptorSign(seckey, adaptor, msg);

			assertEquals(resultArr.Length, 162, "testAdaptorSign");

			String adaptorSig = toHex(resultArr);
			assertEquals(adaptorSig, expectedAdaptorSig + expectedAdaptorProof, "testAdaptorSign");
		}

		private void assertEquals<T>(T actual, T expected, string message)
		{
			Assert.Equal(expected, actual);
		}

		private string toHex(byte[] resultArr)
		{
			return Encoders.Hex.EncodeData(resultArr).ToUpperInvariant();
		}

		class AcceptorTest
		{
			public static AcceptorTest Open(string folder, Network network)
			{
				var settings = network == Network.RegTest ? Settings : TestnetSettings;
				AcceptorTest t = new AcceptorTest();
				var fundingOverride = Path.Combine(folder, "FundingOverride.hex");
				if (File.Exists(fundingOverride))
				{
					t.FundingOverride = Transaction.Parse(File.ReadAllText(fundingOverride), network);
				}
				t.Offer = Parse<Offer>(Path.Combine(folder, "Offer.json"), settings);
				t.Sign = Parse<Sign>(Path.Combine(folder, "Sign.json"), settings);

				var attestation = Path.Combine(folder, "OracleAttestation.hex");
				if (File.Exists(attestation))
				{
					t.OracleAttestation = new Key(Encoders.Hex.DecodeData(File.ReadAllText(attestation)));
				}
				t.Builder = new DLCTransactionBuilder(false, null, null, null, network);
				t.FundingTemplate = PSBT.Parse(File.ReadAllText(Path.Combine(folder, "FundingTemplate.psbt")), network);
				return t;
			}
			public Transaction FundingOverride { get; set; }
			public Offer Offer { get; set; }
			public Sign Sign { get; set; }
			public Key OracleAttestation { get; set; }

			public DLCTransactionBuilder Builder { get; set; }
			/// <summary>
			/// Funding templates are PSBT built with the following format:
			/// * 1 output sending to "collateral" BTC to the payout address
			/// * Optionally, 1 output which is the change address
			/// </summary>
			public PSBT FundingTemplate { get; set; }
		}

		[Fact]
		public void CheckAttestationMatchBitcoinS()
		{
			var oracleInfo = OracleInfo.Parse("156c7d1c7922f0aa1168d9e21ac77ea88bbbe05e24e70a08bbe0519778f2e5daea3a68d8749b81682513b0479418d289d17e24d4820df2ce979f1a56a63ca525");
			Assert.True(oracleInfo.TryComputeSigpoint(new DiscreteOutcome("Democrat_win"), out var sigpoint));
			var attestation = Context.Instance.CreateECPrivKey(Encoders.Hex.DecodeData("77a5aabd716936411bbe19219bd0b261fae8f0524367268feb264e0a3b215766"));
			var pubKey = attestation.CreatePubKey();
			Assert.Equal(sigpoint, pubKey);
		}
		//[Fact]
		//public void CheckNormalization()
		//{
		//	string elephant = "éléphant";
		//	testOutputHelper.WriteLine(elephant);
		//	var notNormalized = Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(elephant));
		//	testOutputHelper.WriteLine($"Without normalization: {Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(elephant.Normalize()))}");
		//	foreach (var form in new[] { NormalizationForm.FormD, NormalizationForm.FormC, NormalizationForm.FormKD, NormalizationForm.FormKC })
		//	{
		//		testOutputHelper.WriteLine(elephant.Normalize(form));
		//		testOutputHelper.WriteLine($"{form}: {Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(elephant.Normalize(form)))}");
		//	}
		//}

		[Fact]
		public void testAdaptorVerify()
		{

			byte[]
			msg = toByteArray("024BDD11F2144E825DB05759BDD9041367A420FAD14B665FD08AF5B42056E5E2");
			byte[]
			adaptorSig = toByteArray("00CBE0859638C3600EA1872ED7A55B8182A251969F59D7D2DA6BD4AFEDF25F5021A49956234CBBBBEDE8CA72E0113319C84921BF1224897A6ABD89DC96B9C5B208");
			byte[]
			adaptorProof = toByteArray("00B02472BE1BA09F5675488E841A10878B38C798CA63EFF3650C8E311E3E2EBE2E3B6FEE5654580A91CC5149A71BF25BCBEAE63DEA3AC5AD157A0AB7373C3011D0FC2592A07F719C5FC1323F935569ECD010DB62F045E965CC1D564EB42CCE8D6D");
			byte[]
			adaptor = toByteArray("038D48057FC4CE150482114D43201B333BF3706F3CD527E8767CEB4B443AB5D349");
			byte[]
			pubkey = toByteArray("03490CEC9A53CD8F2F664AEA61922F26EE920C42D2489778BB7C9D9ECE44D149A7");

			bool result = adaptorVerify(adaptorSig, pubkey, msg, adaptor, adaptorProof);

			assertEquals(result, true, "testAdaptorVeirfy");
		}

		[Fact]
		public void testAdaptorAdapt()
		{
			byte[] secret = toByteArray("475697A71A74FF3F2A8F150534E9B67D4B0B6561FAB86FCAA51F8C9D6C9DB8C6");
			byte[] adaptorSig = toByteArray("01099C91AA1FE7F25C41085C1D3C9E73FE04A9D24DAC3F9C2172D6198628E57F47BB90E2AD6630900B69F55674C8AD74A419E6CE113C10A21A79345A6E47BC74C1");

			byte[] resultArr = adaptorAdapt(secret, adaptorSig);

			String expectedSig = "30440220099C91AA1FE7F25C41085C1D3C9E73FE04A9D24DAC3F9C2172D6198628E57F4702204D13456E98D8989043FD4674302CE90C432E2F8BB0269F02C72AAFEC60B72DE1";
			String sigString = toHex(resultArr);
			assertEquals(sigString, expectedSig, "testAdaptorAdapt");
		}

		[Fact]
		public void testAdaptorExtractSecret()
		{
			byte[] sig = toByteArray("30440220099C91AA1FE7F25C41085C1D3C9E73FE04A9D24DAC3F9C2172D6198628E57F4702204D13456E98D8989043FD4674302CE90C432E2F8BB0269F02C72AAFEC60B72DE1");
			byte[] adaptorSig = toByteArray("01099C91AA1FE7F25C41085C1D3C9E73FE04A9D24DAC3F9C2172D6198628E57F47BB90E2AD6630900B69F55674C8AD74A419E6CE113C10A21A79345A6E47BC74C1");
			byte[] adaptor = toByteArray("038D48057FC4CE150482114D43201B333BF3706F3CD527E8767CEB4B443AB5D349");

			byte[] resultArr = adaptorExtractSecret(sig, adaptorSig, adaptor);

			String expectedSecret = "475697A71A74FF3F2A8F150534E9B67D4B0B6561FAB86FCAA51F8C9D6C9DB8C6";
			String sigString = toHex(resultArr);
			assertEquals(sigString, expectedSecret, "testAdaptorExtractSecret");
		}

		[Fact]
		public void testSchnorrSign()
		{

			byte[] data = toByteArray("E48441762FB75010B2AA31A512B62B4148AA3FB08EB0765D76B252559064A614");
			byte[] secKey = toByteArray("688C77BC2D5AAFF5491CF309D4753B732135470D05B7B2CD21ADD0744FE97BEF");
			byte[] auxRand = toByteArray("02CCE08E913F22A36C5648D6405A2C7C50106E7AA2F1649E381C7F09D16B80AB");

			byte[] sigArr = schnorrSign(data, secKey, auxRand);
			String sigStr = toHex(sigArr);
			String expectedSig = "6470FD1303DDA4FDA717B9837153C24A6EAB377183FC438F939E0ED2B620E9EE5077C4A8B8DCA28963D772A94F5F0DDF598E1C47C137F91933274C7C3EDADCE8";
			assertEquals(sigStr, expectedSig, "testSchnorrSign");
		}

		private byte[] schnorrSign(byte[] data, byte[] secKey, byte[] auxRand)
		{
			Assert.True(Context.Instance.TryCreateECPrivKey(secKey, out var key));
			Assert.True(key.TrySignBIP340(data, new BIP340NonceFunction(auxRand), out var sig));
			var buf = new byte[64];
			sig.WriteToSpan(buf);
			return buf;
		}

		[Fact]
		public void testSchnorrComputeSigPoint()
		{

			byte[] data = toByteArray("E48441762FB75010B2AA31A512B62B4148AA3FB08EB0765D76B252559064A614");
			byte[] nonce = toByteArray("F14D7E54FF58C5D019CE9986BE4A0E8B7D643BD08EF2CDF1099E1A457865B547");
			byte[] pubKey = toByteArray("B33CC9EDC096D0A83416964BD3C6247B8FECD256E4EFA7870D2C854BDEB33390");

			byte[] pointArr = schnorrComputeSigPoint(data, nonce, pubKey, true);
			String pointStr = toHex(pointArr);
			String expectedPoint = "03735ACF82EEF9DA1540EFB07A68251D5476DABB11AC77054924ECCBB4121885E8";
			assertEquals(pointStr, expectedPoint, "testSchnorrComputeSigPoint");
		}
		[Fact]
		public void testSchnorrComputeSigPoint2()
		{
			byte[] data = toByteArray("FB84860B10A497DEDDC3EFB45D20786ED72D27CFCF54A09A0E1C04DCEF4882A1");
			byte[] nonce = toByteArray("F67F8F41718C86F05EB95FAB308F5ED788A2A963124299154648F97124CAA579");
			byte[] pubKey = toByteArray("E4D36E995FF4BBA4DA2B60AD907D61D36E120D6F7314A3C2A20C6E27A5CD850F");

			byte[] pointArr = schnorrComputeSigPoint(data, nonce, pubKey, true);
			String pointStr = toHex(pointArr);
			String expectedPoint = "03C88D853DEA7F3E9C33027E99680446E4FB2ABF87704475522C8793CD1B03684B";
			assertEquals(pointStr, expectedPoint, "testSchnorrComputeSigPoint");
		}

		private byte[] schnorrComputeSigPoint(byte[] data, byte[] nonce, byte[] pubKey, bool compressed)
		{
			Assert.True(ECXOnlyPubKey.TryCreate(pubKey, Context.Instance, out var pk));
			Assert.True(SchnorrNonce.TryCreate(nonce, out var n));
			Assert.True(new OracleInfo(pk, n).TryComputeSigpoint(new DiscreteOutcome(data), out var sigpoint));
			return sigpoint.ToBytes(compressed);
		}

		[Fact]
		public void testSchnorrVerify()
		{

			byte[] sig = toByteArray("6470FD1303DDA4FDA717B9837153C24A6EAB377183FC438F939E0ED2B620E9EE5077C4A8B8DCA28963D772A94F5F0DDF598E1C47C137F91933274C7C3EDADCE8");
			byte[] data = toByteArray("E48441762FB75010B2AA31A512B62B4148AA3FB08EB0765D76B252559064A614");
			byte[] pubx = toByteArray("B33CC9EDC096D0A83416964BD3C6247B8FECD256E4EFA7870D2C854BDEB33390");

			var result = schnorrVerify(sig, data, pubx);

			assertEquals(result, true, "testSchnorrVerify");
		}

		private bool schnorrVerify(byte[] sig, byte[] data, byte[] pubx)
		{
			Assert.True(NBitcoin.Secp256k1.SecpSchnorrSignature.TryCreate(sig, out var o));
			Assert.True(ECXOnlyPubKey.TryCreate(pubx, Context.Instance, out var pub));
			return pub.SigVerifyBIP340(o, data);
		}

		[Fact]
		public void testSchnorrSignWithNonce()
		{

			byte[] data = toByteArray("E48441762FB75010B2AA31A512B62B4148AA3FB08EB0765D76B252559064A614");
			byte[] secKey = toByteArray("688C77BC2D5AAFF5491CF309D4753B732135470D05B7B2CD21ADD0744FE97BEF");
			byte[] nonce = toByteArray("8C8CA771D3C25EB38DE7401818EEDA281AC5446F5C1396148F8D9D67592440FE");

			byte[] sigArr = schnorrSignWithNonce(data, secKey, nonce);
			String sigStr = toHex(sigArr);
			String expectedSig = "5DA618C1936EC728E5CCFF29207F1680DCF4146370BDCFAB0039951B91E3637A958E91D68537D1F6F19687CEC1FD5DB1D83DA56EF3ADE1F3C611BABD7D08AF42";
			assertEquals(sigStr, expectedSig, "testSchnorrSignWithNonce");
		}

		private byte[] schnorrSignWithNonce(byte[] data, byte[] secKey, byte[] nonce)
		{
			Assert.True(Context.Instance.TryCreateECPrivKey(secKey, out var key));
			Assert.True(key.TrySignBIP340(data, new PrecomputedNonceFunctionHardened(nonce), out var sig));
			var buf = new byte[64];
			sig.WriteToSpan(buf);
			return buf;
		}

		private byte[] adaptorAdapt(byte[] secret, byte[] adaptorSig)
		{
			Assert.True(SecpECDSAAdaptorSignature.TryCreate(adaptorSig, out var adaptorSigObj));
			var privKey = Context.Instance.CreateECPrivKey(secret);
			return adaptorSigObj.AdaptECDSA(privKey).ToDER();
		}

		private byte[] adaptorExtractSecret(byte[] sig, byte[] adaptorSig, byte[] adaptor)
		{
			Assert.True(SecpECDSAAdaptorSignature.TryCreate(adaptorSig, out var adaptorSigObj));
			Assert.True(SecpECDSASignature.TryCreateFromDer(sig, out var sigObj));
			Assert.True(Context.Instance.TryCreatePubKey(adaptor, out var pubkey));
			Assert.True(adaptorSigObj.TryExtractSecret(sigObj, pubkey, out var secret));
			var result = new byte[32];
			secret.WriteToSpan(result);
			return result;
		}
		private bool adaptorVerify(byte[] adaptorSig, byte[] pubkey, byte[] msg, byte[] adaptor, byte[] adaptorProof)
		{
			Assert.True(SecpECDSAAdaptorSignature.TryCreate(adaptorSig, out var adaptorSigObj));
			Assert.True(Context.Instance.TryCreatePubKey(pubkey, out var pubkeyObj));
			Assert.True(Context.Instance.TryCreatePubKey(adaptor, out var adaptorObj));
			Assert.True(SecpECDSAAdaptorProof.TryCreate(adaptorProof, out var adaptorProofObj));
			return pubkeyObj.SigVerify(adaptorSigObj, adaptorProofObj, msg, adaptorObj);
		}
		private byte[] adaptorSign(byte[] seckey, byte[] adaptor, byte[] msg)
		{
			var seckeyObj = Context.Instance.CreateECPrivKey(seckey);
			Assert.True(Context.Instance.TryCreatePubKey(adaptor, out var adaptorObj));
			Assert.True(seckeyObj.TrySignAdaptor(msg, adaptorObj, out var sig, out var proof));
			var output = new byte[65 + 97];
			sig.WriteToSpan(output);
			proof.WriteToSpan(output.AsSpan().Slice(65));
			return output;
		}

		byte[] toByteArray(string hex)
		{
			return Encoders.Hex.DecodeData(hex.ToLowerInvariant());
		}
	}
}
