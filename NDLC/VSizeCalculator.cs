using NBitcoin;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using NDLC.Messages;

namespace NDLC
{
	public class VSizeCalculator
	{
		public VSizeCalculator()
		{

		}
		public VSizeCalculator(FundingInformation o)
		{
			FromFundingInformation(o);
		}
		public void FromFundingInformation(FundingInformation o)
		{
			PayoutLength = o.PubKeys?.PayoutAddress?.ScriptPubKey.Length ?? 0;
			ChangeLength = o.ChangeAddress?.ScriptPubKey.Length ?? 0;
			foreach (var input in o.FundingInputs ?? Array.Empty<FundingInput>())
			{
				InputSize s = new InputSize();
				if (input.MaxWitnessLength is null)
					input.SetRecommendedMaxWitnessLength();
				s.MaxWitnessLength = input.MaxWitnessLength!.Value;
				s.ScriptSigLength = input.RedeemScript is null ? 0 
										: new Script(Op.GetPushOp(input.RedeemScript.ToBytes(true))).Length;
				Inputs.Add(s);
			}
		}

		public class InputSize
		{
			public int ScriptSigLength { get; set; }
			public int MaxWitnessLength { get; set; }
		}
		public int PayoutLength { get; set; }
		public int ChangeLength { get; set; }

		public List<InputSize> Inputs { get; set; } = new List<InputSize>();
		public VSizes Calculate()
		{
			// input_weight = sum(164 + 4*script_sig_len + max_witness_len) (over party's funding inputs)
			var input_weight = Inputs.Select(c => 164 + 4 * c.ScriptSigLength + c.MaxWitnessLength).Sum();
			// output_weight = 36 + 4*change_spk_script length
			var output_weight = 36 + 4 * ChangeLength;
			var total_funding_weight = 107 + output_weight + input_weight;
			var total_cet_weight = 249 + 4 * PayoutLength;
			return new VSizes()
			{
				Funding = (total_funding_weight + 3) / 4,
				CET = (total_cet_weight + 3) / 4
			};
		}
	}
}
