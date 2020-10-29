using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.TLV;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using static NDLC.Infrastructure.Repository;
using static NDLC.Infrastructure.Repository.DLCState;

namespace NDLC.CLI.DLC
{
	public static class DLCHelpers
	{

		public static void AssertState(this InvocationContext ctx, string optionName, DLCState currentState, bool expectedOfferer, DLCNextStep expectedState, Network network)
		{
			if (currentState.BuilderState is null)
				throw new CommandException(optionName, "The DLC is in an invalid state for this action");
			var isOfferer = currentState.GetBuilder(network).State.IsInitiator;
			if (isOfferer && !expectedOfferer)
					throw new CommandException(optionName, "This action must be run by the acceptor, but you are the offerer of the DLC");
			if (!isOfferer && expectedOfferer)
				throw new CommandException(optionName, "This action must be run by the offerer, but you are the acceptor of the DLC");
			var actualStep = currentState.GetNextStep(network);
			if (actualStep != expectedState)
				throw new CommandException(optionName, $"The DLC is in an invalid state for this action. The expected state is '{expectedState}' but your state is '{actualStep}'.");
		}

		public static void WriteTransaction(this InvocationContext ctx, Transaction tx, Coin? fundingCoin, Network network)
		{
			if (ctx.ParseResult.ValueForOption<bool>("psbt"))
			{
				var psbt = PSBT.FromTransaction(tx, network);
				for (int i = 0; i < tx.Inputs.Count; i++)
				{
					psbt.Inputs[i].FinalScriptSig = tx.Inputs[i].ScriptSig;
					psbt.Inputs[i].FinalScriptWitness = tx.Inputs[i].WitScript;
				}
				if (fundingCoin is Coin)
					psbt.AddCoins(fundingCoin);
				ctx.WritePSBT(psbt);
			}
			else
			{
				if (ctx.ParseResult.ValueForOption<bool>("json"))
				{
					ctx.Console.Out.Write(tx.ToString());
				}
				else
				{
					ctx.Console.Out.Write(tx.ToHex());
				}
			}
		}
		public static void WriteObject(this InvocationContext ctx, ITLVObject obj, JsonSerializerSettings settings)
		{
			var json = ctx.ParseResult.ValueForOption<bool>("json");
			if (json)
			{
				var txt = JsonConvert.SerializeObject(obj, settings);
				ctx.Console.Out.Write(txt);
			}
			else
			{
				var ms = new MemoryStream();
				TLVWriter writer = new TLVWriter(ms);
				obj.WriteTLV(writer);
				ms.Position = 0;
				ctx.Console.Out.Write(Encoders.Base64.EncodeData(ms.ToArray()));
			}
		}
		public static void WritePSBT(this InvocationContext ctx, PSBT psbt)
		{
			if (ctx.ParseResult.ValueForOption<bool>("json"))
			{
				ctx.Console.Out.Write(psbt.ToString());
			}
			else
			{
				ctx.Console.Out.Write(psbt.ToBase64());
			}
		}

		public static Offer GetOffer(this InvocationContext ctx, Network network)
		{
			var offer = ctx.ParseResult.CommandResult.GetArgumentValueOrDefault<string>("offer");
			if (offer is null)
				throw new CommandOptionRequiredException("offer");
			try
			{
				var tlv = Encoders.Base64.DecodeData(offer);
				var reader = new TLVReader(new MemoryStream(tlv));
				return Offer.ParseFromTLV(reader, network);
			}
			catch
			{
				throw new CommandException("offer", "Invalid offer");
			}
		}
	}
}
