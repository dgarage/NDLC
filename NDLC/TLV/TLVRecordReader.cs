using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.TLV
{
	public class TLVRecordReader : TLVReader, IDisposable
	{
		long _StartPosition;
		public long Length { get; }
		internal TLVRecordReader(TLVReader parent) : base(parent.Stream)
		{
			Parent = parent;
			Type = parent.ReadBigSize();
			Length = (long)parent.ReadBigSize();
			_StartPosition = parent.Stream.Position;
		}

		public bool IsEnd
		{
			get
			{
				return Parent.Stream.Position >= _StartPosition + Length;
			}
		}
		public TLVReader Parent { get; }
		public ulong Type { get; }

		public void Dispose()
		{
		}
	}
}
