using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using System.Text;
using NDLC.Infrastructure;

namespace NDLC.CLI.Events
{
	public static class EventHelpers
	{
		public static string[] GetOutcomes(this InvocationContext context)
		{
			var outcomes = context.ParseResult.CommandResult.GetArgumentValueOrDefault<List<string>>("outcomes");
			if (outcomes is null || outcomes.Count == 0)
				throw new CommandOptionRequiredException("outcomes");
			foreach (var outcome in outcomes)
			{
				if (outcome.Trim() != outcome)
					throw new CommandException("outcomes", "Superfluous whitespace around the outcome");
			}
			return outcomes.ToArray();
		}

		public static EventFullName GetEventName(this InvocationContext context)
		{
			var eventName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("eventfullname")?.Trim();
			if (eventName is null)
				throw new CommandOptionRequiredException("eventfullname");
			if (!EventFullName.TryParse(eventName, out EventFullName? evt) || evt is null)
				throw new CommandException("eventfullname", "Invalid event full name, should be in the form 'oracleName/eventName'");
			return evt;
		}
	}
}
