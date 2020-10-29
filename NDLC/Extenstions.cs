using NBitcoin;
using NBitcoin.Protocol;
using NDLC.TLV;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace NDLC
{
	public static class Extenstions
	{
		public static void WriteScript(this TLVWriter writer, Script? script)
		{
			if (script is null)
			{
				writer.WriteU16(0);
			}
			else
			{
				writer.WriteU16((ushort)script.Length);
				writer.WriteBytes(script.ToBytes(true));
			}
		}
		public static Script ReadScript(this TLVReader reader)
		{
			var size = reader.ReadU16();
			var buf = new byte[size];
			reader.ReadBytes(buf);
			return Script.FromBytesUnsafe(buf);
		}
	}
}
