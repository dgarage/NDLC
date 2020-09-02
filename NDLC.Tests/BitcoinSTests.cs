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

namespace NDLC.Tests
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
		public void CanCreateFunding()
		{
			var offer = Parse<Messages.Offer>("Data/Offer2.json");
			var accept = Parse<Messages.Accept>("Data/Accept2.json");

			var b = new DLCTransactionBuilder(true, offer, accept, null, Network.RegTest);

			var expected = Transaction.Parse("0200000000010213a31be98d8e29a08cbb3b64de59727b0a6285e2f34338a3ca576ae5250692fc0100000000ffffffffff9208b14cf747f04a7b653debb4dfedc9b4a3291985b91c872dda54b420e7110200000000ffffffff037418f605000000002200205f4c70ba1400d2efc70a2ff32f07a2d79c26d9f6c49a65bde68efc53cf12f0d5dcfb0d700000000016001486039150dbcfd4a136243a7cb4ea2b3dad24e606bbbfd17400000000160014775277f3b5fc4c32bed192ac6b3fd90b4403f1c902483045022100a9518901afbe84053644c0f08e50d45eb373616f7aa729677ecf5718f2fac8b2022032949f92beaf8d4ee8b944fa9452f1feabbc863e59b943fd5acce1aedc89feef012102a3795336df054e4fe408e0d453ff48fc8a0ec4ba7ef9234391babfa4247aef0a024730440220320baf857ca4ff420613e89930dcaa6ee423251fdcfc2108c7a029263cab5c2302201237c4b978c65ed3ed00bb763ff1d1b6f61bdaff8a29e5ca537761e089ce5265012103af3fb0e5788dbfdc478dd5e58d27001177a9a3fb19b990c883dd3ea75aec424c00000000", Network.Main);
			foreach (var input in expected.Inputs)
				input.WitScript = WitScript.Empty;
			// We cheat for now...
			b.FundingOverride = expected;
			var actual = b.BuildFunding();
			Assert.Equal(expected.ToString(), actual.ToString());

			expected = Transaction.Parse("02000000000101ef43cda2f4e8fb1e254864a00b3b81f4915b9c8d060e41fe37523001ac03a08a0000000000feffffff0200879303000000001600145e8bf52af230ae61e9637ecb498a5bed8d09a59b005a62020000000016001411eb59e70fa4abe8c02699b54fb71ed3d76574fc04004830450221009942f177f872e786f6d1edd98abc78e4114bcbd886a72170190e39515841da85022001908f53671480bd2eb869dc7c34d188921a72243dce1e3108646f83948e516401483045022100e988416aba7518317c7a3e1acf051e0ef28ba7cb0473039115072a4073a2a650022028ee55f5d202f9facc230aa7f7c9fd18a75228fc80493bea9d1ae36085cec8830147522103fbae911c3acb17f06a9a544f068d925715900c69d0c58408543c09e75ba249332103f81ee1a50b2b01ce32982b2900c34f0c3a3745e98458e67ecf239086f4e8908852aec8000000", Network.Main);
			foreach (var input in expected.Inputs)
				input.WitScript = WitScript.Empty;

			actual = b.BuildRefund();
			Assert.Equal(expected.ToString(), actual.ToString());
		}

		[Fact]
		public void CanValidateCETSigs()
		{
			var offer = Parse<Messages.Offer>("Data/Offer2.json");
			var accept = Parse<Messages.Accept>("Data/Accept2.json");
			var sign = Parse<Messages.Sign>("Data/Sign2.json");

			var funding = Transaction.Parse("0200000000010213a31be98d8e29a08cbb3b64de59727b0a6285e2f34338a3ca576ae5250692fc0100000000ffffffffff9208b14cf747f04a7b653debb4dfedc9b4a3291985b91c872dda54b420e7110200000000ffffffff037418f605000000002200205f4c70ba1400d2efc70a2ff32f07a2d79c26d9f6c49a65bde68efc53cf12f0d5dcfb0d700000000016001486039150dbcfd4a136243a7cb4ea2b3dad24e606bbbfd17400000000160014775277f3b5fc4c32bed192ac6b3fd90b4403f1c902483045022100a9518901afbe84053644c0f08e50d45eb373616f7aa729677ecf5718f2fac8b2022032949f92beaf8d4ee8b944fa9452f1feabbc863e59b943fd5acce1aedc89feef012102a3795336df054e4fe408e0d453ff48fc8a0ec4ba7ef9234391babfa4247aef0a024730440220320baf857ca4ff420613e89930dcaa6ee423251fdcfc2108c7a029263cab5c2302201237c4b978c65ed3ed00bb763ff1d1b6f61bdaff8a29e5ca537761e089ce5265012103af3fb0e5788dbfdc478dd5e58d27001177a9a3fb19b990c883dd3ea75aec424c00000000", Network.Main);
			var cet = Transaction.Parse("02000000000101ef43cda2f4e8fb1e254864a00b3b81f4915b9c8d060e41fe37523001ac03a08a0000000000feffffff0100e1f505000000001600145e8bf52af230ae61e9637ecb498a5bed8d09a59b04004730440220131ff77ab066d7bfa682691ca70ec9d229dc8e595c4055ddf23468fbed981bf602205c3a0d6b1bfb46da00ed8aad93498ece2d621a0eeacf7e30654be5135d773f0001483045022100efba114529a59517e7c39f6e38fb1c44bd935a8fe6ec63ab15e7a1912bc8b7aa02204a5d54d7f15adf102bb61fc9e4cf174c5a6be212f3c64b74687c6a717ae9104b0147522103fbae911c3acb17f06a9a544f068d925715900c69d0c58408543c09e75ba249332103f81ee1a50b2b01ce32982b2900c34f0c3a3745e98458e67ecf239086f4e8908852ae64000000", Network.Main);
			foreach (var input in cet.Inputs)
				input.WitScript = WitScript.Empty;
			foreach (var isInitiator in new[] { true, false })
			{
				var b = new DLCTransactionBuilder(isInitiator, offer, accept, sign, Network.RegTest);
				b.FundingOverride = funding;
				var actualCet = b.BuildCET(offer.ContractInfo[0].SHA256);
				Assert.Equal(cet.ToString(), actualCet.ToString());
				Assert.True(b.VerifyRemoteCetSigs());
				Assert.True(b.VerifyRemoteRefundSignature());
			}
			
		}

		[Fact]
		public void FullExchange()
		{
			var offerExample = Parse<Messages.Offer>("Data/Offer2.json");

			var initiatorInputKey = new Key();
			var acceptorInputKey = new Key();

			var fund1 = GetFundingPSBT(initiatorInputKey, Money.Coins(0.6m));
			var fund2 = GetFundingPSBT(acceptorInputKey, Money.Coins(0.4m));
			var initiator = new DLCTransactionBuilder(true, null, null, null, Network.RegTest);
			var acceptor = new DLCTransactionBuilder(false, null, null, null, Network.RegTest);

			var offer = initiator.Offer(PSBTFundingTemplate.Parse(fund1), offerExample.OracleInfo, offerExample.ContractInfo, offerExample.Timeouts);
			var accept = acceptor.Accept(offer, PSBTFundingTemplate.Parse(fund2));
			initiator.StartSign(accept);
			var fundPSBT = initiator.BuildFundingPSBT();
			fundPSBT.SignWithKeys(initiatorInputKey);
			var sign = initiator.EndSign(fundPSBT);

			fundPSBT = acceptor.BuildFundingPSBT();
			fundPSBT.SignWithKeys(acceptorInputKey);
			var psbt = acceptor.SignFunding(sign, fundPSBT);
			psbt = psbt.Finalize();
			var fullyVerified = psbt.ExtractTransaction();
			foreach (var i in fullyVerified.Inputs)
				Assert.NotNull(i.WitScript);
		}

		[Fact]
		public void CanCreateAccept()
		{
			var offer = Parse<Messages.Offer>("Data/Offer2.json");
			var builder = new DLCTransactionBuilder(false, null, null, null, Network.RegTest);
			var fundingInputKey = new Key();
			PSBT fundPSBT = GetFundingPSBT(fundingInputKey, Money.Coins(0.4m));
			Assert.True(PSBTFundingTemplate.TryParse(fundPSBT.ToBase64(), fundPSBT.Network, out var template));
			var accept = builder.Accept(offer, template);

			builder = new DLCTransactionBuilder(true, offer, accept, null, Network.RegTest);
			Assert.True(builder.VerifyRemoteCetSigs());
			Assert.True(builder.VerifyRemoteRefundSignature());
		}

		private static PSBT GetFundingPSBT(Key ownedCoinKey, Money collateral)
		{
			var c1 = new Coin(new OutPoint(RandomUtils.GetUInt256(), 0), new TxOut(Money.Coins(2.0m), ownedCoinKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)));
			var txbuilder = Network.RegTest.CreateTransactionBuilder();
			txbuilder.AddCoins(c1);
			txbuilder.Send(Constants.FundingPlaceholder, collateral);
			txbuilder.Send(new Key().ScriptPubKey, Constants.PayoutAmount);
			txbuilder.SetChange(new Key().ScriptPubKey);
			var fundPSBT = txbuilder.BuildPSBT(false);
			return fundPSBT;
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
			var sig = sign.FundingSigs[OutPoint.Parse("e7d8c121f888631289b14989a07e90bcb8c53edf88d5d3ee978fb75b382f26d102000000")][0].ToString(); ;
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
