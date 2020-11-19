using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using NDLC.TLV;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDLC.Messages
{
	public class Accept : FundingInformation, ITLVObject
	{
		[JsonProperty(Order = 100)]
		public CetSigs? CetSigs { get; set; }
		[JsonProperty(Order = 101)]
		public string? EventId { get; set; }

		public uint256? TemporaryContractId { get; set; }

		public byte[] ToTLV()
		{
			var ms = new MemoryStream();
			TLVWriter writer = new TLVWriter(ms);
			WriteTLV(writer);
			return ms.ToArray();
		}

		public const int TLVType = 42780;
		public void WriteTLV(TLVWriter writer)
		{
			if (TemporaryContractId is null)
				throw new InvalidOperationException($"{nameof(TemporaryContractId)} is not set");
			if (TotalCollateral is null)
				throw new InvalidOperationException($"{nameof(TotalCollateral)} is not set");
			if (PubKeys?.FundingKey is null)
				throw new InvalidOperationException($"{nameof(PubKeys.FundingKey)} is not set");
			if (PubKeys?.PayoutAddress is null)
				throw new InvalidOperationException($"{nameof(PubKeys.PayoutAddress)} is not set");
			if (FundingInputs is null)
				throw new InvalidOperationException($"{nameof(FundingInputs)} is not set");
			if (ChangeAddress is null)
				throw new InvalidOperationException($"{nameof(ChangeAddress)} is not set");
			if (CetSigs is null)
				throw new InvalidOperationException($"{nameof(CetSigs)} is not set");
			writer.WriteU16(TLVType);
			writer.WriteUInt256(TemporaryContractId);
			writer.WriteU64((ulong)TotalCollateral.Satoshi);
			Span<byte> buf = stackalloc byte[64];
			PubKeys.FundingKey.Compress().ToBytes(buf, out _);
			writer.WriteBytes(buf.Slice(0, 33));
			writer.WriteScript(PubKeys.PayoutAddress.ScriptPubKey);
			writer.WriteU16((ushort)FundingInputs.Length);
			foreach (var input in FundingInputs)
			{
				input.WriteTLV(writer);
			}
			writer.WriteScript(ChangeAddress.ScriptPubKey);
			CetSigs.WriteTLV(writer);
		}
		public void ReadTLV(TLVReader reader, Network network)
		{
			if (reader.ReadU16() != TLVType)
				throw new FormatException("Invalid TLV type for accept");
			TemporaryContractId = reader.ReadUInt256();
			TotalCollateral = Money.Satoshis(reader.ReadU64());
			PubKeys = new PubKeyObject();
			var pk = new byte[33];
			reader.ReadBytes(pk);
			PubKeys.FundingKey = new PubKey(pk, true);
			PubKeys.PayoutAddress = reader.ReadScript().GetDestinationAddress(network);
			var inputLen = reader.ReadU16();
			FundingInputs = new FundingInput[inputLen];
			for (int i = 0; i < inputLen; i++)
			{
				FundingInputs[i] = FundingInput.ParseFromTLV(reader, network);
			}
			ChangeAddress = reader.ReadScript().GetDestinationAddress(network);
			CetSigs = CetSigs.ParseFromTLV(reader);
		}
		public static Accept ParseFromTLV(string hexOrBase64, Network network)
		{
			var bytes = HexEncoder.IsWellFormed(hexOrBase64) ? Encoders.Hex.DecodeData(hexOrBase64) : Encoders.Base64.DecodeData(hexOrBase64);
			var reader = new TLVReader(new MemoryStream(bytes));
			var accept = new Accept();
			accept.ReadTLV(reader, network);
			return accept;
		}
	}

	public class CetSigs
	{
		public AdaptorSignature[]? OutcomeSigs { get; set; }
		[JsonConverter(typeof(NBitcoin.JsonConverters.SignatureJsonConverter))]
		public ECDSASignature? RefundSig { get; set; }

		public const int AdaptorSigsTLVType = 42774;
		public void WriteTLV(TLVWriter writer)
		{
			if (OutcomeSigs is null)
				throw new InvalidOperationException("OutcomeSigs is null");
			if (RefundSig is null)
				throw new InvalidOperationException("RefundSig is null");
			using (var record = writer.StartWriteRecord(AdaptorSigsTLVType))
			{
				foreach (var sig in OutcomeSigs)
				{
					sig.WriteTLV(record);
				}
			}
			writer.WriteBytes(RefundSig.ToCompact());
		}

		public static CetSigs ParseFromTLV(TLVReader reader)
		{
			CetSigs cet = new CetSigs();
			using (var r = reader.StartReadRecord())
			{
				if (r.Type != AdaptorSigsTLVType)
					throw new FormatException("Invalid TLV type, expected adaptor sigs");
				List<AdaptorSignature> sigs = new List<AdaptorSignature>();
				while (!r.IsEnd)
				{
					sigs.Add(AdaptorSignature.ParseFromTLV(reader));
				}
				cet.OutcomeSigs = sigs.ToArray();
			}
			Span<byte> buf = stackalloc byte[64];
			reader.ReadBytes(buf);
			if (!ECDSASignature.TryParseFromCompact(buf, out var sig))
				throw new FormatException("Invalid DER signature");
			cet.RefundSig = sig;
			return cet;
		}
	}

	public class AdaptorSignature
	{
		public static bool TryParse(string str, out AdaptorSignature? result)
		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			var bytes = Encoders.Hex.DecodeData(str);
			if (bytes.Length == 65 + 97 &&
				SecpECDSAAdaptorSignature.TryCreate(bytes.AsSpan().Slice(0, 65), out var sig) &&
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

		public void WriteTLV(TLVWriter writer)
		{
			Span<byte> output = stackalloc byte[65 + 97];
			Signature.WriteToSpan(output);
			Proof.WriteToSpan(output.Slice(65));
			writer.WriteBytes(output);
		}

		public static AdaptorSignature ParseFromTLV(TLVReader reader)
		{
			Span<byte> output = stackalloc byte[65 + 97];
			reader.ReadBytes(output);
			if (!SecpECDSAAdaptorSignature.TryCreate(output.Slice(0, 65), out var sig))
				throw new FormatException("Invalid adaptor signature");
			if (!SecpECDSAAdaptorProof.TryCreate(output.Slice(65), out var proof))
				throw new FormatException("Invalid adaptor proof");
			return new AdaptorSignature(sig, proof);
		}
	}
}
