using NBitcoin;

namespace NDLC
{
	public class Constants
	{
		public static readonly LockTime NeverLockTime = new LockTime(500000000 - 1);
	}
}
