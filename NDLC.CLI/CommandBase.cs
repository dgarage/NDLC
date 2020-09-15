using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NDLC.CLI.Repository;

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
		public NameRepository NameRepository => Repository.NameRepository;
		protected abstract Task InvokeAsyncBase(InvocationContext context);

		public async Task<Oracle> GetOracle(string optionName, string oracleName)
		{
			var oracle = await TryGetOracle(oracleName);
			if (oracle is null)
				throw new CommandException(optionName, "This oracle does not exists");
			return oracle;
		}
		public async Task<Oracle?> TryGetOracle(string oracleName)
		{
			var id = await NameRepository.GetId(Scopes.Oracles, oracleName);
			if (id is null)
				return null;
			ECXOnlyPubKey.TryCreate(Encoders.Hex.DecodeData(id), Context.Instance, out var pubkey);
			if (pubkey is null)
				return null;
			return await Repository.GetOracle(pubkey);
		}

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
