using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NDLC.Messages;
using NDLC.Secp256k1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NDLC.CLI
{
	public class CreateOfferCommand : CommandBase
	{

		protected override async Task InvokeAsyncBase(InvocationContext context)
		{
			PSBTFundingTemplate template = GetPSBTTemplate(context.ParseResult.ValueForOption<string>("funding"));
			ECXOnlyPubKey oraclePubKey = GetOraclePubKey(context.ParseResult.ValueForOption<string>("oraclepubkey"));
			SchnorrNonce schnorrNonce = GetSchnorrNonce(context.ParseResult.ValueForOption<string>("nonce"));
			DLCTransactionBuilder builder = new DLCTransactionBuilder(true, null, null, null, Network);
			var timeouts = new Timeouts()
			{
				ContractMaturity = GetLockTime(context, "maturity") ?? new LockTime(0),
				ContractTimeout = GetLockTime(context, "expiration") ?? throw new CommandOptionRequiredException("expiration")
			};
		  	var ci = GetOutcomes(context.ParseResult.ValueForOption<string[]>("outcome"));
			var offer = builder.Offer(template, new OracleInfo(oraclePubKey, schnorrNonce), ci, timeouts);
			WriteObject(context, offer);
		}

		private ContractInfo[] GetOutcomes(string[]? outcomes)
		{
			string optionName = "outcome";
			if (outcomes is null || outcomes.Length is 0)
				throw new CommandOptionRequiredException(optionName);
			ContractInfo[] ci = new ContractInfo[outcomes.Length];
			for (int i = 0; i < outcomes.Length; i++)
			{
				var separator = outcomes[i].LastIndexOf(':');
				if (separator == -1)
					throw new CommandException(optionName, "Invalid outcome, the format should be \'outcome:reward\' where \'reward\' is in sats (\'1000 sats\') or in BTC (\'0.2\')");
				var outcome = outcomes[i].Substring(0, separator);
				var reward = outcomes[i].Substring(separator + 1);
				reward = reward.ToLowerInvariant().Trim();
				var satsSeparator = reward.IndexOf("sats");
				Money rewardMoney = Money.Zero;
				if (satsSeparator == -1)
				{
					rewardMoney = Money.Parse(reward);
				}
				else
				{
					rewardMoney = Money.Satoshis(long.Parse(reward.Substring(0, satsSeparator)));
				}
				if (rewardMoney < Money.Zero)
					throw new CommandException(optionName, "Invalid outcome, the reward can't be negative");
				ci[i] = new ContractInfo(outcome, rewardMoney);
			}
			return ci;
		}

		LockTime? GetLockTime(InvocationContext ctx, string optionName)
		{
			var v = ctx.ParseResult.ValueForOption<string>(optionName);
			if (string.IsNullOrEmpty(v))
				return null;
			if (!uint.TryParse(v, out var val))
				return null;
			return new LockTime(val);
		}

		private SchnorrNonce GetSchnorrNonce(string? nonce)
		{
			var optionName = "nonce";
			if (nonce is null)
				throw new CommandOptionRequiredException(optionName);
			if (!SchnorrNonce.TryParse(nonce, out var n) || n is null)
				throw new CommandException(optionName, "Invalid pubkey");
			return n;
		}

		private ECXOnlyPubKey GetOraclePubKey(string? pubkey)
		{
			var optionName = "oraclepubkey";
			if (pubkey is null)
				throw new CommandOptionRequiredException(optionName);
			try
			{
				var hex = Encoders.Hex.DecodeData(pubkey);
				if (hex.Length != 32 ||
					!ECXOnlyPubKey.TryCreate(hex, Context.Instance, out var r) || r is null)
				{
					throw new CommandException(optionName, "Invalid pubkey");
				}
				return r;
			}
			catch
			{
				throw new CommandException(optionName, "Invalid pubkey");
			}
		}

		private PSBTFundingTemplate GetPSBTTemplate(string? psbt)
		{
			var optionName = "funding";
			if (psbt is null)
				throw new CommandOptionRequiredException(optionName);
			if (!PSBTFundingTemplate.TryParse(psbt, Network, out var o) || o is null)
				throw new CommandException(optionName, "Invalid funding PSBT");
			return o;
		}
	}
}
