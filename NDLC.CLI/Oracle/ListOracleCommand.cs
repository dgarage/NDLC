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

			var names = await NameRepository.AsOracleNameRepository().GetIds();
			foreach (var o in names.OrderBy(n => n.Key))
			{
				context.Console.Out.WriteLine($"{o.Key}\t{o.Value}");
			}
		}
	}
}
