using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using NBitcoin.Secp256k1;
using NDLC.CLI.Events;
using NDLC.CLI.JsonConverters;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
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
		public class Oracle
		{
			public string? Name { get; set; }
			[JsonConverter(typeof(ECXOnlyPubKeyJsonConverter))]
			public ECXOnlyPubKey? PubKey { get; set; }
			[JsonConverter(typeof(NBitcoin.JsonConverters.KeyPathJsonConverter))]
			public RootedKeyPath? RootedKeyPath { get; set; }
		}

		public async Task<DiscreteOutcome?> AddReveal(EventFullName name, Key oracleAttestation)
		{
			var oracles = await GetOracles();
			var oracle = GetOracle(name.OracleName, oracles);
			if (oracle?.PubKey is null)
				return null;
			var events = await GetEvents(oracle);
			var evt = events.FirstOrDefault(e => e.Name.Equals(name.Name, StringComparison.OrdinalIgnoreCase));
			if (evt?.Nonce is null)
				return null;
			var attestation = oracleAttestation.ToECPrivKey();
			var sig = TryCreateSchnorrSig(attestation, evt.Nonce);
			if (sig is null)
				return null;
			foreach (var outcome in evt.Outcomes)
			{
				var discreteOutcome = new DiscreteOutcome(outcome);
				if (!oracle.PubKey.SigVerifyBIP340FIX_DLC(sig, discreteOutcome.Hash))
					continue;
				evt.Attestations ??= new Dictionary<string, Key>();
				if (!evt.Attestations.TryAdd(outcome, oracleAttestation))
					return null;
				// If we have two attestation for the same event, we can recover the private
				// key of the oracle
				if (evt.Attestations.Count > 1 && oracle.RootedKeyPath is null)
				{
					var sigs = evt.Attestations.Select(kv => (Outcome: new DiscreteOutcome(kv.Key),
												   Signature: TryCreateSchnorrSig(kv.Value.ToECPrivKey(), evt.Nonce) ?? throw new InvalidOperationException("Invalid signature in attestations")))
									.Take(2)
									.ToArray();
					var extracted = oracle.PubKey.ExtractPrivateKey(sigs[0].Outcome.Hash, sigs[0].Signature,
													sigs[1].Outcome.Hash, sigs[1].Signature);
					if (extracted.CreateXOnlyPubKey() != oracle.PubKey)
						throw new InvalidOperationException("Could not recover the private key of the oracle, this should never happen");
					var k = new Key(extracted.ToBytes());
					oracle.RootedKeyPath = new RootedKeyPath(k.PubKey.GetHDFingerPrint(), new KeyPath());
					await SaveOracles(oracles);
					if (!KeySetExists(k.PubKey.GetHDFingerPrint()))
						await SaveKeyset(k.PubKey.GetHDFingerPrint(), new Keyset() { SingleKey = k });
				}
				await SaveEvents(oracle, events);
				return discreteOutcome;
			}
			return null;
		}

		SecpSchnorrSignature? TryCreateSchnorrSig(ECPrivKey key, SchnorrNonce nonce)
		{
			var sig64 = new byte[64];
			nonce.WriteToSpan(sig64);
			key.WriteToSpan(sig64.AsSpan().Slice(32));
			NBitcoin.Secp256k1.SecpSchnorrSignature.TryCreate(sig64, out var sig);
			return sig;
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
			var dir = Path.Combine(DataDirectory, "events");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			// base58, help keeping filename small to make windows happy
			var eventFilePath = Path.Combine(dir, Helpers.ToBase58(oracle.PubKey));
			await File.WriteAllTextAsync(eventFilePath, JsonConvert.SerializeObject(events, JsonSettings));
		}

		private static Event GetEvent(EventFullName name, List<Event> events)
		{
			return events.FirstOrDefault(ev => ev.Name?.Equals(name.Name, StringComparison.OrdinalIgnoreCase) is true);
		}

		private async Task<List<Event>> GetEvents(Oracle oracle)
		{
			if (oracle.PubKey is null)
				throw new InvalidOperationException("This oracle's pubkey is not set");
			var dir = Path.Combine(DataDirectory, "events");
			if (!Directory.Exists(dir))
				return new List<Event>();
			// base58, help keeping filename small to make windows happy
			var eventFilePath = Path.Combine(dir, Helpers.ToBase58(oracle.PubKey));
			if (!File.Exists(eventFilePath))
				return new List<Event>();
			return JsonConvert.DeserializeObject<List<Event>>(await File.ReadAllTextAsync(eventFilePath), JsonSettings)
					?? new List<Event>();
		}

		public class Settings
		{
			[JsonConverter(typeof(NBitcoin.JsonConverters.HDFingerprintJsonConverter))]
			public HDFingerprint? DefaultWallet { get; set; }
		}
		public async Task<Key> GetKey(RootedKeyPath keyPath)
		{
			var keyset = await OpenKeyset(keyPath.MasterFingerprint);
			return keyset.GetKey(keyPath);
		}

		public async Task<List<(string Name, ECXOnlyPubKey PubKey)>> ListOracles()
		{
			List<(string Name, ECXOnlyPubKey PubKey)> r = new List<(string Name, ECXOnlyPubKey PubKey)>();
			foreach (var oracle in await GetOracles())
			{
				if (oracle.Name is string && oracle.PubKey is ECXOnlyPubKey)
					r.Add((oracle.Name, oracle.PubKey));
			}
			return r;
		}

		public async Task<(RootedKeyPath KeyPath, Key PrivateKey)> CreatePrivateKey()
		{
			var settings = await this.GetSettings();
			Keyset wallet;
			if (settings.DefaultWallet is HDFingerprint fp)
			{
				wallet = await OpenKeyset(fp);
			}
			else
			{
				var extkey = new ExtKey();
				wallet = new Keyset() { HDKey = extkey.GetWif(this.Network), NextKeyPath = new KeyPath(0) };
				fp = extkey.Neuter().PubKey.GetHDFingerPrint();
				settings.DefaultWallet = fp;
				await this.SaveSettings(settings);
			}
			var key = wallet.GetNextKey();
			await this.SaveKeyset(fp, wallet);
			return (new RootedKeyPath(fp, key.KeyPath), key.Key);
		}

		private async Task SaveKeyset(HDFingerprint fingerprint, Keyset v)
		{
			var dir = Path.Combine(DataDirectory, "keysets");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			await File.WriteAllTextAsync(keyset, JsonConvert.SerializeObject(v, JsonSettings));
		}

		private async Task<Keyset> OpenKeyset(HDFingerprint fingerprint)
		{
			var dir = Path.Combine(DataDirectory, "keysets");
			if (!Directory.Exists(dir))
				throw new FormatException("Invalid keyset file");
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			return JsonConvert.DeserializeObject<Keyset>(await File.ReadAllTextAsync(keyset), JsonSettings) ??
					throw new FormatException("Invalid keyset file");
		}
		private bool KeySetExists(HDFingerprint fingerprint)
		{
			var dir = Path.Combine(DataDirectory, "keysets");
			if (!Directory.Exists(dir))
				return false;
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			return File.Exists(keyset);
		}

		public async Task<Oracle?> GetOracle(string oracleName)
		{
			var oracle = GetOracle(oracleName, await GetOracles());
			if (oracle is null)
				return null;
			return oracle;
		}

		JsonSerializerSettings JsonSettings;
		public Repository(string? dataDirectory, Network network)
		{
			Network = network;
			dataDirectory ??= GetDefaultDataDirectory("ndlc", GetSubDirectory(network));
			DataDirectory = dataDirectory;
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
			JsonSettings.Converters.Add(new NBitcoin.JsonConverters.BitcoinStringJsonConverter(network));
		}

		public async Task<bool> OracleExists(string oracleName)
		{
			return GetOracle(oracleName, await GetOracles()) is Oracle;
		}

		public string DataDirectory
		{
			get;
		}
		public Network Network { get; }

		public async Task<Settings> GetSettings()
		{
			var file = Path.Combine(DataDirectory, "settings.json");
			if (!File.Exists(file))
				return new Settings();
			var settings = JsonConvert.DeserializeObject<Settings>(await File.ReadAllTextAsync(file), JsonSettings);
			return settings ?? new Settings();
		}

		async Task<List<Oracle>> GetOracles()
		{
			var file = Path.Combine(DataDirectory, "oracles.json");
			if (!File.Exists(file))
				return new List<Oracle>();
			var oracles = JsonConvert.DeserializeObject<List<Oracle>>(await File.ReadAllTextAsync(file), JsonSettings);
			return oracles?.Where(o => !string.IsNullOrWhiteSpace(o.Name) && o.PubKey != null).ToList()
				?? new List<Oracle>();
		}
		async Task SaveOracles(List<Oracle> oracles)
		{
			var file = Path.Combine(DataDirectory, "oracles.json");
			await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(oracles, JsonSettings));
		}
		private async Task SaveSettings(Settings settings)
		{
			var file = Path.Combine(DataDirectory, "settings.json");
			await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(settings, JsonSettings));
		}
		public async Task SetOracle(string oracleName, ECXOnlyPubKey pubKey, RootedKeyPath? rootedKeyPath = null)
		{
			var oracles = await GetOracles();
			var oracle = GetOracle(oracleName, oracles);
			if (oracle is null)
			{
				oracle = new Oracle() { Name = oracleName };
				oracles.Add(oracle);
			}
			oracle.PubKey = pubKey;
			oracle.RootedKeyPath = rootedKeyPath;
			await SaveOracles(oracles);
		}

		private static Oracle GetOracle(string oracleName, IEnumerable<Oracle> oracles)
		{
			return oracles.Where(o => oracleName.Equals(o.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
		}

		public async Task<bool> RemoveOracle(string oracleName)
		{
			var oracles = await GetOracles();
			var oracle = GetOracle(oracleName, oracles);
			if (oracle is null)
				return false;
			oracles.Remove(oracle);
			await SaveOracles(oracles);
			return true;
		}

		private string GetSubDirectory(Network network)
		{
			return Enum.GetName(typeof(NetworkType), network.NetworkType) ?? throw new NotSupportedException(network.NetworkType.ToString());
		}

		static string GetDefaultDataDirectory(string appDirectory, string subDirectory, bool createIfNotExists = true)
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
				else if (createIfNotExists)
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir environment variables HOME or APPDATA are not set");
				}
				else
					return string.Empty;
			}

			if (createIfNotExists)
			{
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}
				directory = Path.Combine(directory, subDirectory);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}
			}
			else
			{
				directory = Path.Combine(directory, subDirectory);
			}
			return directory;
		}
	}
}
