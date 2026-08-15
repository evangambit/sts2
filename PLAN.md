# Slay the Spire 2 AI — Design Plan

An AlphaZero-style agent for Slay the Spire 2: a fast headless simulator, MCTS
tree search, and a neural network providing value/policy guidance. Bespoke.

---

## 1. Core strategic decision: you must reimplement the simulator

You cannot get full simulation "for free" (driving the real game, with automatic
updates on new patches). The reason is structural, not incidental. The search
loop needs a simulator that is:

- **Forkable / clonable** — MCTS clones the game state at every node and rolls it
  back. The real game holds one mutable world; it is not built to snapshot and
  restore millions of times.
- **Headless and fast** — AlphaZero-style self-play is bottlenecked on simulator
  throughput. Target roughly **10⁵–10⁶ state transitions/sec/core**. The real
  game, even modded with rendering stripped, is 3–5+ orders of magnitude too slow
  and runs a single instance.

Therefore the real game can serve as a **data source and correctness oracle**, but
not as the simulator you search over.

### Consequences

- **No free auto-update.** Every balance patch, new card, relic, and character is
  manual work. This is the standing maintenance tax and is why the existing
  emulator (Zamiell) covers only one character — content coverage is the grind.
  *Mitigated but not eliminated by tooling:* because the game is decompilable C#
  (§4), the logic can be **read directly and ported**, not guessed, and data can be
  extracted semi-automatically each patch — so the tax is real but much lower than
  reverse-engineering a black box.
- **Split data from logic.** Card numbers, relic stats, enemy movesets, and map
  parameters are *data* that can be extracted from `sts2.pck` / decompiled models
  and re-imported semi-automatically each patch. The *interaction logic* (how
  effects combine) is ported from decompiled C# and is where bugs and update-churn
  live. Design the simulator along this seam from day one.

---

## 2. Language stack

> **Revised after research (see §4).** StS2's game logic is **C#** (Godot 4), the
> decompiled source is C#, and the leading open-source emulator (Zamiell) is
> already a **C# NativeAOT core + Python** RL sim. This flips the earlier Rust
> recommendation: matching C# lets you **port decompiled game logic directly**
> instead of re-deriving it in another language — the single biggest correctness
> lever in the whole project.

| Component | Language | Rationale |
|---|---|---|
| Simulator + MCTS + self-play orchestration | **C#** (.NET, NativeAOT) | Matches the game and the reference emulator. Lets you copy/adapt method bodies straight from decompiled `src/Core/` instead of hand-translating them — massively fewer fidelity bugs. NativeAOT gives good, GC-manageable performance; use structs + pooling for cheap state clones. |
| NN definition + training | **Python + PyTorch** | Standard; nothing pushes off it. The emulator already exposes a Gymnasium interface to Python. |
| Bridge (sim ↔ Python) | NativeAOT shared lib via `ctypes` | Exactly how the Zamiell emulator already bridges C# sim → Python. Proven. |
| Bridge (inference in search) | serialized model boundary | Keep sim↔model a stable format (ONNX, or batched requests to a GPU server), not a tight FFI you rebuild constantly. |

**Why not Rust anymore.** Rust is faster raw and GC-free, but the game logic you
must replicate is C#. Porting decompiled C# into Rust multiplies the bug surface
in exactly the component that most needs to match the original bit-for-bit. That
correctness cost now outweighs Rust's speed edge. **Reconsider Rust only if
profiling later proves C# self-play throughput is the bottleneck — and even then,
port only the hot path, not the whole sim.**

### The inference bridge

Inference must be fast *inside* search. Options, roughly in order of throughput:

1. **Batched inference server (preferred).** Search workers send leaf states to a
   GPU process that batches evals across many parallel MCTS trees. Batching keeps
   the GPU busy and is what scales.
