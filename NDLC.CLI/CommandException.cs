using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI
{
	public class CommandException : Exception
	{
		public CommandException(string optionName, string error):base($"--{optionName}: {error}")
		{
			OptionError = error;
			OptionName = optionName;
		}
		public string OptionName { get; }
		public string OptionError { get; }
	}
}
