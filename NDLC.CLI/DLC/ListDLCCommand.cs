using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NDLC.CLI.DLC
{
    public class ListDLCCommand : CommandBase
    {
        public static Command CreateCommand()
        {
			Command command = new Command("list", "List DLCs");
			command.Add(new Option<string>("--oracle", "Filter events of this specific oracle")
			{
				Argument = new Argument<string>()
				{
					Arity = ArgumentArity.ExactlyOne
				},
				IsRequired = false
			});
			command.Add(new Option<string>("--eventfullname", "Filter events of this specific event")
			{
				Argument = new Argument<string>()
				{
					Arity = ArgumentArity.ExactlyOne
				},
				IsRequired = false
			});
			command.Handler = new ListDLCCommand();
			return command;
        }
        protected override async Task InvokeAsyncBase(InvocationContext context)
        {
			var eventName = context.ParseResult.ValueForOption<string>("eventfullname");
			var dlcs = await NameRepository.AsDLCNameRepository().ListDLCs();
			foreach (var dlcName in dlcs.OrderBy(o => o.Key.ToString()))
			{
				var dState = await Repository.GetDLC(new uint256(dlcName.Value));
				context.Console.Out.WriteLine(dlcName.Key + ": " + dlcName.Value + $": NextStep({dState.GetNextStep(Network)})");
			}
        }
    }
}