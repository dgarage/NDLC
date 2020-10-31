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
using NDLC.Infrastructure;
using static NDLC.Infrastructure.Repository.DLCState;
using NDLC.TLV;
using System.IO;

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

			var type = GetTLVType(signedMessageBase64);
			bool wellFormated = false;
			try
			{
				if (type == Accept.TLVType)
				{
					var accept = Accept.ParseFromTLV(signedMessageBase64, Network);
					wellFormated = true;
					return HandleAccept(context, accept);
				}
				else if (type == Sign.TLVType)
				{
					var sign = Sign.ParseFromTLV(signedMessageBase64, Network);
					wellFormated = true;
					return HandleSign(context, sign);
				}
			}
			catch (Exception ex) when (!wellFormated)
			{
				throw new CommandException("signed", $"Invalid signed message ({ex.Message})");
			}
			throw new CommandException("signed", "Invalid signed message");
		}

		private ushort GetTLVType(string signedMessageBase64)
		{
			try
			{
				var data = Encoders.Base64.DecodeData(signedMessageBase64);
				var r = new TLVReader(new MemoryStream(data));
				return r.ReadU16();
			}
			catch
			{

			}
			return 0;
		}

		private async Task HandleAccept(InvocationContext context, Accept accept)
		{
			var dlc = await GetDLC(accept.TemporaryContractId);
			context.AssertState("signed", dlc, true, DLCNextStep.CheckSigs, Network);
			var builder = new DLCTransactionBuilder(dlc.BuilderState!.ToString(), Network);
			try
			{
				builder.Sign1(accept);
				dlc.BuilderState = builder.ExportStateJObject();
				dlc.Accept = accept;
				await Repository.SaveDLC(dlc);
				context.WritePSBT(builder.GetFundingPSBT());
			}
			catch (Exception ex)
			{
				throw new CommandException("signed", $"Invalid signatures. ({ex.Message})");
			}
		}

		private async Task HandleSign(InvocationContext context, Sign sign)
		{
			var dlc = await GetDLC(sign.ContractId);
			context.AssertState("signed", dlc, false, DLCNextStep.CheckSigs, Network);
			var builder = new DLCTransactionBuilder(dlc.BuilderState!.ToString(), Network);
			try
			{
				builder.Finalize1(sign);
				dlc.Sign = sign;
				dlc.BuilderState = builder.ExportStateJObject();
				await Repository.SaveDLC(dlc);
				context.WritePSBT(builder.GetFundingPSBT());
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
