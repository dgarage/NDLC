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

		private static void PrintPnL(InvocationContext context, IEnumerable<ProfitAndLoss> pnl)
		{
			foreach (var item in pnl)
			{
				var type = item.IsHash ? "PreImage" : "Message";
				context.Console.Out.WriteLine($"\t{item.Value.ToString(true, false)} <= {type}({item.Outcome})");
			}
		}
	}
}
