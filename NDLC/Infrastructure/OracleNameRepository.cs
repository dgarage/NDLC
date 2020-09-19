using System.Collections.Generic;
using System.Threading.Tasks;

namespace NDLC.Infrastructure
{
	public class OracleNameRepository
	{
		public OracleNameRepository(NameRepository nameRepository)
		{
			NameRepository = nameRepository;
		}
		public NameRepository NameRepository { get; }
		public async Task<OracleId?> GetId(string name)
		{
			var id = await NameRepository.GetId(Scopes.Oracles, name);
			if (id is null)
				return null;
			OracleId.TryParse(id, out var r);
			return r;
		}

		public async Task<ICollection<KeyValuePair<string, OracleId>>> GetIds()
		{
			var ids = await NameRepository.GetIds(Scopes.Oracles);
			var result = new List<KeyValuePair<string, OracleId>>();
			foreach (var id in ids)
			{
				if (OracleId.TryParse(id.Value, out var idObj))
					result.Add(new KeyValuePair<string, OracleId>(id.Key, idObj));
			}
			return result;
		}
	}
}
