using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class ReviewOfferCommand : CommandBase
	{
		protected override Task InvokeAsyncBase(InvocationContext context)
		{
			try
			{
				var human = context.ParseResult.ValueForOption<bool>("human");
				var offer = JsonConvert.DeserializeObject<Offer>(context.ParseResult.CommandResult.GetArgumentValueOrDefault("offer") as string ?? string.Empty, JsonSerializerSettings);
				if (offer is null)
					throw new CommandException("offer", "Invalid offer");
				var review = new OfferReview(offer);
				if (human)
				{
					context.Console.Out.WriteLine($"Oracle pubkey: {review.OraclePubKey}");
					context.Console.Out.WriteLine($"Event's nonce: {review.Nonce}");
					context.Console.Out.WriteLine($"Offerer PnL:");
					PrintPnL(context, review.OffererPnL);
					context.Console.Out.WriteLine($"Accepter PnL:");
					PrintPnL(context, review.AcceptorPnL);
					context.Console.Out.WriteLine($"Expected Acceptor Collateral: {review.AcceptorCollateral.ToString(false, false)}");
					context.Console.Out.Write($"You will be able to broadcast the contract transactions " + ToString(review.Timeouts.ContractMaturity));
					context.Console.Out.Write($"If the oracle disappears, you can be refunded " + ToString(review.Timeouts.ContractTimeout));
				}
				else
				{
					WriteObject(context, review);
				}
			}
			catch (Exception ex)
			{
				throw new CommandException("offer", $"Invalid offer ({ex.Message})");
			}
			return Task.CompletedTask;
		}

		private string ToString(LockTime locktime)
		{
			if (locktime.IsHeightLock)
			{
				return $"at block {locktime.Height}";
			}
			else
			{
				return $"the {locktime.Date:f}";
			}
		}

		private static void PrintPnL(InvocationContext context, IEnumerable<ProfitAndLoss> pnl)
		{
			foreach (var item in pnl)
			{
				var type = item.IsHash ? "PreImage" : "Message";
				context.Console.Out.WriteLine($"\t{item.Reward.ToString(true, false)} <= {type}({item.Outcome})");
			}
		}
	}
}
