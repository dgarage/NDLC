using NBitcoin;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NDLC.CLI.Repository;

namespace NDLC.CLI.Events
{
	public class GenerateEventCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			EventFullName evt = context.GetEventName();
			if (await Repository.GetEvent(evt) is Event)
				throw new CommandException("eventfullname", "This event already exists");
			var outcomes = context.GetOutcomes();
			var oracle = await Repository.GetOracle(evt.OracleName);
			if (oracle is null)
				throw new CommandException("eventfullname", "This oracle does not exists");
			if (oracle.RootedKeyPath is null)
				throw new CommandException("eventfullname", "You do not own the keys of this oracle");


			var k = await Repository.CreatePrivateKey();
			var nonce = k.PrivateKey.ToECPrivKey().CreateSchnorrNonce();
			if (!await Repository.AddEvent(evt, nonce, outcomes, k.KeyPath))
				throw new CommandException("eventfullname", "This event already exists");
			context.Console.Out.Write(nonce.ToString());
		}
	}
}
