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
using static NDLC.CLI.Repository;

namespace NDLC.CLI.DLC
{
	public class GenerateDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("generate", "Generate a new DLC");
			command.Add(new Argument<string>("name", "The name of this DLC")
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
			command.Handler = new GenerateDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");
			EventFullName evtName = context.GetEventName();
			var oracle = await Repository.GetOracle(evtName.OracleName);
			if (oracle?.PubKey is null)
				throw new CommandException("eventfullname", "The specified oracle does not exists");
			var evt = await Repository.GetEvent(evtName);
			if (evt?.Nonce is null)
				throw new CommandException("eventfullname", "The specified event does not exists");
			var payoffsStr = context.ParseResult.CommandResult.GetArgumentValueOrDefault<List<string>>("payoff");
			if (payoffsStr is null || payoffsStr.Count == 0)
				throw new CommandOptionRequiredException("payoff");
			var payoffs = CreatePayoffs(payoffsStr);
			FixCasing(evt, payoffs);
			var builder = new DLCTransactionBuilder(true, null, null, null, Network);
			builder.Offer(oracle.PubKey, evt.Nonce, payoffs, new Timeouts()
			{
				ContractMaturity = 0,
				ContractTimeout = 0
			});
			await Repository.NewDLC(name, new OracleInfo(oracle.PubKey, evt.Nonce), builder.ExportState());
		}

		private DiscretePayoffs CreatePayoffs(List<string> payoffs)
		{
			var result = new DiscretePayoffs();
			foreach (var payoff in payoffs)
			{
				if (!Helpers.TryParse(payoff, out var o) || o is null)
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
				var knownOutcome = evt.Outcomes.FirstOrDefault(o => o.Equals(outcomeString, StringComparison.OrdinalIgnoreCase));
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
