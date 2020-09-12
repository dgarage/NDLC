using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.Events
{
	class AddEventCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			EventFullName evt = context.GetEventName();
			var rNonce = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("nonce")?.ToLowerInvariant().Trim();
			if (rNonce is null)
				throw new CommandOptionRequiredException("nonce");
			if (!SchnorrNonce.TryParse(rNonce, out var nonce) || nonce is null)
				throw new CommandException("nonce", "Invalid nonce");
			var outcomes = context.GetOutcomes();
			if (!await Repository.OracleExists(evt.OracleName))
				throw new CommandException("eventfullname", "The specified oracle do not exists");
			if (!await Repository.AddEvent(evt, nonce, outcomes.ToArray()))
				throw new CommandException("eventfullname", "The specified event already exists");
		}
	}
}
