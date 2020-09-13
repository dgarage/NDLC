using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI
{
	class Constants
	{
		public static readonly LockTime NeverLockTime = new LockTime(500000000 - 1);
	}
}
