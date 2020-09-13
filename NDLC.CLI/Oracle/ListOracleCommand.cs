using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class ListOracleCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("list", "List oracles");
			command.Handler = new ListOracleCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			byte[] buf = new byte[32];
			foreach (var o in (await Repository.ListOracles()).OrderBy(o => o.Name))
			{
				o.PubKey.WriteToSpan(buf);
				var hex = Encoders.Hex.EncodeData(buf);
				context.Console.Out.WriteLine($"{o.Name}\t{hex}");
			}
		}
	}
}
