using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.Tests
{
    public class DLCFeeTestVector
    {
        public class OfferInput
        {
            public int redeemScriptLen { get; set; }
            public int maxWitnessLen { get; set; }
        }
        public class AcceptInput
        {
            public int redeemScriptLen { get; set; }
            public int maxWitnessLen { get; set; }
        }
        public class Inputs
        {
            public List<OfferInput> offerInputs { get; set; }
            public int offerPayoutSPKLen { get; set; }
            public int offerChangeSPKLen { get; set; }
            public List<AcceptInput> acceptInputs { get; set; }
            public int acceptPayoutSPKLen { get; set; }
            public int acceptChangeSPKLen { get; set; }
            public int feeRate { get; set; }
        }
        public Inputs inputs { get; set; }
        public int offerFundingFee { get; set; }
        public int offerClosingFee { get; set; }
        public int acceptFundingFee { get; set; }
        public int acceptClosingFee { get; set; }
    }
}
