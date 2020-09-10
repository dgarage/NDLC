using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public abstract class CommandBase : ICommandHandler
	{
		public Network Network { get; private set; } = Network.Main;
		public JsonSerializerSettings JsonSerializerSettings { get; private set; } = new JsonSerializerSettings() { Formatting = Formatting.Indented };
		public async Task<int> InvokeAsync(InvocationContext context)
		{
			try
			{
				Network = Bitcoin.Instance.GetNetwork(GetNetworkType(context.ParseResult.RootCommandResult.ValueForOption("network") as string ?? "mainnet"));
				_Repository = new Repository(context.ParseResult.RootCommandResult.ValueForOption<string>("datadir"), Network);
				NDLC.Messages.Serializer.Configure(JsonSerializerSettings, Network);
				await InvokeAsyncBase(context);
				return 0;
			}
			catch (CommandException ex)
			{
				context.Console.Error.WriteLine(ex.Message);
				return 1;
			}
		}

		Repository? _Repository;
		public Repository Repository => _Repository ?? throw new InvalidOperationException("Repository is not set");
		protected void WriteObject<T>(InvocationContext context, T obj)
		{
			context.Console.Out.Write(JsonConvert.SerializeObject(obj, JsonSerializerSettings));
		}

		protected abstract Task InvokeAsyncBase(InvocationContext context);

		private NetworkType GetNetworkType(string networkType)
		{
			var type = Enum.GetNames(typeof(NetworkType))
				.Select(n => n.ToUpperInvariant())
				.FirstOrDefault(o => networkType.ToUpperInvariant() == o);
			if (type is null)
				throw new CommandException("network", $"Invalid network available are {string.Join(",", Enum.GetNames(typeof(NetworkType)).ToArray())}");
			return (NetworkType)Enum.Parse(typeof(NetworkType), networkType, true);
		}
	}
}
