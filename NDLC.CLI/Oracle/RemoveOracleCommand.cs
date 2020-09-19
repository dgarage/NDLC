using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;

namespace NDLC.CLI
{
	public class RemoveOracleCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("remove", "Remove an oracle")
				{
					new Argument<string>("name", "The oracle name")
				};
			command.Handler = new RemoveOracleCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name");
			if (name is null)
				throw new CommandOptionRequiredException("name");
			var oracle = await GetOracle("name", name);
			await NameRepository.RemoveMapping(Scopes.Oracles, name);
			await Repository.RemoveOracle(oracle.PubKey);
		}
	}
}
