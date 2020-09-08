using NDLC.CLI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace NDLC.Tests
{
	public class CommandTester
	{
		public CommandTester(ITestOutputHelper log)
		{
			Log = log;
			SpyConsole = new SpyConsole(log);
		}
		SpyConsole SpyConsole;
		public ITestOutputHelper Log { get; }

		public async Task AssertInvokeSuccess(string[] args)
		{
			SpyConsole.Clear();
			var command = Program.CreateCommand();
			await command.AssertInvokeSuccess(args, SpyConsole);
			var result = command.Parse(args);
			LastCommand = (CommandBase)((Command)result.CommandResult.Command).Handler;
		}
		public CommandBase LastCommand { get; set; }
		public T GetResult<T>()
		{
			if (LastCommand is null)
				throw new InvalidOperationException("No command ran");
			return JsonConvert.DeserializeObject<T>(GetLastOutput(), LastCommand.JsonSerializerSettings);
		}

		public string GetLastOutput()
		{
			return SpyConsole.GetOutput();
		}
	}
}
