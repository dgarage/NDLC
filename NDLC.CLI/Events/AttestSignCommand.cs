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

namespace NDLC.CLI.Events
{
	public class AttestSignCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var outcome = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("outcome")?.Trim();
			if (outcome is null)
				throw new CommandOptionRequiredException("outcome");
			EventFullName evt = context.GetEventName();
			var oracle = await Repository.GetOracle(evt.OracleName);
			if (oracle is null)
				throw new CommandException("name", "This oracle does not exists");
			if (oracle.RootedKeyPath is null)
				throw new CommandException("name", "You do not own the keys of this oracle");

			var discreteOutcome = new DiscreteOutcome(outcome);
			var evtObj = await Repository.GetEvent(evt);
			if (evtObj?.Nonce is null)
				throw new CommandException("name", "This event does not exists");
			if (evtObj?.NonceKeyPath is null)
				throw new CommandException("name", "You did not generated this event");
			outcome = evtObj.Outcomes.FirstOrDefault(o => o.Equals(outcome, StringComparison.OrdinalIgnoreCase));
			if (outcome is null)
				throw new CommandException("outcome", "This outcome does not exists in this event");
			var key = await Repository.GetKey(oracle.RootedKeyPath);
			var kValue = await Repository.GetKey(evtObj.NonceKeyPath);
			key.ToECPrivKey().TrySignBIP140DLC_FIX(discreteOutcome.Hash, new PrecomputedNonceFunctionHardened(kValue.ToECPrivKey().ToBytes()), out var sig);
			if (sig is null)
				throw new NotSupportedException("BUG this should never happen");
			var oracleAttestation = new Key(sig.s.ToBytes());
			if (await Repository.AddReveal(evt, oracleAttestation) != outcome)
				throw new InvalidOperationException("Error while validating reveal");
			context.Console.Out.Write(oracleAttestation.ToHex());
		}
	}
}
