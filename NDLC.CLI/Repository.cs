using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NDLC.CLI.JsonConverters;
using NDLC.Messages.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class Repository
	{
		class Keyset
		{
			[JsonConverter(typeof(MnemonicJsonConverter))]
			public Mnemonic? Mnemonic { get; set; }
			public uint NextIndex { get; set; }

			public Key GetNextKey()
			{
				if (Mnemonic is null)
					throw new InvalidOperationException("The keyset does not have a mnemonic set");
				var k = Mnemonic.DeriveExtKey().Derive(new KeyPath(NextIndex)).PrivateKey;
				NextIndex++;
				return k;
			}

			public Key GetKey(RootedKeyPath keyPath)
			{
				if (Mnemonic is null)
					throw new InvalidOperationException("The keyset does not have a mnemonic set");
				var master = Mnemonic.DeriveExtKey();
				if (master.GetPublicKey().GetHDFingerPrint() != keyPath.MasterFingerprint)
					throw new InvalidOperationException("The fingerprint of the keyset, does not match the mnemonic");
				return master.Derive(keyPath.KeyPath).PrivateKey;
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
				var mnemo = new Mnemonic(Wordlist.English);
				wallet = new Keyset() { Mnemonic = mnemo, NextIndex = 0 };
				fp = mnemo.DeriveExtKey().Neuter().PubKey.GetHDFingerPrint();
				settings.DefaultWallet = fp;
				await this.SaveSettings(settings);
			}
			var key = wallet.GetNextKey();
			await this.SaveKeyset(fp, wallet);
			return (new RootedKeyPath(fp, new KeyPath(wallet.NextIndex - 1)), key);
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
				Directory.CreateDirectory(dir);
			var keyset = Path.Combine(dir, $"{fingerprint}.json");
			return JsonConvert.DeserializeObject<Keyset>(await File.ReadAllTextAsync(keyset), JsonSettings) ??
					throw new FormatException("Invalid keyset file");
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
			dataDirectory ??= GetDefaultDataDirectory("ndlc", GetSubDirectory(network));
			DataDirectory = dataDirectory;
			JsonSettings = new JsonSerializerSettings()
			{
				Formatting = Formatting.Indented,
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			};
		}

		public async Task<bool> OracleExists(string oracleName)
		{
			return GetOracle(oracleName, await GetOracles()) is Oracle;
		}

		public string DataDirectory
		{
			get;
		}

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
