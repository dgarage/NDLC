﻿using NBitcoin;
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
		public static DiscretePayoffs CreateFromContractInfo(ContractInfo[] contractInfos, Money collateral, DiscreteOutcome[]? preimages = null)
		{
			var hashsetPreimages = (preimages ?? Array.Empty<DiscreteOutcome>()).ToHashSet();
			var payoffs = new DiscretePayoffs();
			foreach (var i in contractInfos)
			{
				if (i.Outcome is DiscreteOutcome && i.Payout is Money)
				{
					hashsetPreimages.TryGetValue(i.Outcome, out var preimage);
					payoffs.Add(preimage ?? i.Outcome, i.Payout - collateral);
				}
			}
			return payoffs;
		}

		public static bool TryParse(string str, out DiscretePayoffs? result)
		{
			result = null;
			if (string.IsNullOrEmpty(str))
				return false;
			return TryParse(str.Split(",").Select(x => x.Trim()).ToList(), out result);
		}

		public static bool TryParse(IList<string> payoffStrs, out DiscretePayoffs? result)
		{
			result = new DiscretePayoffs();
			if (!payoffStrs.Any())
				return false;
			foreach (var s in payoffStrs)
			{
				if (string.IsNullOrEmpty(s))
					return false;

				if (!DiscretePayoff.TryParse(s, out var o) || o is null)
					return false;
				result.Add(o);
			}
			return true;
		}

		List<DiscretePayoff> _Outcomes = new List<DiscretePayoff>();
		Dictionary<DiscreteOutcome, Money> _Rewards = new Dictionary<DiscreteOutcome, Money>();
		public Money CalculateMinimumCollateral()
		{
			Money collateral = Money.Zero;
			foreach (var pnl in this)
			{
				collateral = Money.Max(collateral, -pnl.Reward);
			}
			return collateral;
		}

		public void Add(DiscreteOutcome outcome, Money reward)
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

		public ContractInfo[] ToContractInfo(Money collateral)
		{
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

		public bool TryGetValue(DiscreteOutcome outcome, out Money payout)
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
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				var oldPayoff = _Outcomes[index];
				_Outcomes[index] = value;
				_Rewards.Remove(oldPayoff.Outcome);
				_Rewards.Add(value.Outcome, value.Reward);
			}
		}
	}
	public class DiscretePayoff
	{
		public static bool TryParse(string str, out DiscretePayoff? payoff)
		{
			payoff = null;
			str = str.Trim();
			var i = str.LastIndexOf(':');
			if (i == -1)
				return false;

			var outcome = str.Substring(0, i);
			var reward = str.Substring(i + 1);
			if (!Money.TryParse(reward, out var btc) || btc is null)
				return false;
			payoff = new DiscretePayoff(new DiscreteOutcome(outcome), reward);
			return true;
		}
		public DiscretePayoff(string outcome, Money reward) :
			this(new DiscreteOutcome(outcome), reward)
		{

		}
		public DiscretePayoff(DiscreteOutcome outcome, Money reward)
		{
			if (outcome.OutcomeString is null)
				throw new ArgumentException("OutcomeString is not available", nameof(outcome));
			Reward = reward;
			Outcome = outcome;
		}
		public Money Reward { get; }
		public DiscreteOutcome Outcome { get; }
		public override string ToString()
		{
			return $"{Outcome.OutcomeString!}:{Reward.ToString(false, true)}";
		}
	}
}
