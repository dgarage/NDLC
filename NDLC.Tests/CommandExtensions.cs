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
		public static async Task AssertInvoke(this RootCommand command, string[] args, IConsole console, int expectedResult)
		{
			var result = await command.InvokeAsync(args, console);
			Assert.Equal(expectedResult, result);
		}
	}
}
