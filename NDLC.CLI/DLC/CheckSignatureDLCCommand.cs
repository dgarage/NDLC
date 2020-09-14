using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.Contracts;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI.DLC
{
	class CheckSignatureDLCCommand : CommandBase
	{
		class ContractIds
		{
			public string? OffererContractId { get; set; }
			public string? AcceptorContractId { get; set; }
		}
		public static Command CreateCommand()
		{
			Command command = new Command("checksigs", "Check that the other party properly signed the DLC");
			command.Add(new Argument<string>("signed", "Signatures of the DLC"));
			command.Handler = new CheckSignatureDLCCommand();
			return command;
		}
		protected override Task InvokeAsyncBase(InvocationContext context)
		{
			var signedMessageBase64 = context.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("signed")?.Trim();
			if (signedMessageBase64 is null)
				throw new CommandOptionRequiredException("signed");
			var contractIds = Parse<ContractIds>(signedMessageBase64);
			if (contractIds.AcceptorContractId is string &&
				contractIds.OffererContractId is string)
			{
				return HandleAccept(context, signedMessageBase64);
			}
			else if (contractIds.AcceptorContractId is string &&
				contractIds.OffererContractId is null)
			{
				return HandleSign(context, signedMessageBase64);
			}
			throw new CommandException("signed", "Invalid signed message");
		}

		private async Task HandleAccept(InvocationContext context, string signedMessageBase64)
		{
			var accept = Parse<Accept>(signedMessageBase64);
			var dlc = await GetDLC(accept.OffererContractId);
			if (dlc.GetNextStep(Network) != Repository.DLCState.DLCNextStep.OffererCheckSigs
				|| dlc.BuilderState is null)
				throw new CommandException("signed", "The DLC is not in a state requiring to check signatures of the acceptor");
			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			try
			{
				builder.Sign1(accept);
				dlc.BuilderState = builder.ExportStateJObject();
				dlc.Accept = JObject.FromObject(accept, JsonSerializer.Create(Repository.JsonSettings));
				await Repository.SaveDLC(dlc);
				context.Console.Out.Write(builder.GetFundingPSBT().ToBase64());
			}
			catch (Exception ex)
			{
				throw new CommandException("signed", $"Invalid signatures. ({ex.Message})");
			}
		}
		private async Task HandleSign(InvocationContext context, string signedMessageBase64)
		{
			var sign = Parse<Sign>(signedMessageBase64);
			var dlc = await GetDLC(sign.AcceptorContractId);
			if (dlc.GetNextStep(Network) != Repository.DLCState.DLCNextStep.AcceptorCheckSigs || dlc.BuilderState is null)
				throw new CommandException("signed", "The DLC is not in a state requiring to check signatures of the offerer");
			var builder = new DLCTransactionBuilder(dlc.BuilderState.ToString(), Network);
			try
			{
				builder.Finalize1(sign);
				dlc.Sign = JObject.FromObject(sign, JsonSerializer.Create(Repository.JsonSettings));
				dlc.BuilderState = builder.ExportStateJObject();
				await Repository.SaveDLC(dlc);
				context.Console.Out.Write(builder.GetFundingPSBT().ToBase64());
			}
			catch (Exception ex)
			{
				throw new CommandException("signed", $"Invalid signatures. ({ex.Message})");
			}
		}

		private async Task<Repository.DLCState> GetDLC(uint256? contractId)
		{
			if (contractId is null)
			{
				// TODO: Allow to pass a hint via command line
				throw new CommandException("signed", "This accept message does not match any of our DLC");
			}
			else
			{
				var dlc = await Repository.GetDLC(contractId);
				if (dlc is null)
					throw new CommandException("signed", "This accept message does not match any of our DLC");
				return dlc;
			}
		}

		private T Parse<T>(string base64)
		{
			try
			{
				var json = UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(base64));
				return JsonConvert.DeserializeObject<T>(json, Repository.JsonSettings) ?? throw new Exception();
			}
			catch
			{
				throw new CommandException("signed", "Invalid signed message");
			}
		}
	}
}
