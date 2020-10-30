using NBitcoin;
using NDLC.CLI.Events;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository;

namespace NDLC.CLI.DLC
{
	public class OfferDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("offer", "Offer a new DLC");
			command.Add(new Option<uint>("--cetlocktime", "The locktime of the CET transaction (Default: 0, the CET is valid as soon as it can be signed)"));
			command.Add(new Option<uint>("--refundlocktime", "The locktime of the CET transaction (Default: 499999999, the refund will never be valid)"));
			command.Add(new Argument<string>("name", "The local name given to this DLC")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("eventfullname", "The full name of the event")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("payoff", "The payoffs in the format 'outcome:reward' or 'outcome:-loss'")
			{
				Arity = ArgumentArity.OneOrMore
			});
			command.Handler = new OfferDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");
			if (await this.TryGetDLC(name) != null)
				throw new CommandException("name", "This DLC already exists");
			EventFullName evtName = context.GetEventName();
			var oracle = await GetOracle("eventfullname", evtName.OracleName);
			if (oracle?.PubKey is null)
				throw new CommandException("eventfullname", "The specified oracle does not exists");
			var evt = await GetEvent("eventfullname", evtName);
			var payoffsStr = context.ParseResult.CommandResult.GetArgumentValueOrDefault<List<string>>("payoff");
			if (payoffsStr is null || payoffsStr.Count == 0)
				throw new CommandOptionRequiredException("payoff");
			var payoffs = CreatePayoffs(payoffsStr);
			FixCasing(evt, payoffs);
			var builder = new DLCTransactionBuilder(true, null, null, null, Network);

			var timeout = new Timeouts()
			{
				ContractMaturity = 0,
				ContractTimeout = Constants.NeverLockTime
			};
			if (context.ParseResult.HasOption("cetlocktime"))
			{
				timeout.ContractMaturity = new LockTime(context.ParseResult.ValueForOption<uint>("cetlocktime"));
			}
			if (context.ParseResult.HasOption("refundlocktime"))
			{
				timeout.ContractTimeout = new LockTime(context.ParseResult.ValueForOption<uint>("refundlocktime"));
			}
			var collateral = payoffs.CalculateMinimumCollateral();
			builder.Offer(oracle.PubKey, evt.EventId!.RValue, payoffs, timeout);
			var dlc = await Repository.NewDLC(evt.EventId, builder);
			await NameRepository.AsDLCNameRepository().SetMapping(name, dlc.LocalId);
			context.Console.Out.Write($"Offer created, you now need to setup the DLC sending {collateral} BTC to yourself. For more information, run `dlc show \"{name}\"`.");
		}

		private DiscretePayoffs CreatePayoffs(List<string> payoffs)
		{
			var result = new DiscretePayoffs();
			foreach (var payoff in payoffs)
			{
				if (!DiscretePayoff.TryParse(payoff, out var o) || o is null)
					throw new CommandException("payoff", "The payoff can't be parsed");
				result.Add(o);
			}
			return result;
		}

		/// <summary>
		/// Fix case mistakes
		/// </summary>
		private static void FixCasing(Event evt, DiscretePayoffs payoffs)
		{
			HashSet<DiscreteOutcome> outcomes = new HashSet<DiscreteOutcome>();
			for (int i = 0; i < payoffs.Count; i++)
			{
				var outcomeString = payoffs[i].Outcome.OutcomeString?.Trim();
				if (outcomeString is null)
					throw new CommandException("payoff", "The payoff can't be parsed");
				var knownOutcome = evt.Outcomes.Select(o => new DiscreteOutcome(o))
												.FirstOrDefault(o => o.OutcomeString!.Equals(outcomeString, StringComparison.OrdinalIgnoreCase));
				if (knownOutcome is null)
					throw new CommandException("payoff", $"This outcome {outcomeString} is not part of the event");
				outcomes.Add(knownOutcome);
				payoffs[i] = new DiscretePayoff(knownOutcome, payoffs[i].Reward);
			}

			if (outcomes.Count != evt.Outcomes.Length)
			{
				throw new CommandException("payoff", $"You did not specified the reward of all outcomes of the event");
			}
		}
	}
}
