using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using NDLC.CLI.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
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
				_NameRepository = new NameRepository(Path.Combine(_Repository.RepositoryDirectory, "names.json"));
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
		NameRepository? _NameRepository;
		public NameRepository NameRepository => _NameRepository ?? throw new InvalidOperationException("Repository is not set");
		protected abstract Task InvokeAsyncBase(InvocationContext context);

		public async Task<Oracle> GetOracle(string optionName, string oracleName)
		{
			var oracle = await TryGetOracle(oracleName);
			if (oracle is null)
				throw new CommandException(optionName, "This oracle does not exists");
			return oracle;
		}
		public async Task<DLCState> GetDLC(string optionName, string dlcName)
		{
			var dlc = await TryGetDLC(dlcName);
			if (dlc is null)
				throw new CommandException(optionName, "This DLC does not exists");
			return dlc;
		}

		public async Task<DLCState?> TryGetDLC(string dlcName)
		{
			var id = await NameRepository.AsDLCNameRepository().GetId(dlcName);
			if (id is null)
				return null;
			return await Repository.GetDLC(id);
		}

		public async Task<Event> GetEvent(string optionName, EventFullName eventFullName)
		{
			var evt = await TryGetEvent(eventFullName);
			if (evt is null)
				throw new CommandException(optionName, "This event's full name does not exists");
			return evt;
		}
		public async Task<Event?> TryGetEvent(EventFullName eventFullName)
		{
			var id = await NameRepository.AsEventRepository().GetEventId(eventFullName);
			if (id is null)
				return null;
			return await Repository.GetEvent(id);
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
