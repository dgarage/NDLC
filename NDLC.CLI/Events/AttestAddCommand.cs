using System;
using System.Linq;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NDLC.Secp256k1;
using NBitcoin.Secp256k1;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.CommandLine;

namespace NDLC.CLI.Events
{
	public class AttestAddCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("add", "Add an attestation received by an oracle");
			command.Add(new Argument<string>("eventfullname", "The event full name, in format 'oraclename/name'")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("attestation", "The received attestation")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Handler = new AttestAddCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var attestation = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("attestation")?.Trim();
			if (attestation is null)
				throw new CommandOptionRequiredException("attestation");
			var bytes = Encoders.Hex.DecodeData(attestation);
			if (bytes.Length != 32)
				throw new CommandException("attestation", "The attestation must be 32 bytes");
			var attestationKey = new Key(bytes);
			EventFullName evt = context.GetEventName();
			var oracle = await Repository.GetOracle(evt.OracleName);
			if (oracle is null)
				throw new CommandException("eventfullname", "This oracle does not exists");
			var outcome = await Repository.AddAttestation(evt, attestationKey);
			if (outcome?.OutcomeString is null)
				throw new CommandException("attestation", "This attestation does not attest known outcomes");
			context.Console.Out.Write(outcome.OutcomeString);
		}
	}
}
