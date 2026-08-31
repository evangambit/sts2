# Slay the Spire 2 Emulator

> Forked from [Zamiell/slay-the-spire-2-emulator](https://github.com/Zamiell/slay-the-spire-2-emulator)
> (MIT). This fork adds macOS support and is the base for an AlphaZero-style agent —
> start with [HANDOFF.md](HANDOFF.md) (current state + how-to + next steps), then
> [PLAN.md](PLAN.md) for the design and roadmap. Decompiled game source
> (`decompiled/`) is not redistributed here; regenerate it locally from your own
> copy of the game with `scripts/decompile.sh`.

This repository contains a high-performance emulator for Slay the Spire 2 combat and
full-run logic, targeting **v0.107.1**. The core simulator is written in C# and published
as a NativeAOT shared library, then loaded from Python through `ctypes` and exposed as
Gymnasium environments for reinforcement learning experiments.

The project's organising constraint is **fidelity to the real game, demonstrated rather
than asserted**. Expected values are read off the decompiled source, questions the source
cannot answer are settled by capturing the running game, and every divergence found so far
is written down in [docs/divergence-catalog.md](docs/divergence-catalog.md) with what
exposed it and how to reproduce it.

## What is included

- `src/Sts2Emulator`: C# combat and run simulator targeting .NET 9 NativeAOT.
- `src/Sts2Emulator/Core`: combat state, turn flow, card effects, buffs, enemy AI, potions,
  relic effects, run state, RNG streams, map routing, rewards, shops, events, rests, and
  reward calculation.
- `src/Sts2Emulator/Generated`: card, enemy, potion, power, relic and encounter definitions
  generated from the decompiled source by `scripts/extract_data.py`. Nothing in here is
  hand-maintained — a hand-kept list of ids is a claim about the game that nothing rechecks.
- `src/Sts2Emulator/Interop`: native exports used by Python.
- `src/sts2_gym`: Python `ctypes` bindings plus Gymnasium wrappers for single-combat and
  full-run training. `names.py` reads the id-to-name tables straight out of the emulator's
  own source, for anything that has to show a state to a person.
- `src/Sts2Emulator.Tests`: xUnit tests for combat and run behaviour.
- `src/Sts2Emulator.Tests/Cards`: per-card test classes, the tests generated from live card
  captures, and the coverage guard that fails the build when an implemented card has no
  tests. See [AGENTS.md](AGENTS.md) for the conventions.
- `tests/python`: tests that must cross the native boundary — the observation layout, the
  command layer, and the live fixtures.
- `scripts`: build, data extraction, patch update, trace validation, full-run trace capture,
  live per-card and per-event capture, the source-vs-emulator audits, and training scripts.
  `scripts/play.py` is the interactive client that lets a person play a run by hand.

## Architecture

```text
Python RL training
    |
    | Gymnasium + NumPy observations
    v
src/sts2_gym
    |
    | ctypes
    v
out/Sts2Emulator.{dylib,so,dll}
    |
    | NativeAOT C# combat + run engine
    v
src/Sts2Emulator
```

The Python environment calls the native library in-process, avoiding sockets or
serialization overhead.

`Sts2RunEnv` exposes two input layers:

- `env.step(action, target=-1)` is the low-level Gymnasium API used by RL training. The
  integer action is phase-dependent and is paired with `env.action_masks()`.
- `env.command(payload)` accepts STS2MCP-style command payloads such as
  `{"action": "play_card", "card_index": 0}` or `{"action": "choose_map_node", "index": 1}`
  and translates them to the integer action API. Replay and parity tooling should prefer
  this command layer so fresh STS2MCP `.replay` files exercise the same command shape used
  by the game mod.

### What belongs where

The emulator has two layers with distinct responsibilities: C# owns canonical
deterministic game state and Python owns reinforcement-learning integration.

**C# (`src/Sts2Emulator`)** handles **combat and full-run simulation**: card effects, enemy
AI, buff/debuff resolution, draw and discard, damage calculation, RNG streams, map routing,
encounter selection, rewards, shops, events, rests, relic pickup effects, gold,
deck/relic/potion state, and floor progression. This layer is parity-critical and is
compiled to native code.

**Python (`src/sts2_gym`)** handles **bindings and Gymnasium wrappers**. It loads the
NativeAOT library with `ctypes`, exposes observations/action masks/info dictionaries, and
runs training/evaluation scripts without mutating canonical full-run state.

When adding new mechanics, put deterministic game behaviour in C# and keep Python as an
interop/training layer. Full-run parity should be implemented against decompiled C#
semantics rather than re-created in Python.

## Current scope

### Content

| pool          | cards | implemented | with a test suite |
| ------------- | ----: | ----------: | ----------------: |
| Ironclad      |    87 |          85 |                86 |
| Silent        |    88 |          88 |                88 |
| Defect        |    88 |          88 |                88 |
| Necrobinder   |    88 |          88 |                88 |
| The Regent    |    88 |          88 |                88 |
| Colourless    |    64 |          64 |                64 |
| Event / Token |    41 |          38 |                 2 |
| Curse/Status  |    30 |          13 |                 3 |

(Ironclad's counts differ by pool vs id class — the pool excludes a few cards the class
carries, and vice versa; it is tested end to end either way.)

All five character pools have been captured card-by-card against the live game. Each
character's own resource is modelled: the Necrobinder's **Osty** pet, the Regent's **stars**
and **Forge/Sovereign Blade**, and the Defect's **orb queue** and Focus.

Beyond cards: 171 of 296 relics — every one of the 122 an ordinary run can be handed has
been read against the source — plus 111 monsters with their movesets, the event pool,
potions, shops, rest sites, map routing and Neow.

### What is verified, and how

Three independent mechanisms, because they catch different things:

1. **Hand-written tests**, with expected values read off `decompiled/` and cited in each
   file. `CardCoverageTests` fails the build when a card gains an implementation without
   gaining tests, and the untested remainder sits in a `Pending` burn-down that cannot go
   stale in either direction.
2. **Live capture.** `scripts/capture_card.py` stages a card in the running game and commits
   the before/after as a fixture; `generate_card_capture_tests.py` turns 309 of those into
   tests whose every number is the game's. This settles what source cannot: effect ordering,
   rounding, what a power sees mid-effect. Five fixtures that cannot be rebuilt live in
   `tests/fixtures/cards/blocked/`, each with a written reason.
3. **Source audits.** `audit_cards.py` and `audit_relics.py` track which cards and relics
   have actually been _read_ against the current source, keyed by a digest of that source so
   a note goes stale the moment the game patches. Every card with a test suite has now been
   read — the number that matters is "tested but unread", cards that LOOK covered, and it
   is zero. `audit_shared_card_bodies.py` catches the
   `switch`-label slip that has produced a dozen defects. `audit_enemy_moves.py`,
   `audit_card_keywords.py` and `audit_ascension_literals.py` report zero.

Reading and capturing are not substitutes for each other, and the project has measurements
for it in both directions. The Defect pool arrived fully read and fully tested, and live
capture still found three defects. The Necrobinder and Regent pools arrived fully captured,
and reading them found 21 of 45 and 13 of 27 — so **one card in two that nobody has read is
wrong, whatever its capture says.** The Silent's already-worked-over tail ran 4 in 30, which
is what a second pass over the same pool is worth.

This is not yet a full game emulator. Exact Neow/shop/reward/event odds, the 125 unmodelled
relics, and expanded trace parity are still future work. The nearest gaps: 49 event-pool relics that are
wired up and unread, and six act-2 events the emulator cannot yet reach. Every event an
ordinary run CAN reach has now been read against its source, implemented and captured —
51 of 57 events carry live fixtures, and the six that do not are the unreachable ones.

## Requirements

- .NET 9 SDK
- Python 3.11+ (3.12 recommended; 3.14 is too new for the pinned torch/numpy)
- uv
- Native build tools required by .NET NativeAOT for the target platform
- Python packages managed by uv from `pyproject.toml`

Install Python dependencies:

```bash
uv sync
```

If .NET was installed with Microsoft's `dotnet-install.sh` rather than a package manager,
`dotnet` is not on `PATH` for non-login shells and `scripts/build.sh` will not find it:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
```

## Build the native library

```bash
bash scripts/build.sh
```

The script picks the runtime from `uname` — pass one explicitly to override
(`osx-arm64`, `linux-x64`, `win-x64`). Or publish directly:

```bash
dotnet publish "src/Sts2Emulator/Sts2Emulator.csproj" -c Release -r osx-arm64 --self-contained -o "out"
```

The Python bindings look for `out/Sts2Emulator.dylib` (macOS), `.so` (Linux) or `.dll`
(Windows). Set `STS2_LIB_PATH` to a directory containing the native library to take
precedence over `out`. To prevent stale native-code rollouts, Python fails fast if the
loaded library is older than the C# source files or does not export the required native API
version, and the error names the rebuild command for your platform. Set
`STS2_ALLOW_STALE_NATIVE=1` only when deliberately testing an older build.

## Run tests and checks

```bash
# C# suite (~3650 tests, about 2 minutes)
dotnet test src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj

# Python suite — drives the live dylib through ctypes, so build first
uv run python -m unittest discover -s tests/python

# Everything, including lint and formatting
bash lint-and-test.sh
```

The Python suite is **not** part of `dotnet test`, and it is the only thing that exercises
the native boundary — an observation field can go stale in it for days without any C# test
noticing. Run it after C# changes that touch `CombatState` or the interop layer.

The source audits are cheap and worth running before a commit that touches cards or relics:

```bash
uv run python scripts/audit_cards.py            # what has been read, and what has drifted
uv run python scripts/audit_relics.py
uv run python scripts/audit_shared_card_bodies.py
uv run python scripts/card_pair.py Bury Reap    # decompiled source beside the emulator's arm
```

For the questions decompiled source cannot answer, capture the running game:

```bash
uv run python scripts/capture_card.py --card MoltenFist --power VULNERABLE_POWER=2
uv run python scripts/generate_card_capture_tests.py
```

That needs the game running with STS2MCP; see [AGENTS.md](AGENTS.md) for the conventions and
for what the capture harness deliberately refuses to do.

## Play a run by hand

```bash
uv run python scripts/play.py                 # a random seed
uv run python scripts/play.py --seed CLIPLAY  # the same run every time
```

An interactive terminal client for the same `Sts2RunEnv` the agent uses. It reads the
observation, names everything in it, labels every action the mask allows, and asks a
person to pick one — combat, map, shop, rest, event, reward and card-select screens
included. `help` lists the meta-commands: `deck`, `relics`, `map`, `log`, `state`, and
`undo`, which restores a faithful `clone()` of the position before the last move.

Choosing where to go draws the whole act map — every node, every edge, the floor in the
gutter and `@` where the run is standing — with the nodes the mask allows marked `[x]`:

```text
   17               B          m monster · E elite · ? unknown · $ shop
                    |          t treasure · r rest site · B boss
        +-----------+---+
        |               |
   16   r               r
        | \           / |
   15   ?   E       ?   m
        | /         |     \
   14   ?           E       E
        ...
    8   r   m       m          [r]
        | / |     / |           |
    7   E   r   m   $           @
        0   1   2   3   4   5   6
```

Aiming is `<action> <enemy>`, so `0 2` plays hand card 0 at the second enemy standing.

It shows the run **exactly as the observation carries it and no more**, which is the point
of having it: the screens are the fastest way to reach a state a fixture would otherwise
have to build, and the only reader that puts a name to every id at once — so a wrong card,
a wrong intent or an option that should not be on a screen reads as something a player
notices. Where the emulator models nothing there is nothing to show: an event's options
are numbered rather than named, the draw pile is a count rather than a list, and the
Crystal Sphere's board stays under its fog (see
[docs/agent-interface.md](docs/agent-interface.md)).

## Train and evaluate

```bash
# Check the Gymnasium environment
uv run python scripts/train.py --check

# Short training job
uv run python scripts/train.py --timesteps 5000 --n-envs 2

# Against the native full-run wrapper
uv run python scripts/train.py --run-env --timesteps 5000 --n-envs 2

# Longer run, saved as a Stable Baselines3 checkpoint
uv run python scripts/train.py --timesteps 1000000 --n-envs 4 --save-path checkpoints/maskable_ppo
```

`scripts/train.py` trains `MaskablePPO` from `sb3-contrib` using action masks from the
environment.

```bash
# Baseline policy over fixed seeds, with per-encounter win rates
uv run python scripts/evaluate.py --episodes 100 --policy first-valid

# Native full-run episodes
uv run python scripts/evaluate.py --run-env --episodes 10 --policy first-valid --max-episode-steps 200

# Force an encounter, starter-deck-aware baseline
uv run python scripts/evaluate.py --episodes 100 --policy starter-aggressive --encounter chompers
```

## Differential testing against the live game

These need Slay the Spire 2 running with the [STS2MCP](https://github.com/evangambit/STS2MCP)
mod, launched **through Steam** rather than by starting the executable directly:

```bash
open "steam://rungameid/2868840"
curl -s http://localhost:15526/ >/dev/null && echo "STS2MCP is up"
```

```bash
# Deterministic emulator trace, for comparison against a real-game trace
uv run python scripts/trace.py --seed 0 --encounter toadpoles --actions 0 1 2

# The same trace from the running game
uv run python scripts/trace_real_game.py --actions 0 1 2

# Compare two traces on their normalized player/enemy fields
uv run python scripts/compare_traces.py emulator-trace.json real-game-trace.json

# Start a seeded run in the game before tracing
uv run python scripts/start_real_game_run.py VALIDATION1 --character IRONCLAD --abandon-existing

# Capture a full run
uv run python scripts/trace_real_game_run.py FULLRUN_SEED --abandon-existing --output traces/full-run/FULLRUN_SEED.json

# The repeatable validation sweep
uv run python scripts/validate_real_game_sweep.py --suite all --continue-on-failure
```

`--suite direct` runs one-passive-turn direct encounter checks, `--suite passive-boss` the
three-turn boss checks, and `--encounter aeonglass` narrows to one fight.

Generated full-run captures are gitignored: they are large and usually superseded quickly.
Prefer fresh STS2MCP replay-derived traces for parity work, and do not keep obsolete traces
after the emulator has moved past them. Per-card fixtures are the exception — those are
small, committed, and are the regression suite.

## Updating generated game data

When the game patches:

- `scripts/decompile.sh`: decompile the game assembly.
- `scripts/extract_data.py`: regenerate the C# data tables from decompiled sources.
- `scripts/diff_patch.py`: summarize what changed in the generated data.
- `scripts/patch_update.sh`: run the whole pipeline (decompile → extract → diff → build → test).

A patch also invalidates readings: `audit_cards.py` and `audit_relics.py` key their notes to
a digest of the source that was read, so anything the patch touched reports as stale rather
than silently staying "verified".

See [PLAN.md](PLAN.md) for the active parity gaps and the next implementation steps.
