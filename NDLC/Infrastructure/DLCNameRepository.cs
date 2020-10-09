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

		public async Task<ICollection<KeyValuePair<string, string>>> ListDLCs(EventFullName? eventFullName = null)
		{
			List<Repository.DLCState> states =  new List<Repository.DLCState>();
			var dlcs = await NameRepository.GetIds(Scopes.DLC);
			return dlcs;
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
