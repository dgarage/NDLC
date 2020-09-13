using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.Events
{
	public class ListEventsCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("list", "List events");
			command.Add(new Option<string>("--oracle", "Filter events of this specific oracle")
			{
				Argument = new Argument<string>()
				{
					Arity = ArgumentArity.ExactlyOne
				},
				IsRequired = false
			});
			command.Handler = new ListEventsCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.ValueForOption<string>("oracle");
			foreach (var oracle in (await Repository.ListOracles()).OrderBy(o => o.Name))
			{
				if (oracleName is string && !oracle.Name.Equals(oracleName, StringComparison.OrdinalIgnoreCase))
					continue;
				foreach (var evts in (await Repository.ListEvents(oracle.Name)).OrderBy(o => o.Name))
				{
					var fullName = new EventFullName(oracle.Name, evts.Name);
					context.Console.Out.WriteLine(fullName.ToString());
				}
			}
		}
	}
}
