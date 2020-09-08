using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class ReviewOfferCommand : CommandBase
	{
		protected override Task InvokeAsyncBase(InvocationContext context)
		{
			var offer = JsonConvert.DeserializeObject<Offer>(context.ParseResult.CommandResult.GetArgumentValueOrDefault("offer") as string ?? string.Empty, JsonSerializerSettings);
			if (offer?.OracleInfo is null)
				throw new CommandException("offer", "The offer does not have oracle information");
			context.Console.Out.WriteLine("Oracle: " + Encoders.Hex.EncodeData(offer.OracleInfo.PubKey.ToBytes()));
			context.Console.Out.WriteLine("Nonce: " + offer.OracleInfo.RValue);
			if (offer?.ContractInfo is null)
				throw new CommandException("offer", "The offer does not have contract information");

			context.Console.Out.WriteLine("The offerer profit or loss are:");
			foreach (var ci in offer.ContractInfo)
			{
				context.Console.Out.WriteLine($"{(ci.Sats - offer.TotalCollateral).ToString(true, false)} => {ci.Outcome}");
			}
			context.Console.Out.WriteLine("The acceptor profit or loss are:");
			var acceptorCollateral = offer.ContractInfo.Select(o => o.Sats - offer.TotalCollateral).Max();
			acceptorCollateral = Money.Max(Money.Zero, acceptorCollateral);
			foreach (var ci in offer.ContractInfo)
			{
				context.Console.Out.WriteLine($"{(acceptorCollateral - (ci.Sats - offer.TotalCollateral)).ToString(true, false)} => {ci.Outcome}");
			}
			return Task.CompletedTask;
		}
	}
}
