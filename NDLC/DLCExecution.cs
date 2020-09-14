using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC
{
	public class DLCExecution
	{
		public DLCExecution(Transaction cET, DiscreteOutcome outcome)
		{
			CET = cET;
			Outcome = outcome;
		}

		public Transaction CET { get; set; }
		public DiscreteOutcome Outcome { get; set; }
	}
}
