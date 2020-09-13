using NBitcoin.DataEncoders;
using NDLC.TLV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace NDLC.Tests
{
	public class TLVTests
	{
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
	}
}
