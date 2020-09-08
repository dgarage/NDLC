using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NDLC.Tests
{
	public static class CommandExtensions
	{
		public static async Task AssertInvokeSuccess(this RootCommand command, string[] args, IConsole console)
		{
			var result = await command.InvokeAsync(args, console);
			Assert.Equal(0, result);
		}
	}
}
