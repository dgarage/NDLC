using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NDLC
{
	public class DiscretePayoffs : IEnumerable<DiscretePayoff>, IList<DiscretePayoff>
	{
		public static DiscretePayoffs CreateFromContractInfo(ContractInfo[] contractInfos, Money collateral)
		{
			var payoffs = new DiscretePayoffs();
			foreach (var i in contractInfos)
			{
				if (i.Outcome is DLCOutcome && i.Payout is Money)
				{
					payoffs.Add(i.Outcome, i.Payout - collateral);
				}
			}
			return payoffs;
		}

		List<DiscretePayoff> _Outcomes = new List<DiscretePayoff>();
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
			this.Add(new DiscretePayoff(outcome, reward));
		}
		public void Add(DiscretePayoff outcome)
		{
			if (_Rewards.ContainsKey(outcome.Outcome))
				throw new ArgumentException("Duplicate outcome", nameof(outcome));
			_Outcomes.Add(outcome);
			_Rewards.Add(outcome.Outcome, outcome.Reward);
		}

		public DiscretePayoffs Inverse()
		{
			var outcomes = new DiscretePayoffs();
			foreach (var pnl in this)
			{
				outcomes.Add(new DiscretePayoff(pnl.Outcome, -pnl.Reward));
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

		public IEnumerator<DiscretePayoff> GetEnumerator()
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

		int IList<DiscretePayoff>.IndexOf(DiscretePayoff item)
		{
			throw new NotImplementedException();
		}

		void IList<DiscretePayoff>.Insert(int index, DiscretePayoff item)
		{
			throw new NotImplementedException();
		}

		void IList<DiscretePayoff>.RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			_Rewards.Clear();
			_Outcomes.Clear();
		}

		bool ICollection<DiscretePayoff>.Contains(DiscretePayoff item)
		{
			throw new NotImplementedException();
		}

		void ICollection<DiscretePayoff>.CopyTo(DiscretePayoff[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		bool ICollection<DiscretePayoff>.Remove(DiscretePayoff item)
		{
			throw new NotImplementedException();
		}

		public int Count => _Rewards.Count;

		public bool IsReadOnly => false;

		public DiscretePayoff this[int index]
		{
			get { return _Outcomes[index]; }
			set { _Outcomes[index] = value; }
		}
	}
	public class DiscretePayoff
	{
		public DiscretePayoff(DLCOutcome outcome, Money reward)
		{
			Reward = reward;
			Outcome = outcome;
		}
		public Money Reward { get; }
		public DLCOutcome Outcome { get; }
	}
}
