using System.Collections.Generic;
using System.Threading.Tasks;
using NDLC.Messages;
using NDLC.Secp256k1;

namespace NDLC.Infrastructure
{
	public class EventNameRepository
	{
		public EventNameRepository(NameRepository nameRepository)
		{
			NameRepository = nameRepository;
		}

		public NameRepository NameRepository { get; }

		public async Task<OracleInfo?> GetEventId(EventFullName eventFullName)
		{
			var oracleId = await NameRepository.AsOracleNameRepository().GetId(eventFullName.OracleName);
			if (oracleId is null)
				return null;
			var id = await NameRepository.GetId(Scopes.Events, GetEventFullName(oracleId, eventFullName.Name));
			if (id is null)
				return null;
			if (!SchnorrNonce.TryParse(id, out var nonce))
				return null;
			return new OracleInfo(oracleId.PubKey, nonce);
		}

		public async Task<EventFullName?> ResolveName(OracleInfo eventId)
		{
			var events = await NameRepository.GetIds(Scopes.Events);
			foreach (var ev in events)
			{
				var oracleId = new OracleId(eventId.PubKey);
				if (ev.Key.StartsWith(oracleId.ToString()))
				{
					if (ev.Value == eventId.RValue.ToString())
					{
						var oracleName = await NameRepository.GetName(Scopes.Oracles, oracleId.ToString());
						if (oracleName is null)
							return null;
						var idx = ev.Key.IndexOf('/');
						return new EventFullName(oracleName, ev.Key.Substring(idx + 1));
					}
				}
			}
			return null;
		}

		public async Task<ICollection<EventFullName>> ListEvents(string? searchedOracleName)
		{
			List<EventFullName> names = new List<EventFullName>();
			var events = await NameRepository.GetIds(Scopes.Events);
			OracleId? searchedOracleId = null;
			if (searchedOracleName is string)
			{
				searchedOracleId = await NameRepository.AsOracleNameRepository().GetId(searchedOracleName);
				if (searchedOracleId is null)
					return names;
			}
			foreach (var evt in events)
			{
				var idx = evt.Key.IndexOf('/');
				var oracleId = evt.Key.Substring(0, idx);
				if (searchedOracleId is OracleId && searchedOracleId.ToString() != oracleId)
					continue;
				var oracleName = await NameRepository.GetName(Scopes.Oracles, oracleId);
				if (oracleName is null)
					continue;
				names.Add(new EventFullName(oracleName, evt.Key.Substring(idx + 1)));
			}
			return names;
		}

		public async Task SetMapping(OracleInfo eventId, string name)
		{
			var fullname = GetEventFullName(eventId.PubKey, name);
			await NameRepository.SetMapping(Scopes.Events, fullname, eventId.RValue.ToString());
		}

		private string GetEventFullName(OracleId oracleId, string name)
		{
			return $"{oracleId.ToString()}/{name}";
		}
	}
}
