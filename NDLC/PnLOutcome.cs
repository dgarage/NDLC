using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NDLC
{
	public class PnLOutcomes : IEnumerable<PnLOutcome>, IList<PnLOutcome>
	{
		List<PnLOutcome> _Outcomes = new List<PnLOutcome>();
		Dictionary<DLCOutcome, Money> _Rewards = new Dictionary<DLCOutcome, Money>();
		public Money CalculateCollateral()
		{
			Money collateral = Money.Zero;
			foreach (var pnl in this)
			{
				collateral = Money.Max(collateral, -pnl.Reward);
			}
			return collateral;
		}

		public void Add(DLCOutcome outcome, Money reward)
		{
			this.Add(new PnLOutcome(outcome, reward));
		}
		public void Add(PnLOutcome outcome)
		{
			if (_Rewards.ContainsKey(outcome.Outcome))
				throw new ArgumentException("Duplicate outcome", nameof(outcome));
			_Outcomes.Add(outcome);
			_Rewards.Add(outcome.Outcome, outcome.Reward);
		}

		public PnLOutcomes Inverse()
		{
			var outcomes = new PnLOutcomes();
			foreach (var pnl in this)
			{
				outcomes.Add(new PnLOutcome(pnl.Outcome, -pnl.Reward));
			}
			return outcomes;
		}

		public ContractInfo[] ToContractInfo()
		{
			var collateral = CalculateCollateral();
			return this
				.Select(o => new ContractInfo(o.Outcome, collateral + o.Reward))
				.ToArray();
		}

		public IEnumerator<PnLOutcome> GetEnumerator()
		{
			return this._Outcomes.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool TryGetValue(DLCOutcome outcome, out Money payout)
		{
			return _Rewards.TryGetValue(outcome, out payout);
		}

		int IList<PnLOutcome>.IndexOf(PnLOutcome item)
		{
			throw new NotImplementedException();
		}

		void IList<PnLOutcome>.Insert(int index, PnLOutcome item)
		{
			throw new NotImplementedException();
		}

		void IList<PnLOutcome>.RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			_Rewards.Clear();
			_Outcomes.Clear();
		}

		bool ICollection<PnLOutcome>.Contains(PnLOutcome item)
		{
			throw new NotImplementedException();
		}

		void ICollection<PnLOutcome>.CopyTo(PnLOutcome[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		bool ICollection<PnLOutcome>.Remove(PnLOutcome item)
		{
			throw new NotImplementedException();
		}

		public int Count => _Rewards.Count;

		public bool IsReadOnly => false;

		public PnLOutcome this[int index]
		{
			get { return _Outcomes[index]; }
			set { _Outcomes[index] = value; }
		}
	}
	public class PnLOutcome
	{
		public PnLOutcome(DLCOutcome outcome, Money reward)
		{
			Reward = reward;
			Outcome = outcome;
		}
		public Money Reward { get; }
		public DLCOutcome Outcome { get; }
	}
}
