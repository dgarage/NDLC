using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI.Events
{
	public class EventFullName
	{

		private static readonly EventFullName _Empty = new EventFullName("","");
		public static EventFullName Empty
		{
			get
			{
				return _Empty;
			}
		}
		public EventFullName(string oracleName, string name)
		{
			OracleName = oracleName;
			Name = name;
		}

		public string OracleName { get; }
		public string Name { get; }

		public override string ToString()
		{
			return $"{OracleName}/{Name}";
		}
		public static bool TryParse(string fullName, out EventFullName? evt)
		{
			if (fullName == null)
				throw new ArgumentNullException(nameof(fullName));
			fullName = fullName.Trim();
			evt = null;
			var i = fullName.IndexOf('/');
			if (i == -1)
				return false;
			evt = new EventFullName(fullName.Substring(0, i), fullName.Substring(i + 1));
			return true;
		}
	}
}
