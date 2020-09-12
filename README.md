# NDLC

DISCLAIMER: THIS PROJECT IS EXPERIMENTAL BASED ON A PROTOCOL WHICH IS STILL EVOLVING EVERYDAY. USE WITH CAUTION.

## What are DLC?

Smart contracts are an often touted feature of cryptographic currencysystems such as Bitcoin, but they have yet to see widespread financial use.Two of the biggest hurdles to their implementation and adoption have beenscalability of the smart contracts, and the difficulty in getting data externalto the curency system into the smart contract.  Privacy of the contract hasbeen  another  issue  to  date.   Discreet  Log  Contracts  are  a  system  whichaddresses  the  scalability  and  privacy  concerns  and  seeks  to  minimize  thetrust required in the oracle which provides external data.  The contracts arediscreet in that external observers cannot detect the presence of the contractin the transaction log.  They also hinge on knowledge of adiscretelogarithm,which is a plus.

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

## Licence

MIT