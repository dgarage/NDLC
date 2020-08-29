using NBitcoin.DataEncoders;
using NBitcoin.DLC.Messages;
using NBitcoin.DLC.Secp256k1;
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

namespace NBitcoin.DLC.Tests
{
	public class BitcoinSTests
	{
		JsonSerializerSettings _Settings;
		JsonSerializerSettings Settings
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
		[Fact]
		public void CanCheckMessages()
		{
			var offer = Parse<Messages.Offer>("Data/Offer.json");
			Assert.Equal("cbaede9e2ad17109b71b85a23306b6d4b93e78e8e8e8d830d836974f16128ae8", offer.ContractInfo[1].SHA256.ToString());
			Assert.Equal(200000000L, offer.ContractInfo[1].Sats.Satoshi);
			Assert.Equal(100000000L, offer.TotalCollateral.Satoshi);

			var accept = Parse<Messages.Accept>("Data/Accept.json");
			Assert.Equal(100000000L, accept.TotalCollateral.Satoshi);
			Assert.Equal(2, accept.CetSigs.OutcomeSigs.Count);
			Assert.Equal("00595165a73cc04eaab13077abbffae5edf0c371b9621fad9ea28da00026373a853bcc3ac24939d0d004e39b96469b2173aa20e429ca3bffd3ab0db7735ad6d87a012186ff2afb8c05bca05ad8acf22aecadf47f967bb81753c13c3b081fc643c8db855283e554359d1a1a870d2b016a9db6e6838f5ca1afb1508aa0c50fd9d05ac60a7b7cc2570b62426d467183baf109fb23a5fdf37f273c087c23744c6529f353", accept.CetSigs.OutcomeSigs[new uint256("1bd3f7beb217b55fd40b5ea7e62dc46e6428c15abd9e532ac37604f954375526")].ToString());
			var str = JsonConvert.SerializeObject(accept, Settings);
			accept = JsonConvert.DeserializeObject<Accept>(str, Settings);
			Assert.Equal("00595165a73cc04eaab13077abbffae5edf0c371b9621fad9ea28da00026373a853bcc3ac24939d0d004e39b96469b2173aa20e429ca3bffd3ab0db7735ad6d87a012186ff2afb8c05bca05ad8acf22aecadf47f967bb81753c13c3b081fc643c8db855283e554359d1a1a870d2b016a9db6e6838f5ca1afb1508aa0c50fd9d05ac60a7b7cc2570b62426d467183baf109fb23a5fdf37f273c087c23744c6529f353", accept.CetSigs.OutcomeSigs[new uint256("1bd3f7beb217b55fd40b5ea7e62dc46e6428c15abd9e532ac37604f954375526")].ToString());

			var sign = Parse<Messages.Sign>("Data/Sign.json");
			var sig = Encoders.Hex.EncodeData(sign.FundingSigs[OutPoint.Parse("e7d8c121f888631289b14989a07e90bcb8c53edf88d5d3ee978fb75b382f26d102000000")][0].ToBytes()); ;
			Assert.Equal("220202f37b2ca55f880f9d73b311a4369f2f02fbadefc037628d6eaef98ec222b8bcb046304302206589a41139774c27c242af730ae225483ba264eeec026952c6a6cd0bc8a7413c021f2289c32cfb7b4baa5873e350675133de93cfc69de50220fddafbc6f23a46e201", sig);

			CanRoundTrip<Messages.Accept>("Data/Accept.json");
			CanRoundTrip<Messages.Offer>("Data/Offer.json");
			CanRoundTrip<Messages.Sign>("Data/Sign.json");
		}

		private void CanRoundTrip<T>(string file)
		{
			var content = File.ReadAllText(file);
			var data = JsonConvert.DeserializeObject<T>(content, Settings);
			var back = JsonConvert.SerializeObject(data, Settings);
			var expected = JObject.Parse(content);
			var actual = JObject.Parse(back);
			Assert.Equal(expected.ToString(Formatting.Indented), actual.ToString(Formatting.Indented));
		}

		private T Parse<T>(string file)
		{
			var content = File.ReadAllText(file);
			return JsonConvert.DeserializeObject<T>(content, Settings);
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

		[Fact]
		public void testAdaptorVeirfy()
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
			var secret = adaptorSigObj.ExtractSecret(sigObj, pubkey);
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
			return pubkeyObj.SigVerify(adaptorSigObj, msg, adaptorObj, adaptorProofObj);
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
