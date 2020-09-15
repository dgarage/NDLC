using NBitcoin;
using NBitcoin.DataEncoders;
using NDLC.Messages;
using NDLC.Messages.JsonConverters;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
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
		public async Task CanMakeContract()
		{
			var alice = CreateDataDirectory("Alice");
			var bob = CreateDataDirectory("Bob");
			var parties = new[] { alice, bob };
			var olivia = CreateDataDirectory("Olivia");
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", olivia,
				"oracle", "generate",
				"olivia"
			});
			var oliviaPubKey = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", olivia,
				"event", "generate", "olivia/coolestguy",
				"Ninja", "Samurai", "Cowboy", "NicolasDorier"
			});
			var nonce = Tester.GetLastOutput();
			foreach (var party in parties)
			{
				await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", party,
					"oracle", "add",
					"olivia", oliviaPubKey
				});
				await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", party,
					"event", "add",
					"olivia/coolestguy", nonce,
					"Ninja", "Samurai", "Cowboy", "NicolasDorier"
				});
			}
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "offer",
					"BetWithBob", "olivia/coolestguy",
					"Ninja:-0.4",
					"cowboy:-1.2",
					"Samurai:0.6",
					"NicolasDorier:1"
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "BetWithBob", 
				});

			var aliceSigner = new Key();
			var offerFunding = CreateOfferFunding(Money.Coins(1.2m), aliceSigner);
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "setup", "BetWithBob", offerFunding
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "BetWithBob",
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "--offer", "BetWithBob",
				});
			var offer = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "--offer", "--json", "BetWithBob",
				});
			
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "review", offer
				});

			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "accept", "BetWithAlice", offer
				});

			var bobSigner = new Key();
			var acceptFunding = CreateOfferFunding(Money.Coins(1.0m), bobSigner);

			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "setup", "BetWithAlice", acceptFunding
				});

			var acceptorSigs = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "show", "BetWithAlice"
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "checksigs", acceptorSigs
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "BetWithBob", "--funding"
				});
			var aliceFunding = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "show", "BetWithAlice", "--funding"
				});
			var bobFunding = Tester.GetLastOutput();
			Assert.Equal(aliceFunding, bobFunding);
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "BetWithBob"
				});

			var funding = PSBT.Parse(aliceFunding, Network.Main);
			funding.SignWithKeys(aliceSigner);
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "start", "BetWithBob", funding.ToBase64()
				});
			var signMessage = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "BetWithBob"
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", alice,
					"dlc", "show", "--refund", "BetWithBob"
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "show", "--refund", "BetWithAlice"
				});
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "checksigs", signMessage
				});
			funding = PSBT.Parse(bobFunding, Network.Main);
			funding.SignWithKeys(bobSigner);
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", bob,
				"dlc", "start", "--psbt", "BetWithAlice", funding.ToBase64()
			});
			// Should be fully signed
			var fullySigned = PSBT.Parse(Tester.GetLastOutput(), Network.Main);
			fullySigned.Finalize();
			fullySigned.ExtractTransaction();
			// Ready!
			await Tester.AssertInvokeSuccess(new string[]
				{
					"--datadir", bob,
					"dlc", "show", "BetWithAlice"
				});

			// Olivia attest NicolasDorier to be the coolest guy
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", olivia,
					"event", "attest", "sign", "olivia/coolestguy", "NicolasDorier"
			});
			var attestation = Tester.GetLastOutput();
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", bob,
					"dlc", "execute", "BetWithAlice", attestation
			});
			var cet = Tester.GetLastOutput();
			// Can Alice extract the attestation through CET?
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", alice,
					"dlc", "extract", "BetWithBob", cet
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", alice,
					"dlc", "execute", "BetWithBob"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
					"--datadir", bob,
					"dlc", "execute", "BetWithAlice"
			});
		}

		private string CreateOfferFunding(Money money, Key signer)
		{
			TransactionBuilder builder = Network.Main.CreateTransactionBuilder();
			builder.AddCoins(new Coin(new OutPoint(RandomUtils.GetUInt256(), 0), new TxOut(Money.Coins(50.0m), signer.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit))));
			builder.Send(new Key().PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), money);
			builder.SetChange(new Key().PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
			builder.SendEstimatedFees(new FeeRate(10.0m));
			return builder.BuildPSBT(false).ToBase64();
		}

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
				"morpheus", "399e2348d8b5db8161a915af8efd900aa7241e6eed6d28ff8cc77afc505e8258"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "add",
				"morpheus/elections", "2fe5252fa3c676a4522f2e8a05d2bc3a79c865ca17a44ab6d168bcc79194491f",
				"Republicans", "Democrats", "Smith"
			});
			await Tester.AssertInvokeSuccess(new string[]
			{
				"--datadir", GetDataDirectory(),
				"event", "attest", "add",
				"morpheus/elections", "b7728d379612731b26db15249106d9b59855c4c3957454415a93cb5daa065803"
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
				"morpheus/elections", "67812f3bd89a6ccc27ced51030f604a738ee9ac1726d21c22cb7282b2585f05e"
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
		public string CreateDataDirectory(string name, [CallerMemberName] string testName = null)
		{
			if (!Directory.Exists(testName))
				Directory.CreateDirectory(testName);
			var fullname = Path.Combine(testName, name);
			if (Directory.Exists(fullname))
				Directory.Delete(fullname, true);
			Directory.CreateDirectory(fullname);
			return fullname;
		}
	}
}
