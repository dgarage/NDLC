using System;
using System.Linq;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine.Invocation;
using System.CommandLine.IO;

namespace NDLC.CLI.DLC
{
	public class ListDLCCommand : CommandBase
	{
		public static Command CreateCommand()
		{
			Command command = new Command("list", "List DLCs");
			command.Handler = new ListDLCCommand();
			return command;
		}
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var dlcs = await NameRepository.AsDLCNameRepository().GetNames();
			foreach (var name in dlcs.OrderBy(n => n))
			{
				context.Console.Out.WriteLine(name);
			}
		}
	}
}