2. **Export to ONNX, run in-process** via ONNX Runtime (`Microsoft.ML.OnnxRuntime`
   for C#). Good for self-contained deployment/eval.

Plan: train in Python, serve batched, search in C#.

---

## 3. Correctness: how to get to high fidelity

You cannot *guarantee* zero bugs, but the path to high fidelity is well-precedented
— and largely **already built** for StS2: the Zamiell emulator ships
differential-testing infra (trace-JSON comparison, validation against the running
game via the **STS2MCP** mod), and effects there are "trace-observed" from
decompiled code. Reuse that harness rather than rebuilding it. Note too that
porting from decompiled C# (§4) means much logic is *read*, not inferred, so
differential testing becomes a **cross-check on the port** rather than the primary
means of discovering behavior.

1. **Reproduce the game's RNG exactly** — same algorithm, same seeding, same
   *order* draws are consumed (card draws, map gen, combat rolls). If RNG matches,
   a seed + action sequence reproduces an identical playthrough, which unlocks
   everything below. This is the highest-leverage early task. If RNG does not
   match, you can only diff deterministic transitions.
2. **Oracle logging from the real game** — a mod (or the Zamiell emulator as a
   secondary oracle) that logs full state before/after every action. Replay the
   same seed + actions in your sim and assert identical resulting states. Gold
   standard and regression corpus.
3. **Large golden-replay suite** — capture playthroughs, freeze them, run every
   commit. New content = new goldens. Catches update-regressions. See *Capturing
   playthroughs* below for how.
4. **Property / invariant tests + fuzzing** — energy conservation, deck-count
   conservation, HP bounds, no illegal states — under randomized action fuzzing.
   Catches interaction bugs that per-card unit tests miss.
5. **Per-card / relic / enemy unit tests** for the specific numbers.

**Honest framing:** for *training*, minor divergences are often tolerable as long
as they do not open unrealistic exploits the agent learns to farm. For *actual
play fidelity* you want tight matching. Differential testing with matched RNG plus
a growing golden corpus is the proven route.

### Capturing playthroughs

The game does not persist a per-state, per-action log. You do not *save* states —
you *instrument* the game to emit them, and thanks to bit-exact RNG you need far
less than "every state."

**What a golden replay needs:**

- **The seed** — the whole run is a function of it.
- **The action sequence** — cards played + targets + order in combat, path taken,
  rewards chosen.
- **Ground-truth checkpoints** to compare against — *not* every state.

Given seed + actions, the simulator regenerates every intermediate state itself.

**The RNG trick makes capture cheap.** Once RNG is bit-exact, a golden replay is
just `(seed, action_sequence)`. The sim produces all intermediate states
deterministically; to validate, the real game only needs to emit a **compact state
hash per turn (or per action)**, not full state:

- Hashes match throughout → replay passes, near-zero storage.
- A hash diverges → the *first* mismatched turn pinpoints the exact bad transition.
- Full-state dumps only when a hash diverges and you want to debug it (re-run with
  verbose logging at that point).

**How to emit states from the game (best → worst):**

1. **A logging / "communication" mod.** If StS2 is moddable, a mod hooks the game
   loop and emits full state as JSON plus the action stream. Precedent: StS1's
   **CommunicationMod** exposed complete game state as JSON over stdio specifically
   so bots could read state and send actions — exactly this capability. Feasibility
   is the open moddability question (§4).
2. **The game's own run-history / metrics save files** — usually free but *coarse*
   (map-level: path, card rewards, relics; not per-card combat actions). Good for
   map/economy-layer goldens, insufficient for combat.
3. **Memory reading** (external process reconstructs state from RAM) — fragile,
   breaks on updates; fallback only.
4. **Video / manual transcription** — impractical; ignore.

**You do not need humans to generate playthroughs:**

- **Scripted / random / fuzzing agents driving the modded game** produce traces
  automatically and cover bizarre card+relic interactions better than human play.
- **Bootstrap from your own bot.** Once even a weak MCTS bot exists, run its games
  through the real game to validate — a self-growing corpus focused on states the
  agent actually visits.
- Seed with a few curated human runs for realism; bulk it out with automated
  coverage.

**Net:** capture = instrument (mod) the game to emit `(seed, actions, per-turn
hash)`, generated mostly by automated agents, validated against the sim via hash
comparison, with full-state dumps only on divergence. Hinges on StS2 being
moddable enough for a CommunicationMod-style hook (§4).

---

## 4. Engine, moddability & existing ecosystem (researched)

**StS2 is C# on Godot 4, and both moddable and already heavily reverse-engineered.**
Game logic ships in `sts2.dll` and decompiles cleanly with **ILSpy / ilspycmd**
into ~3,300 readable C# files (`src/Core/`, with cards/potions/relics in
`src/Core/Models/`). Assets live in `sts2.pck` (~15,000 files) and extract with
**GDRE Tools** (Godot RE Tools). Mods are C# (.NET 9.0) using **Harmony** patches
for runtime hooking, loaded via **GUMM** (Godot Universal Mod Manager) with a
`manifest.json`; Steam Workshop is supported natively. This is a far richer,
more tractable situation than the StS1 (Java) lineage — the exact game logic is
readable, not inferred.

### Existing projects to build on (don't reinvent)

