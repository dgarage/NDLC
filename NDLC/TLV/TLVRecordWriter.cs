using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NDLC.TLV
{
	public class TLVRecordWriter : TLVWriter, IDisposable
	{
		internal TLVRecordWriter(TLVWriter parent, ulong type) : base(new MemoryStream())
		{
			parent.WriteBigSize(type);
			Type = type;
			Parent = parent;
		}
		public TLVWriter Parent { get; }
		public ulong Type { get; }
		public long Size => Stream.Length;
		public void Dispose()
		{
			Parent.WriteBigSize((ulong)Size);
			Stream.Position = 0;
			Parent.WriteStream(Stream);
		}
	}
}
