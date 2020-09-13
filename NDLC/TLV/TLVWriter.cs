using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NDLC.TLV
{
	public class TLVWriter
	{
		byte[] buffer = new byte[32];
		public TLVWriter(Stream stream)
		{
			Stream = stream;
		}

		public Stream Stream { get; }

		public void Flush()
		{
			Stream.Flush();
		}

		public void WriteU64(ulong v)
		{
			Utils.ToBytes(v, true, buffer);
			Stream.Write(buffer, 0, 8);
		}
		public void WriteU32(uint v)
		{
			Utils.ToBytes(v, true, buffer);
			Stream.Write(buffer, 0, 4);
		}
		public void WriteU16(ushort v)
		{
			buffer[0] = (byte)v;
			buffer[1] = (byte)(v >> 8);
			Stream.Write(buffer, 0, 2);
		}
		public void WriteByte(byte v)
		{
			Stream.WriteByte(v);
		}
		public void WriteBigSize(ulong v)
		{
			if (v < 0xfd)
			{
				Stream.WriteByte((byte)v);
			}
			else if (v < 0x10000)
			{
				buffer[0] = 0xfd;
				buffer[1] = (byte)(v >> 8);
				buffer[2] = (byte)(v);
				Stream.Write(buffer, 0, 3);
			}
			else if (v < 0x100000000)
			{
				buffer[0] = 0xfe;
				Utils.ToBytes((uint)v, false, buffer.AsSpan().Slice(1));
				Stream.Write(buffer, 0, 5);
			}
			else
			{
				buffer[0] = 0xff;
				Utils.ToBytes(v, false, buffer.AsSpan().Slice(1));
				Stream.Write(buffer, 0, 9);
			}
		}
	}
}