- **Zamiell emulator** — `github.com/Zamiell/slay-the-spire-2-emulator`. Already
  the architecture this plan describes: high-performance headless sim, **C#
  NativeAOT core loaded from Python via `ctypes`**, **Gymnasium** RL interface,
  **seeded per-instance RNG** for deterministic resets/rollouts, card effects /
  enemy AI / buffs / draw-discard / damage / **RNG streams** / map routing, effects
  "trace-observed" from decompiled code, plus **differential-testing infra**
  (compare trace JSONs; validate vs the running game via STS2MCP). **Verified
  behavior: Ironclad only** — but see the source evaluation below; the architecture
  is *not* single-character-shaped. → **Decision: fork it** (see §4a).
- **STS2MCP** — `github.com/Gennadiyev/STS2MCP`. A mod that exposes full in-game
  state as JSON over a localhost REST API (`127.0.0.1:15526`) plus an MCP server.
  Covers all screens (combat, rewards, map DAG w/ lookahead, shop, events, card
  selection, relics). **This is the observability / oracle layer already built** —
  use it for differential testing instead of writing our own mod.
- **sts2-modding-mcp** (elliotttate) — 151+ tools: decompiles, indexes ~3,048
  entities + 144 hooks, call graphs, hook recommendation, code generation, live
  scene inspection. Useful for **semi-automated data extraction** each patch.
- **ModSmith** (`cpimhoff.github.io/Sts2-ModSmith`) and community modding tutorials
  — decompile/build toolchain references.

---

## 4a. Zamiell emulator: source evaluation & fork decision

Read the actual source (`src/Sts2Emulator/`) to decide fork vs. rebuild. **Verdict:
fork it.** The "single character" concern is about *verified content depth*, not
architecture — the engine is already multi-character and structured so content is
an additive grind, not a wall.

**Facts.** MIT license. ~143 commits, last ~June 4 2026 (≈10 weeks stale as of this
writing). 6 stars, 0 forks — a one-person WIP, built Zamiell + Claude. Three-layer:
C# NativeAOT core (`Core/`) ↔ Python `ctypes`/Gym (`Interop/`) ↔ generated data
(`Generated/`). Has xUnit tests + Python differential validation.

**What the source shows:**

- **Generic, multi-character data model.** `Generated/Cards.g.cs` defines **~546
  cards across all five characters** (Ironclad, Silent, Defect, Necrobinder,
  Regent) + Colorless. `CardDef` is a clean generic template (Id, Name, Cost,
  BaseDamage, BaseBlock, Upgrade…, Type, Rarity); runtime state split into
  `CardInstance`. No Ironclad assumptions in the schema. All **generated** from the
  decompiled assembly via `decompile.sh → extract_data.py → diff_patch.py` (a real
  per-patch update pipeline).
- **Behavior = one big switch on card ID** in `Core/Effects/CardEffects.cs` (184KB).
  Each card is an isolated case (`case IC.Bludgeon: DealDamage(state, Dmg(def,
  upgraded)); break;`). Adding/fixing a card is **additive and isolated — zero
  impact on existing cards** (and parallelizable, incl. AI-assisted).
- **Graceful fallback.** `ApplyGeneratedCardApproximation()` runs any card lacking a
  hand-written case straight from its generated data. So all 546 cards *already run*
  approximately; you promote approximate → exact **one card at a time, gated by
  differential tests.** Coverage is a smooth gradient, never a blocking wall.

**Residual risks (execution, not architecture):**

1. The switch file gets large at full scale — inelegant but mergeable; refactor later.
2. Non-Ironclad fidelity is **unproven** — broad data, narrow *verified* behavior;
   the approximate→exact loop is only battle-tested on Ironclad.
3. Bus-factor-1 WIP in a churny state (fixtures recently deleted, progress
   self-described as blocked on new traces). Forking means owning it.

**De-risk before committing:** run `decompile.sh` + `extract_data.py` against
**today's** game build and confirm it regenerates current data cleanly. That turns
the patch-currency story from promised to proven.

### Local environment (confirmed 2026-08-14)

Game installed at `~/Library/Application Support/Steam/steamapps/common/Slay the
Spire 2/SlayTheSpire2.app`. All preconditions met:

- **Version `v0.107.1`** (commit `59260271`, 2026-06-18) — per
  `Contents/Resources/release_info.json`.
- **`sts2.dll`** (`.../Resources/data_sts2_macos_arm64/`, 8.9 MB) is a **managed
  .NET/Mono assembly** → decompiles directly with ILSpy/ilspycmd. Game runs on
  CoreCLR (`libcoreclr.dylib`), not NativeAOT — clean to decompile.
