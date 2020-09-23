using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NDLC.Messages.JsonConverters;
using NDLC.TLV;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NDLC.Messages
{
	public class Sign
	{
		public CetSigs? CetSigs { get; set; }

		[JsonConverter(typeof(NBitcoin.JsonConverters.ScriptJsonConverter))]
		public List<WitScript>? FundingSigs { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public uint256? ContractId { get; set; }
		public static Sign ParseFromTLV(string tlv, Network network)
		{
			var bytes = Encoders.Hex.DecodeData(tlv);
			var reader = new TLVReader(new MemoryStream(bytes));
			var sign = new Sign();
			sign.ReadTLV(reader, network);
			return sign;
		}

		const ushort TLVType = 42782;
		const ushort FundingSignaturesTLVType = 42776;

		private void WriteTLV(TLVWriter writer)
		{
			if (ContractId is null)
				throw new InvalidOperationException("ContractId is not set");
			writer.WriteU16(TLVType);
			writer.WriteUInt256(ContractId);
			CetSigs.WriteTLV(writer);
			using (var w = writer.StartWriteRecord(FundingSignaturesTLVType))
			{
				w.WriteU16(FundingSigs.Count);
				foreach (var s in FundingSigs)
				{
					w.WriteU16(s.PushCount);
					for (int i = 0; i < s.PushCount; i++)
					{
						var p = s.GetUnsafePush(i);
						w.WriteU16(p.Length);
						w.WriteBytes(p);
					}
				}
			}
		}
		private void ReadTLV(TLVReader reader, Network network)
		{
			if (reader.ReadU16() != TLVType)
				throw new FormatException("Invalid TLV type for sign");
			ContractId = reader.ReadUInt256();
			CetSigs = CetSigs.ParseFromTLV(reader);

			using (var r = reader.StartReadRecord())
			{
				if (r.Type != FundingSignaturesTLVType)
					throw new FormatException("Invalid TLV type for funding signatures");
				FundingSigs = new List<WitScript>();
				var witnesses = r.ReadU16();
				for (int i = 0; i < witnesses; i++)
				{
					var elementsCount = reader.ReadU16();
					var witnessBytes = new byte[elementsCount][];
					for (int y = 0; y < elementsCount; y++)
					{
						var elementSize = reader.ReadU16();
						witnessBytes[y] = new byte[elementSize];
						r.ReadBytes(witnessBytes[y]);
					}
					FundingSigs.Add(new WitScript(witnessBytes));
				}
			}
		}

		[JsonExtensionData]
		public Dictionary<string,JToken>? AdditionalData { get; set; }

		public byte[] ToTLV()
		{
			var ms = new MemoryStream();
			TLVWriter writer = new TLVWriter(ms);
			WriteTLV(writer);
			return ms.ToArray();
		}
	}
}
