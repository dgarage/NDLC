using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using NDLC.CLI.DLC;
using NDLC.CLI.Events;
using NDLC.Messages;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class Repository
	{
		public class DLCState
		{
			[JsonIgnore]
			public string Name { get; set; } = string.Empty;
			[JsonIgnore]
			public uint256 Id { get; set; } = uint256.Zero;
			[JsonConverter(typeof(KeyPathJsonConverter))]
			public RootedKeyPath? FundKeyPath { get; set; }
			public JObject? BuilderState { get; set; }
			[JsonConverter(typeof(OracleInfoJsonConverter))]
			public OracleInfo? OracleInfo { get; set; }
			public JObject? Offer { get; set; }
			public JObject? Accept { get; set; }
			public JObject? Sign { get; set; }

			public PSBT? Abort { get; set; }

			public DLCTransactionBuilder GetBuilder(Network network)
			{
				if (BuilderState is null)
					throw new InvalidOperationException("The builder is not created yet");
				return new DLCTransactionBuilder(BuilderState.ToString(), network);
			}

			public enum DLCNextStep
			{
				Fund,
				CheckSigs,
				Setup,
				Done,
			}
			public DLCNextStep GetNextStep(Network network)
			{
				if (BuilderState is null)
					throw new InvalidOperationException("BuilderState not set");
				var builder = new DLCTransactionBuilder(BuilderState.ToString(), network);
				DLCNextStep nextStep;
				if (builder.State.IsInitiator)
				{
					if (FundKeyPath is null)
					{
						nextStep = DLCNextStep.Setup;
					}
					else if (Accept is null)
					{
						nextStep = DLCNextStep.CheckSigs;
					}
					else if (Sign is null)
					{
						nextStep = DLCNextStep.Fund;
					}
					else
					{
						nextStep = DLCNextStep.Done;
					}
				}
				else
				{
					if (builder.State.Funding is null)
					{
						nextStep = DLCNextStep.Setup;
					}
					else if (Sign is null)
					{
						nextStep = DLCNextStep.CheckSigs;
					}
					else if (!builder.State.Funding.PSBT.CanExtractTransaction())
					{
						
						nextStep = DLCNextStep.Fund;
					}
					else
					{
						nextStep = DLCNextStep.Done;
					}
				}
				return nextStep;
			}
		}

		public async Task<Oracle?> GetOracle(ECXOnlyPubKey pubKey)
		{
			var dir = Path.Combine(RepositoryDirectory, "Oracles");
			if (!Directory.Exists(dir))
				return null;
			var path = GetOracleFilePath(pubKey);
			if (!File.Exists(path))
				return null;
			return JsonConvert.DeserializeObject<Oracle>(await File.ReadAllTextAsync(path), JsonSettings);
		}
		public async Task<Oracle?> GetOracle(string oracleName)
		{
			var id = await NameRepository.GetId(Scopes.Oracles, oracleName);
			if (id is null)
				return null;
			if (!ECXOnlyPubKey.TryCreate(Encoders.Hex.DecodeData(id), Context.Instance, out var pubkey)
				|| pubkey is null)
				return null;
			return await GetOracle(pubkey);
		}

		private string GetOracleFilePath(ECXOnlyPubKey pubKey)
		{
			return Path.Combine(Path.Combine(RepositoryDirectory, "Oracles"), Encoders.Base58.EncodeData(pubKey.ToBytes()) + ".json");
		}

		async Task SaveOracle(Oracle oracle)
		{
			if (oracle.PubKey is null)
				throw new ArgumentException("Oracle PubKey should not be null");
			var dir = Path.Combine(RepositoryDirectory, "Oracles");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			var path = GetOracleFilePath(oracle.PubKey);
			await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(oracle, JsonSettings));
		}
		public Task<bool> RemoveOracle(ECXOnlyPubKey? pubkey)
		{
			if (pubkey is null)
				return Task.FromResult(false);
			var path = GetOracleFilePath(pubkey);
			if (File.Exists(path))
			{
				File.Delete(path);
				return Task.FromResult(true);
			}
			return Task.FromResult(false);
		}

		public class Event
		{
			public string Name { get; set; } = string.Empty;
			[JsonConverter(typeof(SchnorrNonceJsonConverter))]
			public SchnorrNonce? Nonce { get; set; }
			[JsonConverter(typeof(KeyPathJsonConverter))]
			public RootedKeyPath? NonceKeyPath { get; set; }
			public string[] Outcomes { get; set; } = Array.Empty<string>();
			[JsonProperty(ItemConverterType = typeof(KeyJsonConverter))]
			public Dictionary<string, Key>? Attestations { get; set; }
		}

		public async Task<Event?> GetEvent(EventFullName evtName)
		{
			var oracle = await GetOracle(evtName.OracleName);
			if (oracle is null)
				return null;
			var evts = await GetEvents(oracle);
			return evts.FirstOrDefault(e => e.Name.Equals(evtName.Name, StringComparison.OrdinalIgnoreCase));
		}
		public async Task<Event?> GetEvent(ECXOnlyPubKey oraclePubKey, SchnorrNonce nonce)
		{
			var events = await GetEvents(oraclePubKey);
			return events?.FirstOrDefault(e => e.Nonce == nonce);
		}

		class Keyset
		{
			[JsonProperty("HDKey")]
			public BitcoinExtKey? HDKey { get; set; }
			[JsonConverter(typeof(NBitcoin.JsonConverters.KeyJsonConverter))]
			public Key? SingleKey { get; set; }

			[JsonConverter(typeof(NBitcoin.JsonConverters.KeyPathJsonConverter))]
			public KeyPath? NextKeyPath { get; set; }

			public (KeyPath KeyPath, Key Key) GetNextKey()
			{
				if (HDKey is null || NextKeyPath is null)
					throw new InvalidOperationException("Invalid keyset");
				var k = HDKey.Derive(NextKeyPath).PrivateKey;
				var path = NextKeyPath;
				NextKeyPath = NextKeyPath.Increment()!;
				return (path, k);
			}

			public Key GetKey(RootedKeyPath keyPath)
			{
				if (SingleKey is Key)
				{
					if (SingleKey.PubKey.GetHDFingerPrint() != keyPath.MasterFingerprint)
						throw new InvalidOperationException("The fingerprint of the keyset, does not match the mnemonic");
					if (keyPath.KeyPath.Indexes.Length != 0)
						throw new InvalidOperationException("Indices in the keypath are not valid for this keyset");
					return SingleKey;
				}
				if (HDKey is null)
					throw new InvalidOperationException("The keyset does not have a mnemonic set");
				if (HDKey.GetPublicKey().GetHDFingerPrint() != keyPath.MasterFingerprint)
					throw new InvalidOperationException("The fingerprint of the keyset, does not match the mnemonic");
				return HDKey.Derive(keyPath.KeyPath).PrivateKey;
			}
		}

		public async Task<DLCState> NewDLC(string name, OracleInfo oracleInfo, DLCTransactionBuilder builder)
		{
			name = name.Trim();
			var dir = Path.Combine(RepositoryDirectory, "dlcs");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			var s = new DLCState() 
			{ 
				OracleInfo = oracleInfo,
				BuilderState = builder.ExportStateJObject(),
				Name = name,
				Id = RandomUtils.GetUInt256()
			};
			await AddDLCMapping(s.Id, name);
			var file = GetDLCFilePath(s.Id);
			await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(s, JsonSettings));
			return s;
		}

		private async Task AddDLCMapping(uint256 id, string name)
		{
			var dlcs = Path.Combine(RepositoryDirectory, "dlcs-mapping.json");
			JObject mapping = File.Exists(dlcs) ? JObject.Parse(await File.ReadAllTextAsync(dlcs))
												: new JObject();
			mapping[name] = id.ToString();
			await File.WriteAllTextAsync(dlcs, mapping.ToString(Formatting.Indented));
		}
		private async Task<(uint256 Id, string Name)?> GetDLCId(string name)
		{
			var dlcs = Path.Combine(RepositoryDirectory, "dlcs-mapping.json");
			if (!File.Exists(dlcs))
				return null;
			var jobj = JObject.Parse(await File.ReadAllTextAsync(dlcs));
			var prop = jobj.Property(name, StringComparison.OrdinalIgnoreCase);
			if (prop is null)
				return null;
			return (new uint256(prop.Value.Value<string>()), prop.Name);
		}

		public async Task SaveDLC(DLCState dlc)
		{
			var dir = Path.Combine(RepositoryDirectory, "dlcs");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			var file = GetDLCFilePath(dlc.Id);
			if (!File.Exists(file))
				throw new InvalidOperationException("This DLC does not exists");
			await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(dlc, JsonSettings));
		}

		public async Task<DLCState?> GetDLC(string name)
		{
			var dir = Path.Combine(RepositoryDirectory, "dlcs");
			if (!Directory.Exists(dir))
				return null;

			var mapping = await GetDLCId(name);
			if (mapping is null)
				return null;
			var state = await GetDLC(mapping.Value.Id);
			if (state is null)
				return null;
			state.Name = mapping.Value.Name;
			return state;
		}
		public async Task<DLCState?> GetDLC(uint256 id)
		{
			var file = GetDLCFilePath(id);
			if (!File.Exists(file))
				return null;
			var state = JsonConvert.DeserializeObject<DLCState>(await File.ReadAllTextAsync(file), JsonSettings);
			if (state is null)
				return null;
			state.Id = id;
			return state;
		}

		string GetDLCFilePath(uint256 contractId)
		{
			var fileName = Encoders.Base58.EncodeData(contractId.ToBytes(false));
			return Path.Combine(RepositoryDirectory, "dlcs", $"{fileName}.json");
		}

		public class Oracle
		{
			[JsonConverter(typeof(ECXOnlyPubKeyJsonConverter))]
			public ECXOnlyPubKey? PubKey { get; set; }
			[JsonConverter(typeof(NBitcoin.JsonConverters.KeyPathJsonConverter))]
			public RootedKeyPath? RootedKeyPath { get; set; }
		}

		public async Task<DiscreteOutcome?> AddAttestation(OracleInfo oracleInfo, Key oracleAttestation)
		{
			var oracle = await GetOracle(oracleInfo.PubKey);
			if (oracle is null)
				return null;
			var evt = await GetEvent(oracleInfo.PubKey, oracleInfo.RValue);
			if (evt is null)
				return null;
			var oracleName = await NameRepository.GetName(Scopes.Oracles, new OracleId(oracleInfo.PubKey).ToString());
			return await AddAttestation(new EventFullName(oracleName, evt.Name), oracleAttestation);
		}
		public async Task<DiscreteOutcome?> AddAttestation(EventFullName name, Key oracleAttestation)
		{
			var oracle = await GetOracle(name.OracleName);
			if (oracle?.PubKey is null)
				return null;
			var events = await GetEvents(oracle);
			var evt = events.FirstOrDefault(e => e.Name.Equals(name.Name, StringComparison.OrdinalIgnoreCase));
			if (evt?.Nonce is null)
				return null;
			var attestation = oracleAttestation.ToECPrivKey();
			var sig = evt.Nonce.CreateSchnorrSignature(attestation);
			if (sig is null)
				return null;
			foreach (var outcome in evt.Outcomes)
			{
				var discreteOutcome = new DiscreteOutcome(outcome);
				if (!oracle.PubKey.SigVerifyBIP340(sig, discreteOutcome.Hash))
					continue;
				evt.Attestations ??= new Dictionary<string, Key>();
				if (!evt.Attestations.TryAdd(outcome, oracleAttestation))
					return null;
				// If we have two attestation for the same event, we can recover the private
				// key of the oracle
				if (evt.Attestations.Count > 1 && oracle.RootedKeyPath is null)
				{
					var sigs = evt.Attestations.Select(kv => (Outcome: new DiscreteOutcome(kv.Key),
												   Signature: evt.Nonce.CreateSchnorrSignature(kv.Value.ToECPrivKey()) ?? throw new InvalidOperationException("Invalid signature in attestations")))
									.Take(2)
									.ToArray();
					if (!oracle.PubKey.TryExtractPrivateKey(
													sigs[0].Outcome.Hash, sigs[0].Signature,
													sigs[1].Outcome.Hash, sigs[1].Signature, out var extracted) || extracted is null)
						throw new InvalidOperationException("Could not recover the private key of the oracle, this should never happen");
					var k = new Key(extracted.ToBytes());
					oracle.RootedKeyPath = new RootedKeyPath(k.PubKey.GetHDFingerPrint(), new KeyPath());
					await SaveOracle(oracle);
					if (!KeySetExists(k.PubKey.GetHDFingerPrint()))
						await SaveKeyset(k.PubKey.GetHDFingerPrint(), new Keyset() { SingleKey = k });
				}
				await SaveEvents(oracle, events);
				return discreteOutcome;
			}
			return null;
		}

		public async Task<bool> AddEvent(EventFullName name, SchnorrNonce nonce, string[] outcomes, RootedKeyPath? nonceKeyPath = null)
		{
			var oracle = await GetOracle(name.OracleName);
			if (oracle is null)
				throw new InvalidOperationException("The oracle does not exists");
			var events = await GetEvents(oracle);
			var evt = GetEvent(name, events);
			if (evt is Event)
				return false;
			evt = new Event()
			{
				Name = name.Name,
				Nonce = nonce,
				Outcomes = outcomes,
				NonceKeyPath = nonceKeyPath
			};
			events.Add(evt);
			await SaveEvents(oracle, events);
			return true;
		}
		public async Task<List<Event>> ListEvents(string oracleName)
		{
			var oracle = await GetOracle(oracleName);
			if (oracle is null)
				return new List<Event>();
			return await GetEvents(oracle);
		}

		private async Task SaveEvents(Oracle oracle, List<Event> events)
		{
			if (oracle.PubKey is null)
				throw new InvalidOperationException("This oracle's pubkey is not set");
			var dir = Path.Combine(RepositoryDirectory, "events");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			// base58, help keeping filename small to make windows happy
			var eventFilePath = GetEventFilePath(oracle.PubKey, dir);
			await File.WriteAllTextAsync(eventFilePath, JsonConvert.SerializeObject(events, JsonSettings));
		}

		private static Event GetEvent(EventFullName name, List<Event> events)
		{
			return events.FirstOrDefault(ev => ev.Name?.Equals(name.Name, StringComparison.OrdinalIgnoreCase) is true);
		}

		private Task<List<Event>> GetEvents(Oracle oracle)
		{
			if (oracle.PubKey is null)
				throw new InvalidOperationException("This oracle's pubkey is not set");
			return GetEvents(oracle.PubKey);
		}
		private async Task<List<Event>> GetEvents(ECXOnlyPubKey oracle)
		{
			var dir = Path.Combine(RepositoryDirectory, "events");
			if (!Directory.Exists(dir))
				return new List<Event>();
			// base58, help keeping filename small to make windows happy
			var eventFilePath = GetEventFilePath(oracle, dir);
			if (!File.Exists(eventFilePath))
				return new List<Event>();
			return JsonConvert.DeserializeObject<List<Event>>(await File.ReadAllTextAsync(eventFilePath), JsonSettings)
					?? new List<Event>();
		}

		private static string GetEventFilePath(ECXOnlyPubKey oraclePubKey, string dir)
		{
			return Path.Combine(dir, Helpers.ToBase58(oraclePubKey)) + ".json";
		}

		public class Settings
		{
			[JsonConverter(typeof(NBitcoin.JsonConverters.HDFingerprintJsonConverter))]
			public HDFingerprint? DefaultKeyset { get; set; }
		}
		public async Task<Key> GetKey(RootedKeyPath keyPath)
		{
			var keyset = await OpenKeyset(keyPath.MasterFingerprint);
			return keyset.GetKey(keyPath);
		}

		public async Task<(RootedKeyPath KeyPath, Key PrivateKey)> CreatePrivateKey()
		{
			var settings = await this.GetSettings();
			Keyset wallet;
			if (settings.DefaultKeyset is HDFingerprint fp)
			{
				wallet = await OpenKeyset(fp);
			}
			else
			{
				var extkey = new ExtKey();
				wallet = new Keyset() { HDKey = extkey.GetWif(this.Network), NextKeyPath = new KeyPath(0) };
				fp = extkey.Neuter().PubKey.GetHDFingerPrint();
				settings.DefaultKeyset = fp;
				await this.SaveSettings(settings);
			}
			var key = wallet.GetNextKey();
			await this.SaveKeyset(fp, wallet);
			return (new RootedKeyPath(fp, key.KeyPath), key.Key);
		}

		private async Task SaveKeyset(HDFingerprint fingerprint, Keyset v)
		{
			var dir = Path.Combine(RepositoryDirectory, "keysets");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			await File.WriteAllTextAsync(keyset, JsonConvert.SerializeObject(v, JsonSettings));
		}

		private async Task<Keyset> OpenKeyset(HDFingerprint fingerprint)
		{
			var dir = Path.Combine(RepositoryDirectory, "keysets");
			if (!Directory.Exists(dir))
				throw new FormatException("Invalid keyset file");
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			return JsonConvert.DeserializeObject<Keyset>(await File.ReadAllTextAsync(keyset), JsonSettings) ??
					throw new FormatException("Invalid keyset file");
		}
		private bool KeySetExists(HDFingerprint fingerprint)
		{
			var dir = Path.Combine(RepositoryDirectory, "keysets");
			if (!Directory.Exists(dir))
				return false;
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			return File.Exists(keyset);
		}

		public JsonSerializerSettings JsonSettings { get; }
		public NameRepository NameRepository { get; }
		public Repository(string? dataDirectory, Network network)
		{
			Network = network;
			dataDirectory ??= GetDefaultDataDirectory("ndlc");
			if (!Directory.Exists(dataDirectory))
				Directory.CreateDirectory(dataDirectory);
			DataDirectory = dataDirectory;
			RepositoryDirectory = Path.Combine(dataDirectory, GetSubDirectory(network));
			if (!Directory.Exists(RepositoryDirectory))
				Directory.CreateDirectory(RepositoryDirectory);
			NameRepository = new NameRepository(Path.Combine(RepositoryDirectory, "names.json"));
			JsonSettings = new JsonSerializerSettings()
			{
				Formatting = Formatting.Indented,
				ContractResolver = new CamelCasePropertyNamesContractResolver()
				{
					NamingStrategy = new CamelCaseNamingStrategy()
					{
						ProcessDictionaryKeys = false
					}
				},
				DefaultValueHandling = DefaultValueHandling.Ignore
			};
			NDLC.Messages.Serializer.Configure(JsonSettings, network);
		}

		

		public async Task<bool> OracleExists(string oracleName)
		{
			return await GetOracle(oracleName) is Oracle;
		}

		public string RepositoryDirectory
		{
			get;
		}
		public string DataDirectory
		{
			get;
		}
		public Network Network { get; }

		public async Task<Settings> GetSettings()
		{
			var file = Path.Combine(RepositoryDirectory, "settings.json");
			if (!File.Exists(file))
				return new Settings();
			var settings = JsonConvert.DeserializeObject<Settings>(await File.ReadAllTextAsync(file), JsonSettings);
			return settings ?? new Settings();
		}

		private async Task SaveSettings(Settings settings)
		{
			var file = Path.Combine(RepositoryDirectory, "settings.json");
			await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(settings, JsonSettings));
		}
		public async Task<bool> AddOracle(ECXOnlyPubKey pubKey, RootedKeyPath? rootedKeyPath = null)
		{
			var oracle = await GetOracle(pubKey);
			if (oracle is Oracle)
				return false;
			oracle = new Oracle();
			oracle.PubKey = pubKey;
			oracle.RootedKeyPath = rootedKeyPath;
			await SaveOracle(oracle);
			return true;
		}
		private string GetSubDirectory(Network network)
		{
			return Enum.GetName(typeof(NetworkType), network.NetworkType) ?? throw new NotSupportedException(network.NetworkType.ToString());
		}

		static string GetDefaultDataDirectory(string appDirectory)
		{
			string? directory = null;
			var home = Environment.GetEnvironmentVariable("HOME");
			var localAppData = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(home) && string.IsNullOrEmpty(localAppData))
			{
				directory = home;
				directory = Path.Combine(directory, "." + appDirectory.ToLowerInvariant());
			}
			else
			{
				if (!string.IsNullOrEmpty(localAppData))
				{
					directory = localAppData;
					directory = Path.Combine(directory, appDirectory);
				}
				else
					return string.Empty;
			}
			return directory;
		}
	}
}
