using NBitcoin.Secp256k1;
using NDLC.CLI.DLC;
using NDLC.CLI.Events;
using NDLC.Secp256k1;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class Program
	{
		static async Task Main(string[] args)
		{
			RootCommand root = CreateCommand();
			await root.InvokeAsync(args);
		}

		public static RootCommand CreateCommand()
		{
			return new RootCommand("A simple tool to manage DLCs.\r\nTHIS IS EXPERIMENTAL, USE AT YOUR OWN RISKS!")
			{
				new Option<string>("--network", "The network type (mainnet, testnet or regtest)")
				{
					Argument = new Argument<string>(),
					IsRequired = false
				},
				new Option<string>("--datadir", "The data directory")
				{
					Argument = new Argument<string>(),
					IsRequired = false
				},
				ShowInfoCommand.CreateCommand(),
				new Command("oracle", "Oracle commands")
				{
					AddSetOracleCommand.CreateCommand(false),
					AddSetOracleCommand.CreateCommand(true),
					RemoveOracleCommand.CreateCommand(),
					ListOracleCommand.CreateCommand(),
					ShowOracleCommand.CreateCommand(),
					GenerateOracleCommand.CreateCommand()
				},
				new Command("event", "Manage events")
				{
					AddEventCommand.CreateCommand(),
					ListEventsCommand.CreateCommand(),
					ShowEventCommand.CreateCommand(),
					GenerateEventCommand.CreateCommand(),
					new Command("attest", "Attest an event")
					{
						AttestAddCommand.CreateCommand(),
						AttestSignCommand.CreateCommand()
					}
				},
				new Command("dlc", "Manage DLCs")
				{
					GenerateDLCCommand.CreateCommand(),
					OfferDLCCommand.CreateCommand(),
					ShowDLCCommand.CreateCommand()
				}
			};
		}
	}
}