- **`Slay the Spire 2.pck`** (`.../Resources/`, 1.8 GB) → GDRE Tools target.
- **Modding already works** — `Contents/MacOS/mods/` has ModConfig, MovesViewer,
  PathTheSpire2, UnifiedSavePaths installed; `0Harmony.dll` bundled. STS2MCP is
  drop-in.

Note: the emulator's last commit (~2026-06-04) **predates** this installed build
(2026-06-18), and a newer public patch (v0.110.x) may exist — so regenerating data
against the local `sts2.dll` is expected first-step work, not optional.

### RNG (from decompiled source, not observation)

Read the exact RNG implementation from decompiled C# in `sts2.dll` — do not
reverse-engineer from observation or assume Godot PCG32. The emulator already
reproduces the game's RNG streams and differential-validates them; confirm seeding
and the *order* draws are consumed. (Note `Core/CountingRandom.cs` + `Core/Rng/`.)

---

## 5. Suggested build order

Research (§4) and the source evaluation (§4a) changed the shape: much of the
foundation already exists and the fork decision is made, so the first move is
**adopt and prove-out**, not build from scratch.

0. **Fork the Zamiell emulator** (decision per §4a). Immediately **prove the update
   story**: run `decompile.sh` + `extract_data.py` against the local game build
   (v0.107.1, see §4a) and confirm current data regenerates cleanly. Toolchain
   preconditions are already met locally — managed `sts2.dll`, readable `.pck`,
   working mod loader; just add ilspycmd + GDRE Tools and drop in STS2MCP.
1. **Stand up the harness end-to-end** on the existing Ironclad/Act-1 scope: sim ↔
   Python (`ctypes`/Gym) + differential testing vs the running game via STS2MCP.
   Confirm you can reproduce RNG streams. This validates the whole pipeline before
   you scale content. (Note the fork's fixtures were recently deleted — expect to
   capture fresh STS2MCP traces here.)
2. **Data extraction pipeline.** Confirm/extend the existing
   `extract_data.py`/`diff_patch.py` flow for card/relic/enemy/map data; make it a
   clean per-patch re-run.
3. **Extend content coverage** — promote cards from the `ApplyGeneratedCard-
   Approximation()` fallback to hand-ported exact logic, one at a time, gated by
   differential tests: remaining Act 1 → full run → other characters. The additive
   switch makes this parallelizable.
4. **MCTS over the simulator** (no NN yet — pure rollouts as a baseline).
5. **NN (value/policy) + training loop**; batched inference bridge.
6. **Self-play loop; iterate.**

The earlier "write your own observability mod + reverse-engineer RNG" ordering is
superseded: STS2MCP already provides observability, and the RNG is read from
decompiled source rather than reverse-engineered.

---

## 6. Progress log

### 2026-08-14 — Fork cloned, macOS pipeline proven end-to-end

Cloned the Zamiell emulator to `./emulator/` (HEAD `04cfe6d`, 2026-06-03, MIT).
Stood up the toolchain on macOS and **proved the patch-update story empirically**.

**Environment set up:**
- **.NET SDK 9.0.317** installed to `~/.dotnet` via Microsoft's `dotnet-install.sh`
  (no sudo; Homebrew's cask needs a sudo `.pkg` install and failed headless). SDK 9
  matches the `net9.0` target exactly.
- **ilspycmd 8.2.0.7535** (`dotnet tool install -g ilspycmd --version 8.2.0.7535`).
  The latest, 11.0.0.9375, has a broken package (missing `DotnetToolSettings.xml`) —
  pin 8.2.
- Per-shell env needed: `export DOTNET_ROOT="$HOME/.dotnet"; export
  PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"` (add to `~/.zshrc` to persist).

**Scripts adapted for macOS** (`emulator/scripts/`):
- `decompile.sh` — now OS-aware: finds `sts2.dll` under any `data_sts2_<platform>`
  folder via `find`, and uses `shasum -a 256` when `sha256sum` is absent.
- `build.sh` — default runtime auto-detects host (`osx-arm64` here) instead of
  hardcoded `win-x64`.

**Pipeline results (against local game v0.107.1):**
- `decompile.sh` → regenerated `decompiled/` from the local macOS `sts2.dll`.
- `extract_data.py` → **545 cards, 106 enemies, 242 powers, 296 relics, 63 potions**.
- `diff_patch.py` vs the repo's June-3 baseline → **all deltas numeric** (card
  costs/block/damage, enemy HP); script's own verdict: *"no manual effect
  implementation needed."* This is the patch-currency proof: updating = decompile →
  extract → diff, zero hand-coding for this patch.
