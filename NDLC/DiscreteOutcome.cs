using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NDLC
{
	public class DiscreteOutcome
	{
		public DiscreteOutcome(byte[] hash32)
		{
			if (hash32 == null)
				throw new ArgumentNullException(nameof(hash32));
			if (hash32.Length != 32)
				throw new ArgumentNullException("sha256 should be 32 bytes", nameof(hash32));
			Hash = hash32;
		}
		public DiscreteOutcome(string outcomeString)
		{
			if (outcomeString == null)
				throw new ArgumentNullException(nameof(outcomeString));
			OutcomeString = outcomeString;
			Hash = Hashes.SHA256(Encoding.UTF8.GetBytes(outcomeString));
		}

		public static bool TryParse(string str, out DiscreteOutcome? outcome)
		{
			outcome = null;
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			if (str.StartsWith("MSG:"))
			{
				outcome = new DiscreteOutcome(str.Substring(4));
				return true;
			}
			else if (str.StartsWith("SHA256:"))
			{
				try
				{
					outcome = new DiscreteOutcome(Encoders.Hex.DecodeData(str.Substring(7)));
					return true;
				}
				catch 
				{
					return false;
				}
			}
			return false;
		}

		public string? OutcomeString { get; }
		public byte[] Hash
		{
			get;
		}

		public override bool Equals(object obj)
		{
			DiscreteOutcome? item = obj as DiscreteOutcome;
			if (item is null)
				return false;
			return Hash.AsSpan().SequenceCompareTo(item.Hash) == 0;
		}
		public static bool operator ==(DiscreteOutcome? a, DiscreteOutcome? b)
		{
			if (System.Object.ReferenceEquals(a, b))
				return true;
			if ((a is null) || (b is null))
				return false;
			return a.Hash.AsSpan().SequenceCompareTo(b.Hash) == 0;
		}

		public static bool operator !=(DiscreteOutcome? a, DiscreteOutcome? b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			var longArray = MemoryMarshal.Cast<byte, ulong>(Hash);
			return HashCode.Combine(longArray[0], longArray[1], longArray[2], longArray[3]);
		}

		public static implicit operator DiscreteOutcome(string outcomeString)
		{
			return new DiscreteOutcome(outcomeString);
		}
		public override string ToString()
		{
			if (OutcomeString is string)
				return $"MSG:{OutcomeString}";
			else
				return $"SHA256:{Encoders.Hex.EncodeData(Hash)}";
		}
	}
}
