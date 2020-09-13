using NBitcoin.DataEncoders;
using NDLC.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Text;

namespace NDLC.CLI.DLC
{
	public static class DLCHelpers
	{
		public static JObject ExportStateJObject(this DLCTransactionBuilder builder)
		{
			return JObject.Parse(builder.ExportState());
		}
		public static void WriteObject(this InvocationContext ctx, object obj, JsonSerializerSettings settings)
		{
			var json = ctx.ParseResult.ValueForOption<bool>("json");
			var txt = JsonConvert.SerializeObject(obj, settings);
			if (json)
				ctx.Console.Out.Write(txt);
			else
				ctx.Console.Out.Write(Encoders.Base64.EncodeData(UTF8Encoding.UTF8.GetBytes(txt)));
		}
	}
}
