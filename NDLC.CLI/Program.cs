using NBitcoin.Secp256k1;
using NDLC.CLI.DLC;
using NDLC.CLI.Events;
using NDLC.Messages;
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
				new Option<bool>("-mainnet", "Use mainnet (default)")
				{
					IsRequired = false
				},
				new Option<bool>("-testnet", "Use testnet")
				{
					IsRequired = false
				},
				new Option<bool>("-regtest", "Use regtest")
				{
					IsRequired = false
				},
				new Option<string>("--datadir", "The data directory")
				{
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
					ListDLCCommand.CreateCommand(),
					OfferDLCCommand.CreateCommand(),
					SetupDLCCommand.CreateCommand(),
					ReviewDLCCommand.CreateCommand(),
					AcceptDLCCommand.CreateCommand(),
					CheckSignatureDLCCommand.CreateCommand(),
					StartDLCCommand.CreateCommand(),
					ExecuteDLCCommand.CreateCommand(),
					ExtractDLCCommand.CreateCommand(),
					ShowDLCCommand.CreateCommand()
				}
			};
		}
	}
}
