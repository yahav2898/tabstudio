# Design: Smart contracts, dApps and Supra

> Phase 12 of the [roadmap](../ROADMAP.md). This document records what Supra offers developers today,
> what TabStudio builds on top of it, and in what order. Facts about Supra are linked to their sources
> and were checked in October 2026.

## Why Supra first

Supra is a layer-1 chain that ships the services most dApps otherwise buy from third parties as part of
the chain itself: price oracles, verifiable randomness, automation and cross-chain transfers. That makes it
an unusually good fit for an IDE: a template can use all of them without any account outside Supra.

It also has two developer surfaces that a single IDE can serve together:

| Surface | Language | Network details |
| --- | --- | --- |
| SupraMVM | Move | Mainnet chain id 8, testnet 6, devnet 50; RPC `https://rpc-{mainnet,testnet,devnet}.supra.com` |
| SupraEVM | Solidity | Devnet chain id 535600, JSON-RPC `https://rpc-devnet.supra.com/rpc/v1/eth` |

Source: [Supra docs, llms-full.txt](https://docs.supra.com/llms-full.txt). The docs list only a devnet for
SupraEVM; a testnet and a mainnet were announced for 2026 ([Decrypt](https://decrypt.co/349228/multivm-support-now-live-on-a-supra-testnet-expanding-to-evm-compatibility)),
so the network list in TabStudio is data, refreshed from the docs, not code.

## What Supra provides to developers

| Building block | What it does | How a contract uses it |
| --- | --- | --- |
| Data feeds (push and pull oracles) | Prices published on chain as "S-values", identified by a numeric pair index | Solidity: `ISupraSValueFeed.getSvalue(pairIndex)`, `getSvalues`, `getTimestamp`. Move: `supra_oracle::supra_oracle_storage::get_price(pair_id)` ([docs](https://docs.supra.com/oracles/data-feeds/push-oracle)) |
| dVRF | Verifiable random numbers | Solidity: `ISupraRouterContract.generateRequest(callbackSig, rngCount, confirmations, clientWallet)`, then a callback `(uint256 nonce, uint256[] rngList)` that checks the caller is the router ([docs](https://docs.supra.com/dvrf/build-with-supra-dvrf/request-random-numbers/evms)) |
| Automation | Scheduled contract calls with no external keepers | `supra move automation register --function-id … --task-max-gas-amount … --task-gas-price-cap … --task-expiry-time-secs … --task-automation-fee-cap …`; tasks are listed by `0x1::automation_registry` ([docs](https://docs.supra.com/automation/your-first-automation-task/register-your-automation-task)) |
| Cross-VM transfers | Move SUPRA between SupraEVM and SupraMVM | EVM precompile `0x…53555002` `crossToMove(bytes32)`; Move `0x1::evm_settlement::deposit_to_evm` ([docs](https://docs.supra.com/llms-full.txt)) |
| SupraNova | Bridging to Ethereum and layer 2s (HyperNova, Hyperloop) | Bridge contracts and SDK |
| PoEL and iAssets | Multi-asset liquid staking that brings outside liquidity to Supra DeFi | Integration guide in the [PoEL docs](https://docs.supra.com/proof-of-efficient-liquidity-poel/overview) |
| Tooling | Supra CLI (in Docker), TypeScript SDK, Python SDK, Supra Move VS Code extension, StarKey wallet, SupraScan explorers | — |

### Supra for AI agents

| Product | What it is | Status |
| --- | --- | --- |
| [SupraFX](https://suprafx.ai/agents) | A cross-chain RFQ trading venue with BFT settlement, across Supra, Ethereum, Robinhood Chain, Arc and Tempo | Live |
| [suprafx-agent-sdk](https://github.com/jtobkin/suprafx-agent-sdk) | A TypeScript SDK and the `suprafx-mcp` MCP server: 13 read tools (`get_orderbook`, `get_balances`, `get_oracle_price`, `preflight`, …) and 5 write tools (`submit_rfq`, `place_quote`, `accept_quote`, `cancel_rfq`, `withdraw_quote`). Trading uses a delegate key with per-asset caps and an expiry enforced on chain; the master key never leaves the wallet | Live, MIT licensed |
| SupraOS | A self-hosted agent platform whose rules are enforced on chain | Alpha since April 2026 ([BlockEden](https://blockeden.xyz/blog/2026/05/06/supra-supraos-life-os-self-hosted-ai-agent-platform/)) |
| Threshold AI Oracles | AI committees whose answers reach contracts with a threshold signature | Announced May 2025, testnet "coming soon" ([The Defiant](https://thedefiant.io/news/press-releases/supra-introduces-their-new-ai-oracle-protocol-making-web3-smarter-and-more-secure)) |

## What TabStudio builds

The goal is one sentence: **the fastest way in the world to go from an idea to a working Supra dApp, on any
device.** Everything below serves that.

### 1. Languages and toolchains

- **Solidity**: the Solidity language server for completion and errors, `solc`, and Foundry (`forge`, `cast`)
  from the Toolchain Store, built for Android. SupraEVM networks are preconfigured.
- **Move**: syntax, completion and errors through the Move language server, and package compile and publish.
  The Supra CLI ships only as a Docker image, so TabStudio publishes through the REST API instead
  (the same path the TypeScript SDK uses), with compiled bytecode from an on-device Move compiler.
- **C#**: TabStudio is a .NET IDE first. Supra has SDKs for TypeScript and Python but not .NET, so TabStudio
  ships an open-source **Supra SDK for .NET** (accounts, resources, events, transactions, faucet, oracle
  prices, SupraEVM through JSON-RPC, and a SupraFX client). It works in TabStudio and in any .NET app.

### 2. The Supra panel

A native panel that replaces the explorer features of the VS Code extension and adds what a tablet needs:

- A network switcher (mainnet, testnet, devnet, SupraEVM devnet), with chain ids and RPCs filled in.
- Accounts: create a profile, see the SUPRA balance, and fund devnet and testnet accounts from the faucet in one tap.
- An account explorer for resources, published modules and their source, and events by transaction hash.
- A live oracle ticker for any pair index, with the value's age.
- Automation tasks for the account, with their state (pending, active, cancelled).
- Keys live in the Android Keystore. TabStudio never asks for a master key; trading agents use SupraFX delegate keys.

### 3. Templates that work on the first tap

| Template | Uses |
| --- | --- |
| Price-triggered vault (Move) | Oracle price + an automation task that checks it every cycle |
| Fair raffle (Solidity) | dVRF request and callback, deployable to SupraEVM or any EVM chain dVRF supports |
| Cross-VM payment (Move + Solidity) | `crossToMove` and `deposit_to_evm` in one solution |
| SupraFX market maker (C# or TypeScript) | The SupraFX API, dry-run by default, guarded writes |
| On-chain dashboard (C#) | The .NET SDK reading balances, events and prices |

Every template opens with its tests passing and a one-tap deploy to devnet.

### 4. Migration Doctor

The Compatibility Doctor, for contracts. Open an existing Solidity dApp and it finds the integrations that
tie it to another chain's services, and shows the Supra equivalent:

| Found in the code | Supra equivalent |
| --- | --- |
| Chainlink `AggregatorV3Interface.latestRoundData()` | `ISupraSValueFeed.getSvalue(pairIndex)` with a staleness check on `getTimestamp` |
| Chainlink VRF `requestRandomWords` / `fulfillRandomWords` | `ISupraRouterContract.generateRequest` and a router-only callback |
| Chainlink Automation `checkUpkeep` / `performUpkeep` | A SupraEVM automation task |

The agent applies the changes in a branch and the user reviews the diff.

### 5. Agents and MCP

- **SupraFX in the agent panel.** TabStudio's agent is an MCP client. It connects to `suprafx-mcp`, so the user can
  ask about balances, the orderbook or oracle prices. Write tools stay in the server's guarded mode, and every
  trade is confirmed in TabStudio's own UI.
- **A developer MCP server for Supra.** SupraFX's MCP server is about trading. TabStudio ships an open-source MCP
  server for *building* on Supra: network info, faucet, account resources and modules, events, oracle prices,
  package publish, automation task registration and dVRF setup. It runs inside TabStudio and works with any
  MCP client, including Claude Code and Cursor.
- **Docs-aware answers.** Supra publishes its documentation as [`llms-full.txt`](https://docs.supra.com/llms-full.txt);
  the agent reads it, so answers about Supra APIs come from Supra's own docs.

## Order of work

1. Supra SDK for .NET and the Supra panel (no toolchains needed, works in C# projects immediately).
2. Solidity toolchain and the SupraEVM templates (dVRF raffle, oracle reader).
3. Move toolchain and the Move templates (vault with automation, cross-VM payment).
4. The developer MCP server and SupraFX in the agent panel.
5. Migration Doctor.

## Safety rules

- Testnet and devnet are the default network for every template and every deploy button.
- No master private key is ever requested, stored or sent. Trading agents use delegate keys with caps and expiry.
- Every transaction that spends funds is shown in full and confirmed by the user, even when an agent proposed it.
- Generated trading code runs in dry-run mode until the user turns it off.
