using NBitcoin;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	public class OfferDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			var command = new Command("offer", "Make the offer")
			{
				new Argument<string>("name", "The name of the DLC"),
				new Argument<string>("setuppsbt", "A PSBT spending your collateral of the DLC to yourself. The output receiving your collateral will be the address receivng the reward when the DLC is settled."),
				new Option<bool>("--json", "Output the offer in json instead of Base64")
			};
			command.Handler = new OfferDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");

			var dlc = await Repository.GetDLC(name);
			if (dlc is null)
				throw new CommandException("name", "This DLC does not exist");
			if (dlc.FundKeyPath is RootedKeyPath || dlc.Offer is JObject)
				throw new CommandException("name", "This DLC already issued an offer");
			var builder = dlc.GetBuilder(Network);
			if (!builder.State.IsInitiator)
				throw new CommandException("name", "This command should be used by the offerer of the DLC");

			var psbt = context.ParsePSBT("setuppsbt", Network);
			var key = await Repository.CreatePrivateKey();
			try
			{
				var offer = builder.FundOffer(key.PrivateKey, psbt);
				offer.OffererContractId = dlc.Id;
				dlc.FundKeyPath = key.KeyPath;
				dlc.BuilderState = builder.ExportStateJObject();
				dlc.Offer = JObject.FromObject(offer, JsonSerializer.Create(Repository.JsonSettings));
				await Repository.SaveDLC(dlc);
				context.WriteObject(offer, Repository.JsonSettings);
			}
			catch (InvalidOperationException err)
			{
				throw new CommandException("fundpsbt", err.Message);
			}
		}
	}
}
