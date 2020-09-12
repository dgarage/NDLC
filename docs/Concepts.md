# Documentation
## General Concepts

A DLC can be seen as a smart contract involving two `parties`, a future `event`, a set of outcomes and a `payoff function`.
An `outcome` can be `attested` by an `oracle`. The `oracle` does not need to interact with either party, and its only role is to `attest` the outcome of the event.

The `payoff function` determines the two parties' profit or loss depending on which outcome get `attested`.

The `oracle` roles is to define the `event` and `attest` a single outcome of the event.

Let's call Alice and Bob the two parties of the contract, and Olivia the oracle.


## How to run this project?

The details are explained in the [README](../README.md), in the rest of this documentation, we will only documents the way to use the CLI, but not the details of how dotnet works.

So if we document you to run

```bash
info
```

What we really mean is that you need to run:

```bash
cd NDLC.CLI
```
And in that folder run.

```bash
dotnet run -c Release -- info
```

or, if you don't want to build: 

```bash
dotnet run -c Release --no-build -- info
```

## About the CLI

By default, the CLI assumes you are using Bitcoin mainnet and the location of a data directory where it will store its state.
```bash
info
```
Will tells you more.

In all the commands we are using you can change those by specifying global options before the first command.

```bash
--network testnet info
```

In this example, `info` is the first command, and `--network` is a global option, because it appears before the first command.
Every command might have children, or their own parameters. You can discover those by using `--help`.

You can invoke help at different levels:

Root level:
```bash
--help
```
`oracle` level:
```bash
oracle --help
```
`oracle list` level:
```bash
oracle list --help
```

In the documentation below, you can simulate Olivia, Alice and Bob by using different data directory for each of them.

For example:
```bash
--datadir Olivia oracle list
```

## How to create an oracle

So imagine that Olivia wants to run an oracle that other people might decide to use for their DLC.

```bash
oracle generate "awesomeoracle"
```

This will output her oracle's pubkey. She can always get it back later with.

```bash
oracle show "awesomeoracle"
```

Or list it with

```bash
oracle list
```

The pubkey of your oracle is its identity. Olivia will share it to the world.

## Alice and Bob adding Olivia as an oracle

Alice and Bob knows Olivia, and they can both see that Olivia is sharing her oracle's pubkey.
So each of them use `oracle add <oraclename> <pubkey>`:

```bash
oracle add "olivia" "ab291..."
```

The oracle name is arbitrary and local to bob and alice. They don't have to share the same name, but they have to share the same pubkey.

## Olivia decides to create a new event

Olivia decides she will attest the winner of the US election. So she creates an `event` that she will share with Alice and Bob.

So each of them use `event generate <eventfullname> <outcome1> <outcome2> <outcome...>`:
```bash
event generate "awesomeoracle/USElection2020" "Republicans" "Democrats" "Others"
```

The event full name is in the format `oraclename/eventname`. On Olivia's installation, the oracle's name she created was `awesomeoracle`, and event name is local and arbitrary `USElection2020`.

The command is giving the event's `nonce`, that she can share with the world along with the outcomes.

She can use `event list` and `event show` to get back the information of the event.

## Alice and Bob see Olivia will attest a new event

Alice and Bob can see the announcement of Olivia, so they will add this event with `event add <eventfullname> <outcome1> <outcome2> <outcome...>`

```bash
event add "olivia/us2020" "cd291..." "Republicans" "Democrats" "Others"
```

The event full name is in the format `oraclename/eventname`, event name is arbitrary and local to Alice/Bob.

## Olivia wants to attest the election

Now imagine Olivia wants to attest the election, she can use `event attest sign <eventfullname> <outcome>`:

```bash
event attest sign "awesomeoracle/USElection2020" "Republicans"
```

This will give back an attestation on the outcome that she can share with the world.

Note if Olivia tried to cheat by attesting two outcomes, by the way DLC works, Alice and Bob would be able to steal her private key.

## Alice and Bob add this attestation

Alice and Bob see the attestation and can add it to their state via `event attest add <eventfullname> <attestation>`.

```bash
event attest add "olivia/us2020" "ff3ea..."
```

Note if Olivia tried to cheat by attesting two outcomes, Alice/Bob could add the second attestations in the same way.
By doing so they will learn Olivia's private key and able to create attestations for her events.

You can test this behavior, with Olivia using the `-f` option:

```bash
event attest sign -f "awesomeoracle/USElection2020" "Democrats"
```

Then Alice/Bob

```bash
event attest add "olivia/us2020" "ecdfa..."
```

Then Alice and Bob can see Olivia's private key with

```bash
oracle show --show-sensitive olivia
```
