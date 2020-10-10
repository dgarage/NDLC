using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NDLC.Infrastructure
{
	public class DLCNameRepository
	{
		private NameRepository NameRepository { get; }

		public DLCNameRepository(NameRepository nameRepository)
		{
			this.NameRepository = nameRepository;
		}

		public Task<string?> ResolveName(uint256 dlcId)
		{
			return NameRepository.GetName(Scopes.DLC, dlcId.ToString());
		}

		public async Task SetMapping(string name, uint256 dlcId)
		{
			await NameRepository.SetMapping(Scopes.DLC, name, dlcId.ToString());
		}

		public async Task<List<(string, uint256)>> ListDLCs(EventFullName? eventFullName = null)
		{
			var nameToIdKVs = await NameRepository.GetIds(Scopes.DLC);
			return nameToIdKVs.Select(kv => (kv.Key, new uint256(kv.Value))).ToList();
		}

		public async Task<uint256?> GetId(string name)
		{
			var id = await NameRepository.GetId(Scopes.DLC, name);
			if (id is null)
				return null;
			uint256.TryParse(id, out var v);
			return v;
		}
	}
}
