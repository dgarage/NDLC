using System;
using System.Collections.Generic;
using System.Text;

namespace NDLC.CLI
{
	public class CommandOptionRequiredException : CommandException
	{
		public CommandOptionRequiredException(string optionName) : base(optionName, "This option is required")
		{

		}
	}
}
