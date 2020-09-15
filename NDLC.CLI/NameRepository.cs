using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class NameRepository
	{
		class Mapping
		{
			public Dictionary<string, Scope> Scopes { get; set; } = new Dictionary<string, Scope>();

			public string? GetId(string scope, string name)
			{
				if (!Scopes.TryGetValue(scope, out var s) || s is null)
					return null;
				s.Ids.TryGetValue(name.Trim(), out var id);
				return id;
			}
			public string? GetName(string scope, string id)
			{
				if (!Scopes.TryGetValue(scope, out var s) || s is null)
					return null;
				return s.Ids.Where(i => i.Value == id)
							.Select(i => i.Key)
							.FirstOrDefault();
			}
		}
		class Scope
		{
			public Dictionary<string, string> Ids { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings()
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver()
			{
				NamingStrategy = new CamelCaseNamingStrategy()
				{
					ProcessDictionaryKeys = false
				}
			},
			Formatting = Formatting.Indented
		};

		public string FilePath { get; }
		public NameRepository(string filePath)
		{
			FilePath = filePath;
		}

		public async Task<string?> GetId(string scope, string name)
		{
			var mapping = await GetMapping();
			return mapping.GetId(scope, name);
		}
		public async Task<string?> GetName(string scope, string id)
		{
			var mapping = await GetMapping();
			return mapping.GetName(scope, id);
		}
		public async Task SetMapping(string scope, string name, string id)
		{
			var mapping = await GetMapping();
			if (!mapping.Scopes.TryGetValue(scope, out var s))
			{
				s = new Scope();
				mapping.Scopes.Add(scope, s);
			}
			if (!s.Ids.TryAdd(name, id))
			{
				s.Ids.Remove(name);
				s.Ids.Add(name, id);
			}
			await SaveMapping(mapping);
		}
		public async Task<bool> RemoveMapping(string scope, string name)
		{
			var mapping = await GetMapping();
			if (!mapping.Scopes.TryGetValue(scope, out var s))
				return false;
			var removed = s.Ids.Remove(name);
			if (s.Ids.Count == 0)
				mapping.Scopes.Remove(scope);
			await SaveMapping(mapping);
			return removed;
		}

		private async Task SaveMapping(Mapping mapping)
		{
			await File.WriteAllTextAsync(FilePath, JsonConvert.SerializeObject(mapping, JsonSettings));
		}

		private async Task<Mapping> GetMapping()
		{
			if (!File.Exists(FilePath))
				return new Mapping();
			var content = await File.ReadAllTextAsync(FilePath);
			return JsonConvert.DeserializeObject<Mapping>(content, JsonSettings) ?? new Mapping();
		}

		public async Task<ICollection<KeyValuePair<string, string>>> GetIds(string scope)
		{
			var mapping = await GetMapping();
			if (!mapping.Scopes.TryGetValue(scope, out var s))
				return new List<KeyValuePair<string, string>>();
			return s.Ids;
		}

		public OracleNameRepository AsOracleNameRepository()
		{
			return new OracleNameRepository(this);
		}

		public EventNameRepository AsEventRepository()
		{
			return new EventNameRepository(this);
		}
		public DLCNameRepository AsDLCNameRepository()
		{
			return new DLCNameRepository(this);
		}
	}

	public class Scopes
	{
		public const string Oracles = "Oracles";
		public const string Events = "Events";
		public const string DLC = "DLCs";
	}
}
