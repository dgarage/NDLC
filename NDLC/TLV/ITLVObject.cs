using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NDLC.TLV
{
	public interface ITLVObject
	{
		void WriteTLV(TLVWriter writer);
		void ReadTLV(TLVReader reader, Network network);
	}
}