- `dotnet test` → **198/198 pass on baseline data**; 6 fail only when run against the
  regenerated v0.107.1 data (value-pinned tests: Spite/Bloodletting HP math,
  DarkShackles cost 1→0, etc.). Confirms toolchain is clean and isolates the 6 as
  pure patch-drift. Baseline `Generated/` was restored, so the tree tests green.

**State of the working tree:** mac script fixes + regenerated `decompiled/` (current
build) are uncommitted; `Generated/` restored to baseline.

### macOS NativeAOT export wiring — RESOLVED (2026-08-14)

The blocker: `Sts2Emulator.csproj` exported its 33 `ctypes` entry points with
**Windows MSVC linker syntax** (`<LinkerArg Include="/EXPORT:short=mangled">`), which
the Apple/clang linker rejects. The exports themselves were bare
`[UnmanagedCallersOnly]` (no `EntryPoint`), so ILC emitted them under mangled names
and the `/EXPORT:` args aliased them — a Windows-only mechanism.

**Fix (strictly better, cross-platform):** switched all 33 attributes in
`Interop/NativeExports.cs` to `[UnmanagedCallersOnly(EntryPoint = "<MethodName>")]`
and **deleted** the entire `/EXPORT:` `ItemGroup`. ILC 9.0.317 honors `EntryPoint`
on macOS (the author's comment suspecting otherwise is outdated) — so the friendly
symbols export directly on every platform with **zero per-platform linker args**.

**Verified end-to-end on macOS:**
- `build.sh osx-arm64` → `out/Sts2Emulator.dylib`; `nm -gU` shows all **33 friendly
  symbols** exported (`_Sts2_Step`, `_Sts2Run_Create`, …).
- `native.py` already maps `darwin → Sts2Emulator.dylib`; no change needed.
- ctypes smoke test: dylib loads, API versions match (`Sts2_NativeApiVersion=10`,
  `Sts2Run_NativeApiVersion=8`), and a **real combat runs through the boundary**
  (obs_size 164, 6 actions, steps return rewards, e.g. +0.07). Python/RL boundary is
  live on macOS.

Caveat: the Windows build now also relies on `EntryPoint` (the standard mechanism)
instead of `/EXPORT:`. This is the canonical NativeAOT approach and should keep CI
green, but hasn't been re-verified on a Windows runner here.

### Python env + Gym boundary — DONE (2026-08-14)

- `uv 0.12.5` installed to `~/.local/bin`. Provisioned **CPython 3.12.14** (the
  system's 3.14.6 is too new for the pinned `torch==2.12.0`/`numpy==2.4.6` wheels).
- `uv venv --python 3.12` + `uv sync` → `.venv` with gymnasium 1.2.3, numpy 2.4.6,
  torch 2.12.0, stable-baselines3 2.8.0, sb3-contrib 2.8.0.
- **Full Python Gym suite green: 17/17** (`uv run python -m unittest discover -s
  tests/python`), driving the live `.dylib` end-to-end (gymnasium env → ctypes → C#).
- Gotcha: `native.py` mtime-based freshness guard fails if any `src/Sts2Emulator`
  source is touched after the dylib build — just re-run `bash scripts/build.sh
  osx-arm64`, or set `STS2_ALLOW_STALE_NATIVE=1` for intentional stale runs.

**macOS setup is now complete — both halves of the stack run locally:** C# build +
198 unit tests, the decompile/extract/diff patch pipeline, the NativeAOT dylib with
cross-platform exports, and the Python 3.12 RL env with 17/17 Gym tests.

### Environment quick-reference

```
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
# C#:     dotnet test src/Sts2Emulator.Tests/
# build:  bash scripts/build.sh osx-arm64        # → out/Sts2Emulator.dylib
# python: uv run python -m unittest discover -s tests/python
# patch:  bash scripts/patch_update.sh "<game dir>"   # decompile→extract→diff→build→test
```

### Next up

1. Re-validate the 6 value-drift tests against fresh v0.107.1 STS2MCP traces, then
   commit the move to current-patch data (or stay pinned to baseline for now).
2. Install the STS2MCP mod; capture traces; run the differential harness live against
   the real game.
3. Begin the AlphaZero layer: MCTS over the sim (C#), then the value/policy net
   (Python) + batched inference bridge, then self-play.
