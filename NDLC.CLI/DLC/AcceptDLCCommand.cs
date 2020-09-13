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
	public class AcceptDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("accept", "Accept an offer");
			command.Add(new Argument<string>("name", "The local name given to this DLC")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("offer")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Add(new Argument<string>("psbt")
			{
				Arity = ArgumentArity.ExactlyOne
			});
			command.Handler = new AcceptDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var offer = DLCHelpers.GetOffer(context, Repository.JsonSettings);
			var name = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("name")?.Trim();
			if (name is null)
				throw new CommandOptionRequiredException("name");

			var psbtStr = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("psbt")?.Trim();
			if (psbtStr is null)
				throw new CommandOptionRequiredException("psbt");
			if (!PSBT.TryParse(psbtStr, Network, out var psbt) || psbt is null)
				throw new CommandException("psbt", "Invalid PSBT");
			if (await Repository.GetDLC(name) != null)
				throw new CommandException("name", "This DLC already exists");
			if (offer.OracleInfo is null)
				throw new CommandException("offer", "Missing oracleInfo");
			if (offer.Timeouts is null)
				throw new CommandException("offer", "Missing timeouts");
			if (offer.ContractInfo is null)
				throw new CommandException("offer", "Missing contractInfos");


			var oracle = await Repository.GetOracle(offer.OracleInfo.PubKey);
			if (oracle is null)
				throw new CommandException("offer", "Unknown oracle");
			var evt = await Repository.GetEvent(offer.OracleInfo.PubKey, offer.OracleInfo.RValue);
			if (evt is null)
				throw new CommandException("offer", "Unknown event");

			var maturity = new LockTimeEstimation(offer.Timeouts.ContractMaturity, Network);
			var refund = new LockTimeEstimation(offer.Timeouts.ContractTimeout, Network);
			if (!refund.UnknownEstimation)
			{
				if (refund.EstimatedRemainingBlocks == 0)
					throw new CommandException("offer", "The refund should not be immediately valid");
				if (refund.EstimatedRemainingBlocks < maturity.EstimatedRemainingBlocks)
					throw new CommandException("offer", "The refund should not be valid faster than the contract execution transactions");
			}

			DLCHelpers.FillOutcomes(offer.ContractInfo, evt);
			try
			{
				var builder = new DLCTransactionBuilder(false, null, null, null, Network);
				builder.Accept(offer);
				var k = await Repository.CreatePrivateKey();
				var accept = builder.FundAccept(k.PrivateKey, psbt);
				var dlc = await Repository.NewDLC(name, offer.OracleInfo, builder);
				dlc.FundKeyPath = k.KeyPath;
				dlc.BuilderState = builder.ExportStateJObject();
				dlc.Offer = JObject.FromObject(offer, JsonSerializer.Create(Repository.JsonSettings));
				await Repository.SaveDLC(dlc);
				dlc.Accept = JObject.FromObject(accept, JsonSerializer.Create(Repository.JsonSettings));
				await Repository.SaveDLC(dlc);
				context.WriteObject(accept, Repository.JsonSettings);
			}
			catch (Exception ex)
			{
				throw new CommandException("offer", $"Invalid offer or PSBT. ({ex.Message})");
			}
		}
	}
}
