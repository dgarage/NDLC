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
			var review = new OfferReview(offer);
			WriteObject(context, review);
			return Task.CompletedTask;
		}
	}
}
