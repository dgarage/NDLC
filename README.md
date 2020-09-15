# NDLC

DISCLAIMER: THIS PROJECT IS EXPERIMENTAL BASED ON A PROTOCOL WHICH IS STILL EVOLVING EVERYDAY. USE WITH CAUTION.

I WILL TAKE NO ATTEMPT AT MAINTAINING BACKWARD COMPATIBILITY AT THIS STAGE.

## What are DLC?

Smart contracts are an often touted feature of cryptographic currency systems such as Bitcoin, but they have yet to see widespread financial use. Two of the biggest hurdles to their implementation and adoption have been scalability of the smart contracts, and the difficulty in getting data external to the curency system into the smart contract. Privacy of the contract has been another issue to date. Discreet Log Contracts are a system which addresses the  scalability and privacy concerns and seeks to minimize the trust required in the oracle which provides external data. The contracts are discreet in that external observers cannot detect the presence of the contract in the transaction log. They also hinge on knowledge of a discrete logarithm, which is a plus.

Source: [Discreet Log Contracts](https://adiabat.github.io/dlc.pdf) by Thaddeus Dryja.

This repository hosts repository is an implementation of the idea, with a variant: We use adaptor signatures.

Adaptor signatures allows us to simplify the protocol and increase privacy.
Without adaptor signatures:
* In a non cooperative case situation, a DLC takes 4 transactions, 3 transactions in a cooperative case.
* The parties need to monitor the blockchain actively to make sure the other party does not cheat.

With adaptor signatures:
* Cooperative, or non-cooperative, a DLC takes only 3 transactions and less roundtrip of communications between the parties.
* The parties don't have to monitor the blockchain, as they have no way to cheat.

## How to use this repository

This repository contains:

* A library `NDLC`, still not released on Nuget, but which aims to implement the [specification](https://github.com/discreetlogcontracts/dlcspecs).
* A CLI program `NDLC.CLI`, which allows you be an oracle and make DLC contract with other peers.

You can build using [.NET Core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) or higher.

We intend to release easy to use packages in the future, but this work is still experimental.

You can build with:

```csharp
cd NDLC.CLI
dotnet build -c Release
```

You can run with

```csharp
cd NDLC.CLI
dotnet run -c Release
```

By default, `dotnet run` also build, so if you just want to run, you can use.

```csharp
cd NDLC.CLI
dotnet run -c Release --no-build
```

You need to pass parameters to the CLI interface after `--`.

```csharp
cd NDLC.CLI
dotnet run -c Release --no-build -- info
```

We will make that easier when the protocol and this repository stabilizes.

## Documentation

Please go to our [Documentation page](docs/Concepts.md).

## Special thanks

Thanks to the contribution of cryptographers for their work on the low level mathematic requirements necessary to make that DLC with adaptor sigs a reality.

* Thaddeus Dryja for the idea of [Discreet Log Contracts](https://adiabat.github.io/dlc.pdf).
* Andrew Poelstra for the idea of [adaptor signatures](https://download.wpsoftware.net/bitcoin/wizardry/mw-slides/2018-05-18-l2/slides.pdf).
* LLoyd Fournier for how to actually leverage those adaptor signatures in Bitcoin [adaptor signatures](https://github.com/LLFourn/one-time-VES/blob/master/main.pdf).
* Nadav Kohen for the idea of [payment points](https://diyhpl.us/wiki/transcripts/lightning-conference/2019/2019-10-20-nadav-kohen-payment-points/).
* Jonas Nick for the implementation of [adaptor signature](https://github.com/jonasnick/secp256k1/pull/14).
* Thanks to Ruben Somsen for explaining me in human language what [adaptor sigs](https://www.youtube.com/watch?v=TlCxpdNScCA&feature=youtu.be) are.

And thanks to those who are working on a specification which will make it possible for several implementation to talk to each other and who helped me Thibaut Le Guilly, Ben Carman, Chris Stewart, Ichiro Kuwahara.

Thanks to all, I could focus solely on development rather than research and protocol design.

## Licence

MIT
