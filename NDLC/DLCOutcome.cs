using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NDLC
{
	public class DLCOutcome
	{
		public DLCOutcome(byte[] hash32)
		{
			if (hash32 == null)
				throw new ArgumentNullException(nameof(hash32));
			if (hash32.Length != 32)
				throw new ArgumentNullException("sha256 should be 32 bytes", nameof(hash32));
			Hash = hash32;
		}
		public DLCOutcome(string outcomeString)
		{
			if (outcomeString == null)
				throw new ArgumentNullException(nameof(outcomeString));
			OutcomeString = outcomeString;
			Hash = Hashes.SHA256(Encoding.UTF8.GetBytes(outcomeString));
		}

		public string? OutcomeString { get; }
		public byte[] Hash
		{
			get;
		}

		public override bool Equals(object obj)
		{
			DLCOutcome? item = obj as DLCOutcome;
			if (item is null)
				return false;
			return Hash.AsSpan().SequenceCompareTo(item.Hash) == 0;
		}
		public static bool operator ==(DLCOutcome a, DLCOutcome b)
		{
			if (System.Object.ReferenceEquals(a, b))
				return true;
			if (((object)a == null) || ((object)b == null))
				return false;
			return a.Hash.AsSpan().SequenceCompareTo(b.Hash) == 0;
		}

		public static bool operator !=(DLCOutcome a, DLCOutcome b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			var longArray = MemoryMarshal.Cast<byte, ulong>(Hash);
			return HashCode.Combine(longArray[0], longArray[1], longArray[2], longArray[3]);
		}

		public static implicit operator DLCOutcome(string outcomeString)
		{
			return new DLCOutcome(outcomeString);
		}
		public override string ToString()
		{
			return OutcomeString ?? Encoders.Hex.EncodeData(Hash);
		}
	}
}
