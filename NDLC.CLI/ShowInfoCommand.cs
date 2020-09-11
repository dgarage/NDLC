using NBitcoin;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class ShowInfoCommand : CommandBase
	{
		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			var version = this.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
			context.Console.Out.WriteLine($"Version: {version}");
			context.Console.Out.WriteLine($"Data directory: {Repository.DataDirectory}");
			var settings = await Repository.GetSettings();
			var keyset = settings.DefaultKeyset is HDFingerprint fp ? fp.ToString() : "<Not set>";
			context.Console.Out.WriteLine($"Default keyset: {keyset}");
		}
	}
}
