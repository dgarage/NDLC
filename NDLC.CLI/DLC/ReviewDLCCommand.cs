using NBitcoin;
using NDLC.CLI.Events;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;
using static NDLC.OfferReview;

namespace NDLC.CLI.DLC
{
	public class ReviewDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("review", "Review an offer before accepting it. This command, if successful, show a human readable summary of the DLC.");
			command.Add(new Argument<string>("offer")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Handler = new ReviewDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var offer = context.GetOffer(Repository.JsonSettings);
			Console.WriteLine($" Offer is {offer}");
			if (offer.OracleInfo is null)
				throw new CommandException("offer", "Missing oracleInfo");
			if (offer.Timeouts is null)
				throw new CommandException("offer", "Missing timeouts");
			if (offer.ContractInfo is null)
				throw new CommandException("offer", "Missing contractInfos");
			var oracle = await Repository.GetOracle(offer.OracleInfo.PubKey);
			var oracleName = await NameRepository.GetName(Scopes.Oracles, new OracleId(offer.OracleInfo.PubKey).ToString());
			if (oracle is null || oracleName is null)
				throw new CommandException("offer", "Unknown oracle");

			var evt = await Repository.GetEvent(offer.OracleInfo.PubKey, offer.OracleInfo.RValue);
			if (evt?.EventId is null)
				throw new CommandException("offer", "Unknown event");

			var maturity = new LockTimeEstimation(offer.Timeouts.ContractMaturity, Network);
			var refund = new LockTimeEstimation(offer.Timeouts.ContractTimeout, Network);
			if (!refund.UnknownEstimation)
			{
				if (refund.EstimatedRemainingBlocks == 0)
					throw new CommandException("offer", "The refund should not be immediately valid");
				if (refund.EstimatedRemainingBlocks < maturity.EstimatedRemainingBlocks)
					throw new CommandException("offer", "The refund should not be valid faster than the contract execution transactions");
			}
			if (!offer.SetContractPreimages(evt.Outcomes))
				throw new CommandException("offer", "The contract info of the offer does not match the specification of the event");
			try
			{
				var review = new OfferReview(offer);
				var evtName = (await NameRepository.AsEventRepository().ResolveName(evt.EventId)) ?? new EventFullName("???", "???");
				context.Console.Out.WriteLine($"Event: {evtName}");
				context.Console.Out.WriteLine($"The payoff function if you accept:");
				PrintPayoffs(context, review.AcceptorPayoffs);
				context.Console.Out.WriteLine($"Your expected collateral: {review.AcceptorCollateral.ToString(false, false)}");
				context.Console.Out.WriteLine($"Contract Execution validity: " + maturity.ToString());
				context.Console.Out.WriteLine($"Refund validity: " + refund.ToString());
				context.Console.Out.WriteLine($"How to accept this offer:");
				context.Console.Out.WriteLine($"If your plan to accept this offer, you need to run 'dlc accept <dlcname> <offer>'. The DLC name is arbitrary and local, you will use it to manage your DLC.");
			}
			catch (Exception ex)
			{
				throw new CommandException("offer", $"Invalid offer. ({ex.Message})");
			}
		}
		
		private static void PrintPayoffs(InvocationContext context, IEnumerable<Payoff> pnl)
		{
			foreach (var item in pnl)
			{
				context.Console.Out.WriteLine($"\t{item.Reward.ToString(true, false)} <= {item.Outcome}");
			}
		}
	}
}
