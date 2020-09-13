using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.Events
{
	class AddEventCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("add", "Add a new event");
			command.Add(new Argument<string>("eventfullname", "The event full name, in format 'oraclename/name'")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("nonce", "The event nonce, as specified by the oracle")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("outcomes", "The outcomes, as specified by the oracle")
			{
				Arity = ArgumentArity.OneOrMore
			});
			command.Handler = new AddEventCommand();
			return command;
		}
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
