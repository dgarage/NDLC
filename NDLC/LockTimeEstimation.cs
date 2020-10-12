using System;
using NBitcoin;

namespace NDLC
{
	public class LockTimeEstimation
	{
		int KnownBlock = 648085;
		DateTimeOffset KnownDate = Utils.UnixTimeToDateTime(1599999529);
		private readonly LockTime lockTime;
		public bool UnknownEstimation { get; set; }
		public LockTimeEstimation(LockTime lockTime, Network network)
		{
			this.lockTime = lockTime;
			if (lockTime.IsHeightLock)
			{
				int currentEstimatedBlock = KnownBlock + (int)network.Consensus.GetExpectedBlocksFor(DateTimeOffset.UtcNow - KnownDate);
				EstimatedRemainingBlocks = Math.Max(0, lockTime.Height - currentEstimatedBlock);
				EstimatedRemainingTime = network.Consensus.GetExpectedTimeFor(EstimatedRemainingBlocks);
			}
			else
			{
				EstimatedRemainingTime = lockTime.Date - DateTimeOffset.UtcNow;
				if (EstimatedRemainingTime < TimeSpan.Zero)
					EstimatedRemainingTime = TimeSpan.Zero;
				EstimatedRemainingBlocks = (int)network.Consensus.GetExpectedBlocksFor(EstimatedRemainingTime);
			}
			UnknownEstimation = network != Network.Main;
		}

		public int EstimatedRemainingBlocks { get; set; }
		public TimeSpan EstimatedRemainingTime { get; set; }

		public override string ToString()
		{
			if (lockTime == Constants.NeverLockTime)
				return "Never";
			if (EstimatedRemainingTime == TimeSpan.Zero || lockTime == 0)
				return "Immediate";
			if (UnknownEstimation)
			{
				if (lockTime.IsHeightLock)
				{
					return $"At block {lockTime.Height}";
				}
				else
				{
					return $"At date {lockTime.Date:f}";
				}
			}
			else
			{
				return $"{TimeString(EstimatedRemainingTime)} (More or less 5 days)";
			}
		}
		public static string TimeString(TimeSpan timeSpan)
		{
			return $"{(int)timeSpan.TotalDays} day{Plural((int)timeSpan.TotalDays)}";
		}
		private static string Plural(int totalDays)
		{
			return totalDays > 1 ? "s" : string.Empty;
		}
	}
}
