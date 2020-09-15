using NBitcoin;
using NDLC.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NDLC.CLI
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
