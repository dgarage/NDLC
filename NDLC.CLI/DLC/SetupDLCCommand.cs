using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository.DLCState;

namespace NDLC.CLI.DLC
{
	class SetupDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("setup", "Setup the DLC. If you are the offer, this command will output a message you need to pass to the other party.");
			command.Add(new Option<bool>("--json", "Output the message in json instead of Base64"));
			command.Add(new Argument<string>("name", "The name of the DLC"));
			command.Add(new Argument<string>("setuppsbt", "A PSBT spending your collateral of the DLC to yourself. The output receiving your collateral will be the address receiving the reward when the DLC is settled."));
			command.Handler = new SetupDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");

			var dlc = await GetDLC("name", name);
			if (dlc is null)
				throw new CommandException("name", "This DLC does not exist");

			var psbt = context.ParsePSBT("setuppsbt", Network);

			var builder = dlc.GetBuilder(Network);
			if (builder.State.IsInitiator)
			{
				context.AssertState("name", dlc, true, DLCNextStep.Setup, Network);
				var key = await Repository.CreatePrivateKey();
				try
				{
					var offer = builder.FundOffer(key.PrivateKey, psbt);
					dlc.FundKeyPath = key.KeyPath;
					dlc.Abort = psbt;
					dlc.BuilderState = builder.ExportStateJObject();
					dlc.Offer = offer;
					await Repository.ChangeDLCId(dlc.Id, offer.GetTemporaryContractId());
					await NameRepository.AsDLCNameRepository().SetMapping(name, offer.GetTemporaryContractId());
					dlc.Id = offer.GetTemporaryContractId();
					await Repository.SaveDLC(dlc);
					context.WriteObject(offer, Repository.JsonSettings);
				}
				catch (InvalidOperationException err)
				{
					throw new CommandException("fundpsbt", err.Message);
				}
			}
			else
			{
				context.AssertState("name", dlc, false, DLCNextStep.Setup, Network);
				var k = await Repository.CreatePrivateKey();
				var accept = builder.FundAccept(k.PrivateKey, psbt);
				dlc.FundKeyPath = k.KeyPath;
				dlc.Abort = psbt;
				dlc.BuilderState = builder.ExportStateJObject();
				dlc.Accept = accept;
				if (builder.State.ContractId is null)
					throw new InvalidOperationException("The contractId of the builder should be set");
				await Repository.ChangeDLCId(dlc.Id, builder.State.ContractId);
				await NameRepository.AsDLCNameRepository().SetMapping(name, builder.State.ContractId);
				dlc.Id = builder.State.ContractId;
				await Repository.SaveDLC(dlc);
				context.WriteObject(accept, Repository.JsonSettings);
			}
		}
	}
}
