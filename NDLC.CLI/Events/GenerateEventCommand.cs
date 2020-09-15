using NBitcoin;
using NDLC.Messages;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine;
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
		public static Command CreateCommand()
		{
			Command command = new Command("generate", "Generate a new event");
			command.Add(new Argument<string>("eventfullname", "The event full name, in format 'oraclename/name'")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("outcomes", "The outcomes, as specified by the oracle")
			{
				Arity = ArgumentArity.OneOrMore
			});
			command.Handler = new GenerateEventCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			EventFullName evt = context.GetEventName();
			if (await TryGetEvent(evt) is Event)
				throw new CommandException("eventfullname", "This event already exists");
			var outcomes = context.GetOutcomes();
			var oracle = await this.GetOracle("eventfullname", evt.OracleName);
			if (oracle.RootedKeyPath is null)
				throw new CommandException("eventfullname", "You do not own the keys of this oracle");

			var k = await Repository.CreatePrivateKey();
			var nonce = k.PrivateKey.ToECPrivKey().CreateSchnorrNonce();
			var evtId = new OracleInfo(oracle.PubKey!, nonce);
			if (!await Repository.AddEvent(evtId, outcomes, k.KeyPath))
				throw new CommandException("eventfullname", "This event already exists");
			await NameRepository.AsEventRepository().SetMapping(evtId, evt.Name);
			context.Console.Out.Write(nonce.ToString());
		}
	}
}
