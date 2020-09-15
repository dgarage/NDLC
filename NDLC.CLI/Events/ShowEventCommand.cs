using NBitcoin;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.Events
{
	public class ShowEventCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("show", "Show details of an event");
			command.Add(new Argument<string>("eventfullname", "The full name of the event"));
			command.Handler = new ShowEventCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var evtName = context.GetEventName();
			var evt = await Repository.GetEvent(evtName);
			if (evt is null)
				throw new CommandException("name", "Event not found");
			var oracle = await Repository.GetOracle(evtName.OracleName);
			if (oracle is null)
				throw new CommandException("name", "Event not found");
			context.Console.Out.WriteLine($"Full Name: {evtName}");
			context.Console.Out.WriteLine($"Name: {evtName.Name}");
			context.Console.Out.WriteLine($"Nonce: {evt.Nonce}");
			context.Console.Out.WriteLine($"Can reveal: {oracle.RootedKeyPath is RootedKeyPath}");
			int i = 0;
			foreach (var outcome in evt.Outcomes)
			{
				context.Console.Out.WriteLine($"Outcome[{i}]: {outcome}");
				i++;
			}
			if (evt.Attestations is Dictionary<string, Key>)
			{
				foreach(var kv in evt.Attestations)
				{
					context.Console.Out.WriteLine($"Attestation[\"{kv.Key}\"]: {kv.Value.ToHex()}");
				}
			}
		}
	}
}
