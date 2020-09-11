using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.Secp256k1;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NDLC.Tests
{
	public class CLITests
	{
		public CLITests(ITestOutputHelper log)
		{
			Log = log;
			Tester = new CommandTester(log);
		}

		public CommandTester Tester { get; }
		public ITestOutputHelper Log { get; }

		[Fact]
		public async Task CanShowInfo()
		{
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"info"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "generate",
				"neo"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"info"
			});
		}

		[Fact]
		public async Task CanManageEvents()
		{
			Log.WriteLine("Adding a wellknown oracle");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "add",
				"OutcomeObserver",
				"57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496"
			});
			Log.WriteLine("No events has been added yet");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "list"
			});
			Assert.Empty(Tester.GetLastOutput());
			Log.WriteLine("Adding an event that our oracle announced");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "add",
				"OutcomeObserver/Elections",
				"57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496",
				"Republican_win",
				"Democrat_win",
				"other"
			});
			Log.WriteLine("The event should show in the list");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "list"
			});
			Assert.Equal("OutcomeObserver/Elections" + Environment.NewLine, Tester.GetLastOutput());
			Log.WriteLine("The event should show in the list, if we filter by the oracle's name");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "list",
				"--oracle", "outcomeobserver"
			});
			Assert.Equal("OutcomeObserver/Elections" + Environment.NewLine, Tester.GetLastOutput());
			Log.WriteLine("But not with another oracle name");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "list",
				"--oracle", "blah"
			});
			Assert.Empty(Tester.GetLastOutput());
			Log.WriteLine("We should be able to see the details of the event");
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", GetDataDirectory(),
					"event", "show", "OutcomeObserver/Elections"
			});

			Log.WriteLine("Let's generate a new oracle, and an event from it");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "generate",
				"neo"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "generate", "neo/NeoElections",
				"Republicans", "Democrats", "Smith"
			});
			Log.WriteLine("It should be in the list");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "show", "neo/NeoElections"
			});

			Log.WriteLine("Let's attest it");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "sign", "neo/NeoElections", "Smith"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "show", "neo/NeoElections"
			});
			Assert.Contains("Attestation", Tester.GetLastOutput());
			Log.WriteLine("Let's check if we can enforce two attestation");
			await Tester.AssertInvoke(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "sign", "neo/NeoElections", "Republicans"
			}, 1);
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "sign", "-f", "neo/NeoElections", "Republicans"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "show", "neo/NeoElections"
			});

			Log.WriteLine("Let's try to manually add an attestation");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "add",
				"morpheus", "daa9d8cdc6a594055efbade8312ec1621e56ec2cb1ed571181bc2460602a61f8"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "add",
				"morpheus/elections", "450f5a8e3da27c79080b853cd925772a929ff8dc5559a8bf81d432d1c9de22a7",
				"Republicans", "Democrats", "Smith"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "add",
				"morpheus/elections", "a3b85eb6275cceb8bedb19076f1aab23a4021b3158faf6b9fdc4910586357c79"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "show", "morpheus/elections"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "add",
				"morpheus/elections", "13bdfe5cb1cb7a2bced86a539b446cfc959d507fc10e777b1c4a39562e5b58e9"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "show", "morpheus", "--show-sensitive"
			});
			Assert.Contains("Has private key: True", Tester.GetLastOutput());
		}

		[Fact]
		public async Task CanManageOracles()
		{
			await Tester.AssertInvoke(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "set",
				"neo", "57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496"
			}, 1);
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "add",
				"neo", "57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496"
			});
			await Tester.AssertInvoke(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "add",
				"neo", "57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496"
			}, 1);
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "set",
				"neo", "16735f228c76e81e1ca671521991f682ad50a79ad7a44fb073f5a5462a4243ba"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "show",
				"neo"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "list"
			});
			Assert.Equal("neo\t16735f228c76e81e1ca671521991f682ad50a79ad7a44fb073f5a5462a4243ba" + Environment.NewLine, Tester.GetLastOutput());
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "remove",
				"neo"
			});
			await Tester.AssertInvoke(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "remove",
				"neo"
			}, 1);
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "list"
			});
			Assert.Empty(Tester.GetLastOutput());
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "generate",
				"neo"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "show",
				"neo"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "show",
				"neo", "--show-sensitive"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "generate",
				"neo2"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"oracle", "show",
				"neo2", "--show-sensitive"
			});
		}

		[Fact]
		public async Task CanCreateAndReviewOffer()
		{
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"--network", "testnet",
				"offer", "create",
				"--oraclepubkey", "57caa081b0a0e9e9413cf4fb72ddc2630d609bdf6a912b98c4cfd358a4ce1496",
				"--nonce", "92ba989222e76cf0cb263fedd67587812110bde1dc1468bef63c8f6974692ea1",
				"--funding", "cHNidP8BALwCAAAAAhyfislzQS3Q4xtaCVbSKztXgCbkXHvqoN0ZQPVwLtiRAQAAAAD+////0sWvPdN8zMIlXWZ5TP2LCmS2tXn0mq5Jy/H+Dr3sVXcAAAAAAP7///8DVwQAAAAAAAAWABRG14/IKu1cwguA1Zpf38w8Uo2WEvFdIAAAAAAAFgAUPDZOW4cQt7HtQ6LTckTs/EdezyIAWmICAAAAABl2qRQCVQOSzWKNBuwbxvjMoah8d4g5wIisI/EbAAABAR/6lFEBAAAAABYAFJwzhGgakahc93MQnjbvxXmq9K/hIgYDsP0UpR8Mgp0gv1V7iuQ64o/esYaR0uVt1w6vso9uL14Yo4MFf1QAAIABAACAAAAAgAEAAAACAAAAAAEBHwAtMQEAAAAAFgAUGArwzyUB0DY+N9TI0lFh2DeURigiBgK4w7vZjBER+7G0ky5uQmb6c20ESovjwPaOS7K/pfhtbxijgwV/VAAAgAEAAIAAAACAAAAAAAYAAAAAIgICzVzQn25qzI10r2VfcIJ6/wedvUpz1QO3LCSN1FIEWKoYo4MFf1QAAIABAACAAAAAgAAAAAAHAAAAACICApmHY8GtEZA8G/dKfXrqg31ArW+8kKK+5PXdMYnbMafKGKODBX9UAACAAQAAgAAAAIABAAAABAAAAAAA",
				"--outcome", "Republican_win:10000sats",
				"--outcome", "Democrats_win:0",
				"--outcome", "other:0.2",
				"--expiration", "1000"
			});
			var offer = Tester.GetResult<Offer>();
			Assert.Equal(Money.Satoshis(10000), offer.ContractInfo[0].Payout);
			Assert.Equal(Money.Coins(0.2m), offer.ContractInfo[2].Payout);
			Assert.Equal("tb1q8smyuku8zzmmrm2r5tfhy38vl3r4anezy6ftse", offer.ChangeAddress.ToString());

			var offerStr = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--network", "testnet",
				"offer", "review",
				offerStr
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--network", "testnet",
				"offer", "review", "-h",
				offerStr
			});
		}


		bool created;
		private string GetDataDirectory([CallerMemberName]string testName = null)
		{
			if (!created)
			{
				if (Directory.Exists(testName))
					Directory.Delete(testName, true);
				Directory.CreateDirectory(testName);
				created = true;
			}
			return testName;
		}
	}
}
