using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.TLV;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NDLC.Tests
{
	public class TLVTests
	{
		public ITestOutputHelper TestOutput { get; }

		public TLVTests(ITestOutputHelper testOutput)
		{
			TestOutput = testOutput;
		}
		[Theory]
		[InlineData(0UL, "00")]
		[InlineData(252UL, "fc")]
		[InlineData(253UL, "fd00fd")]
		[InlineData(65535UL, "fdffff")]
		[InlineData(65536UL, "fe00010000")]
		[InlineData(4294967295UL, "feffffffff")]
		[InlineData(4294967296UL, "ff0000000100000000")]
		[InlineData(18446744073709551615UL, "ffffffffffffffffff")]
		public void CanReadWriteBigSize(ulong value, string hex)
		{
			var ms = new MemoryStream(Encoders.Hex.DecodeData(hex));
			var reader = new TLVReader(ms);
			Assert.Equal(value, reader.ReadBigSize());

			ms.Position = 0;
			var writer = new TLVWriter(ms);
			writer.WriteBigSize(value);
			ms.Position = 0;
			reader = new TLVReader(ms);
			Assert.Equal(value, reader.ReadBigSize());
		}


		[Fact]
		public async Task dlc_fee_test()
		{
			var content = await File.ReadAllTextAsync("Data/dlc_fee_test.json");
			var vectors = JsonConvert.DeserializeObject<DLCFeeTestVector[]>(content);
			foreach (var v in vectors)
			{
				VSizeCalculator calc = new VSizeCalculator();
				calc.ChangeLength = v.inputs.offerChangeSPKLen;
				calc.PayoutLength = v.inputs.offerPayoutSPKLen;
				foreach (var offerInput in v.inputs.offerInputs)
				{
					calc.Inputs.Add(new VSizeCalculator.InputSize()
					{
						MaxWitnessLength = offerInput.maxWitnessLen,
						ScriptSigLength = offerInput.redeemScriptLen == 0 ? 0 : offerInput.redeemScriptLen + 1
					});
				}
				var size = calc.Calculate();
				var rate = new FeeRate((long)v.inputs.feeRate, 1);
				Assert.Equal(Money.Satoshis(v.offerFundingFee), rate.GetFee(size.Funding));
				Assert.Equal(Money.Satoshis(v.offerClosingFee), rate.GetFee(size.CET));


				calc = new VSizeCalculator();
				calc.ChangeLength = v.inputs.acceptChangeSPKLen;
				calc.PayoutLength = v.inputs.acceptPayoutSPKLen;
				foreach (var acceptInput in v.inputs.acceptInputs)
				{
					calc.Inputs.Add(new VSizeCalculator.InputSize()
					{
						MaxWitnessLength = acceptInput.maxWitnessLen,
						ScriptSigLength = acceptInput.redeemScriptLen == 0 ? 0 : acceptInput.redeemScriptLen + 1
					});
				}
				size = calc.Calculate();
				rate = new FeeRate((long)v.inputs.feeRate, 1);
				Assert.Equal(Money.Satoshis(v.acceptFundingFee), rate.GetFee(size.Funding));
				Assert.Equal(Money.Satoshis(v.acceptClosingFee), rate.GetFee(size.CET));
			}
		}

		[Fact]
		public async Task dlc_test()
		{
			await foreach (var vector in DLCTestVector.ReadVectors())
			{
				Assert.Equal(vector.ExpectedOfferTLV, Encoders.Hex.EncodeData(vector.Offer.ToTLV()));
				var accept = new Accept();
				accept.TemporaryContractId = vector.Offer.GetTemporaryContractId();
				vector.FillFundingInformation(accept, "acceptParams");
				accept.PubKeys.FundingKey = vector.AcceptPrivateKey.PubKey;
				var acceptor = new DLCTransactionBuilder(false, null, null, null, Network.RegTest);
				acceptor.Accept(vector.Offer, accept.TotalCollateral);
				var builtAccept = acceptor.FundAccept(vector.AcceptPrivateKey, accept.CreateSetupPSBT(Network.RegTest));
				accept.CetSigs = builtAccept.CetSigs;
				// this signature is non deterministic...
				accept.CetSigs.RefundSig = Accept.ParseFromTLV(vector.ExpectedAcceptTLV, Network.RegTest).CetSigs.RefundSig;
				Assert.Equal(vector.ExpectedAcceptTLV, Encoders.Hex.EncodeData(accept.ToTLV()));

				var actualFundingTransaction = acceptor.GetFundingTransaction();
				var actualRefundTransaction = acceptor.BuildRefund();
				var actualCETs = new Transaction[vector.Offer.ContractInfo.Length];
				for (int i = 0; i < vector.Offer.ContractInfo.Length; i++)
				{
					actualCETs[i] = acceptor.BuildCET(vector.Offer.ContractInfo[i].Outcome);
				}
				var offerer = new DLCTransactionBuilder(true, vector.Offer, null, null, Network.RegTest);
				offerer.Sign1(accept);
				var fundingPSBT = offerer.GetFundingPSBT();
				FillSignatures(fundingPSBT, vector.SignedTxs.FundingTx);
				var sign = offerer.Sign2(vector.OfferPrivateKey, fundingPSBT);
				// this signature is non deterministic...
				sign.CetSigs.RefundSig = Sign.ParseFromTLV(vector.ExpectedSignTLV, Network.RegTest).CetSigs.RefundSig;
				Assert.Equal(vector.ExpectedSignTLV, Encoders.Hex.EncodeData(sign.ToTLV()));
				Assert.Equal(
					RemoveForDebug(vector.UnsignedTxs.FundingTx.ToString()),
					RemoveForDebug(actualFundingTransaction.ToString()));
				Assert.Equal(
					RemoveForDebug(vector.UnsignedTxs.RefundTx.ToString()),
					RemoveForDebug(actualRefundTransaction.ToString()));
				for (int i = 0; i < actualCETs.Length; i++)
				{
					Assert.Equal(
					RemoveForDebug(vector.UnsignedTxs.Cets[i].ToString()),
					RemoveForDebug(actualCETs[i].ToString()));
				}
			}
		}

		private void FillSignatures(PSBT fundingPSBT, Transaction fundingTx)
		{
			for (int i = 0; i < fundingTx.Inputs.Count; i++)
			{
				var psbtInput = fundingPSBT.Inputs[i];
				var signedTxInput = fundingTx.Inputs[i];
				psbtInput.FinalScriptSig = signedTxInput.ScriptSig;
				psbtInput.FinalScriptWitness = signedTxInput.WitScript;
			}
		}

		[Fact]
		public async Task CanConvertMessagesToTLV()
		{
			await foreach (var vector in DLCTestVector.ReadVectors())
			{
				var parsedOffer = Offer.ParseFromTLV(vector.ExpectedOfferTLV, Network.RegTest);
				var actualHex = Encoders.Hex.EncodeData(parsedOffer.ToTLV());
				Assert.Equal(vector.ExpectedOfferTLV, actualHex);
				var parsedAccept = Accept.ParseFromTLV(vector.ExpectedAcceptTLV, Network.RegTest);
				actualHex = Encoders.Hex.EncodeData(parsedAccept.ToTLV());
				Assert.Equal(vector.ExpectedAcceptTLV, actualHex);
				var parsedSign = Sign.ParseFromTLV(vector.ExpectedSignTLV, Network.RegTest);
				actualHex = Encoders.Hex.EncodeData(parsedSign.ToTLV());
				Assert.Equal(vector.ExpectedSignTLV, actualHex);
			}
		}

		private string RemoveForDebug(string json)
		{
			var obj = JObject.Parse(json);
			obj.Remove("hash");
			obj.Remove("size");
			return obj.ToString(Newtonsoft.Json.Formatting.Indented);
		}
	}
}
