using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
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
		class Oracle
		{
			public string? Name { get; set; }
			[JsonConverter(typeof(ECXOnlyPubKeyJsonConverter))]
			public ECXOnlyPubKey? PubKey { get; set; }
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
		public async Task SetOracle(string oracleName, ECXOnlyPubKey pubKey)
		{
			var oracles = await GetOracles();
			var oracle = GetOracle(oracleName, oracles);
			if (oracle is null)
			{
				oracle = new Oracle() { Name = oracleName };
				oracles.Add(oracle);
			}
			oracle.PubKey = pubKey;
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
