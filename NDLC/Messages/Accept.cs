using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NDLC.Messages
{
	public class Accept : FundingInformation
	{
		[JsonProperty(Order = 100)]
		public CetSigs? CetSigs { get; set; }
		[JsonExtensionData]
		public Dictionary<string, JToken>? AdditionalData { get; set; }
	}

	public class CetSigs
	{
		[JsonConverter(typeof(OutcomeSigsJsonConverter))]
		public Dictionary<uint256, AdaptorSignature>? OutcomeSigs { get; set; }
		[JsonConverter(typeof(PartialSignatureJsonConverter))]
		public PartialSignature? RefundSig { get; set; }
	}

	public class AdaptorSignature
	{
		public static bool TryParse(string str, out AdaptorSignature? result)
		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			var bytes = Encoders.Hex.DecodeData(str);
			if (bytes.Length == 65 + 97 &&
				SecpECDSAAdaptorSignature.TryCreate(bytes, out var sig) &&
				SecpECDSAAdaptorProof.TryCreate(bytes.AsSpan().Slice(65), out var proof) &&
				sig is SecpECDSAAdaptorSignature && proof is SecpECDSAAdaptorProof)
			{
				result = new AdaptorSignature(sig, proof);
				return true;
			}
			result = null;
			return false;
		}
		public AdaptorSignature(SecpECDSAAdaptorSignature sig, SecpECDSAAdaptorProof proof)
		{
			if (sig == null)
				throw new ArgumentNullException(nameof(sig));
			if (proof == null)
				throw new ArgumentNullException(nameof(proof));
			Signature = sig;
			Proof = proof;
		}

		public SecpECDSAAdaptorSignature Signature { get; set; }
		public SecpECDSAAdaptorProof Proof { get; set; }

		public override string ToString()
		{
			Span<byte> output = stackalloc byte[65 + 97];
			Signature.WriteToSpan(output);
			Proof.WriteToSpan(output.Slice(65));
			return Encoders.Hex.EncodeData(output);
		}
	}
}
