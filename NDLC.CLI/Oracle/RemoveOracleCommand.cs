using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class RemoveOracleCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var oracleName = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.ToLowerInvariant().Trim();
			if (oracleName is null)
				throw new CommandOptionRequiredException("name");
			if (!await Repository.RemoveOracle(oracleName))
				throw new CommandException("name", "The oracle does not exists");
		}
	}
}
