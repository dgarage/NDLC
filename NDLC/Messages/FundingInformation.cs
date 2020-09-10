using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace NDLC.Messages
{
	public class PartialSignature
	{
		public TransactionSignature Signature { get; }
		public PubKey PubKey { get; private set; }

		public static bool TryParse(string str, out PartialSignature? sig)
		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			sig = null;
			try
			{
				var bytes = Encoders.Hex.DecodeData(str);
				if (bytes.Length < 2 + 33 + 1 || bytes[0] != 0x22 || bytes[1] != 0x02)
					return false;
				var pk = new NBitcoin.PubKey(bytes.AsSpan().Slice(2, 33).ToArray());
				var siglen = bytes[2 + 33];
				if (siglen < 75 && bytes.Length != 2 + 33 + 1 + siglen)
					return false;
				var sigBytes = bytes.AsSpan().Slice(2 + 33 + 1).ToArray();
				if (!TransactionSignature.IsValid(sigBytes))
					return false;
				var s = new TransactionSignature(sigBytes);
				sig = new PartialSignature(pk, s);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public override string ToString()
		{
			var buf = new byte[3 + 33 + 80];
			buf[0] = 0x22;
			buf[1] = 0x02;
			PubKey.ToBytes(buf.AsSpan().Slice(2), out _);
			var sigBytes = Signature.ToBytes();
			sigBytes.AsSpan().CopyTo(buf.AsSpan().Slice(2 + 33 + 1));
			buf[2 + 33] = (byte)sigBytes.Length;
			return Encoders.Hex.EncodeData(buf, 0, 2 + 33 + 1 + sigBytes.Length);
		}

		public PartialSignature(PubKey pubKey, TransactionSignature signature)
		{
			if (pubKey == null)
				throw new ArgumentNullException(nameof(pubKey));
			if (signature == null)
				throw new ArgumentNullException(nameof(signature));
			Signature = signature;
			PubKey = pubKey;
		}
	}
	public class FundingInformation
	{
		[JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
		public Money? TotalCollateral { get; set; }
		public PubKeyObject? PubKeys { get; set; }
		public FundingInput[]? FundingInputs { get; set; }
		public BitcoinAddress? ChangeAddress { get; set; }

		public DiscretePayoffs ToDiscretePayoffs(ContractInfo[] contractInfo)
		{
			if (contractInfo is null || contractInfo.Length is 0)
				throw new ArgumentException("contractInfo is required", nameof(contractInfo));
			DiscretePayoffs payoffs = new DiscretePayoffs();
			foreach (var ci in contractInfo)
			{
				payoffs.Add(new DiscretePayoff(ci.Outcome, ci.Payout - TotalCollateral));
			}
			return payoffs;
		}
	}

}
