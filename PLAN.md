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
   playthroughs* below for how, and **[docs/replay-verification.md](docs/replay-verification.md)**
   for the full-run corpus + replay-harness design (the primary fidelity signal, and
   the honest answer to "how do we know a character is correct").
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

### STS2MCP mod wired up (2026-08-15)

The oracle/observability mod is live on the local game.

- **Built from source**, not downloaded (official binary v0.4.0 targets game v0.99.1;
  source tested v0.103.2; local game is v0.107.1). Cloned `Gennadiyev/STS2MCP` to
  `../STS2MCP` (outside the fork repo) and built against the local v0.107.1
  assemblies: `dotnet build STS2_MCP.csproj -c Release -o out/STS2_MCP
  -p:STS2GameDir="<game root>"` → **0 warnings, 0 errors** (the mod's game-API
  surface still exists in v0.107.1). The csproj already has macOS `.app`-bundle
  reference paths built in.
- **Installed** as a subfolder mod (the loader's format, matching MovesViewer et al.):
  `…/SlayTheSpire2.app/Contents/MacOS/mods/STS2_MCP/{STS2_MCP.dll, mod_manifest.json}`.
  (The README's "flat dll+json in mods/" is an older loader convention — don't use it.)
- **Verified live**: launched via `open steam://rungameid/2868840`; API up ~15 s
  later. `GET /` → `Hello from STS2 MCP v0.4.0`; `GET /api/v1/singleplayer` → live
  state (`state_type: menu`); `GET /api/v1/compendium` → profile/card data. Base URL
  `http://localhost:15526`; the emulator scripts use `/api/v1/singleplayer`
  (GET state / POST action) and `/api/v1/compendium`.
- **Drove a full run via the API** (2026-08-15): main menu → `singleplayer` →
  `standard` → `character_select` → Ironclad → embark → Neow → map → into a Monster
  combat (two Corpse Slugs, 27/29 HP; player 64/80). Menu nav, embark, event/map/combat
  actions, and live state reads all work — the observability + control loop is proven.

**v0.107.1 script-adaptation findings (for the differential-test phase):**

- **Standard mode blocks custom seeds.** `menu_select confirm` with a `seed` fails:
  `Seed should not be changed in standard mode!`. Embark *without* a seed instead.
- **Seed not exposed via the API** at early floors — `compendium.current_run` has no
  `seed` ("save not found yet"), and no `seed` field appears in singleplayer state.
  So the seeded-run scripts (`start_real_game_run.py --seed`, and
  `trace_real_game_run.py` via `start_seeded_run`) don't work as-is on v0.107.1.
  Need either a seeded/custom mode that exposes the seed, or a post-hoc seed read.
- **Neow flow has an extra step.** Choosing a blessing then presents a separate
  `Proceed` event option; `enter_first_combat`'s timing raced past it and timed out.
  Manual `choose_event_option` sequencing works — the script needs a wait/retry tweak.
- **Combat state lives under the `battle` key** (`round`, `turn`, `is_play_phase`,
  `enemies`), not `combat` — confirm the trace scripts read the right key on this version.
- Replay-based capture (`trace_real_game_run.py`) additionally needs Zamiell's
  `start_replay` fork of STS2MCP.

**Net:** the mod and API are fully functional on v0.107.1; the *automation scripts*
need modest adaptation (seed handling + Neow/event timing + `battle` key) before the
seeded differential-test loop runs unattended.

### Live end-to-end harness test (2026-08-15)

Rebuilt/installed the mod (now with `return_to_main_menu`) and ran the harness against
the live game:

- **Verified working**: `return_to_main_menu` (abandon flow), `debug_start_encounter
  CorpseSlugsWeak` (lands in combat, 2 slugs, reaches play phase), and
  `validate_real_game_trace.find_matching_seed` runs end-to-end. All the v0.107.1
  plumbing works.
- **New divergence surfaced — DIAGNOSED (real emulator bug, not a comparison issue).**
  See the diagnosis below.

### Enemy-HP-roll diagnosis (2026-08-15)

Empirical (4 seedless embarks + `debug_start_encounter CorpseSlugsWeak`): slug HP came
out **[27,28], [27,29], [29,28]** — 28s appear, and each pair is two *distinct* values.
Also: **seedless standard runs use a random seed each time** (`R4NJ30ZGS8`, `NVUU5SJD9A`,
…), which is why the earlier `find_matching_seed` failed (it was hunting a random seed).

Root cause (game `CombatState.CreateCreature` → `Creature.SetUniqueMonsterHpValue(
creaturesOnSide, RunState.Rng.Niche)`): each enemy, in creation order, rolls a **unique**
HP from `[MinInitialHp, MaxInitialHp]` minus HP already taken by prior enemies, via
`rng.NextItem(remaining)` on the **Niche** stream.

The emulator **already has a correct port** of this in `CombatFactory.CreateEnemy`'s
`else if (_currentNicheHpRng != null)` branch (`Enumerable.Range` → `ExceptWith(
_usedNicheHps)` → `Next(0,count)`/`ElementAt` → add to used). **The bug: `CreateCorpseSlug`
opts out with `fixedHp: 27/29`,** which takes a broken branch — uses the fixed value
(never 28) *and* consumes the wrong RNG (`Next(0,3)` instead of `NextItem` with
`ExceptWith`), desyncing the Niche stream for the next slug too.

**This is NOT an ordering/comparison issue** — order-insensitive matching would mask it
and still couldn't produce a 28.

**FIX APPLIED (commit `123fecf`):** dropped `fixedHp` from all ranged enemies
(CorpseSlug, Tracker/Assassin/Brute RubyRaider, FossilStalker) so they take the roll
path; and gave the direct combat env (`NativeCombat`, which set `NicheHpRng=null` — the
reason `fixedHp` existed) a **seed-derived Niche stream** (`GameRng(seed,"niche")`) so
the unique-HP roll applies there too. Verified: corpse-slugs (weak) now yields unique
HP across {27,28,29}, all orderings, no duplicates — matching the live game. 198 C# +
17 Python tests pass.

### Custom-run seeded embark — SOLVED (2026-08-15)

Built full custom-run screen support into the STS2MCP fork (`McpMod.CustomRun.cs` +
routing in `McpMod.Actions.cs` + state reporting in `McpMod.StateBuilder.cs`):
`NCustomRunScreen` now reports as `menu_screen: "character_select"` (`custom_run: true`,
with characters / `seed_input` / `ascension`), and `menu_select` drives it — select
character, `confirm` with a seed (via `Lobby.SetSeed`, which custom mode *accepts*),
set ascension. **Verified live:** `singleplayer → custom → IRONCLAD → confirm(seed=
"ABCDEF")` embarks with `current_run.seed == "ABCDEF"` (gen seed 3334281563).

**Exact-match feasibility PROVEN:** new test `RunRngSet_DerivesGameSeedForStringSeed`
asserts `RunRngSet("ABCDEF").Seed == 3334281563u` — the emulator's string→seed
derivation matches the live game for a non-trivial seed (199 C# tests pass). So with
(a) faithful enemy-HP rolls, (b) arbitrary seed control in the game, and (c) matched
seed derivation, **exact seed-for-seed differential verification is unblocked.**

### First exact-match confirmed (2026-08-15)

Ran the live A/B: custom-embark seed **`ABCDEF`** → `debug_start_encounter
CorpseSlugsWeak` → live combat = enemies **[28, 29]**, hand [Bash, Defend, Defend,
Strike, Strike]. Emulator `Sts2CombatEnv(seed=3334281563, encounter="corpse-slugs",
completed_combat_rooms=0)` (3334281563 = the derived gen seed, and the combat env's
`NicheHpRng = GameRng(3334281563,"niche")` = the run's Niche stream) → enemies **[28,
29]**. **ENEMY HP EXACT MATCH** — same values, same order, from a real seed. This
proves the whole chain (seed derivation + Niche stream + HP-roll fix) is bit-exact.

**Opening hand — Shuffle stream now wired (commit `6e53ee2`), exact match still pending.**
Gave the combat env `ShuffleRng = GameRng(seed,"shuffle")` and shuffle with it
(`ShufflePile` is the same Fisher-Yates as `GameRng.Shuffle`). The hand now uses the
Shuffle stream but doesn't match live yet. Deeper diagnosis (2026-08-15):
- **Deck order matches — ruled out.** `AscensionManager` adds Ascender's Bane via
  `Deck.AddInternal(bane, -1)` which *appends* (`CardPile.AddInternal` index -1 → `Add`),
  so the game's run deck is `[5 Strike, 4 Defend, Bash, Bane]` — identical to the
  emulator's `StarterDeckIds`. Algorithm matches (both Fisher-Yates); stream derivation
  matches (Niche proved the mechanism).
- **Emulator gap found: no turn-1 draw-pile reorder.** The game's turn-1 setup
  (`CombatManager` ~658): shuffle → move `ShouldStartAtBottomOfDrawPile` cards
  (Ascender's Bane) to the **bottom** → move `Innate` cards to the **top** → draw. The
  emulator's `CombatFactory` does **neither reorder** — a real fidelity bug (corrupts
  the opening hand whenever Bane or an innate card would be in the top 5). **Fix:** port
  the turn-1 bottom/top reorder before the opening draw.
- **Residual shuffle-state factor.** Even accounting for the reorder, this hand's
  composition still differs (emu 3 Defend/1 Strike vs live 2 Defend/2 Strike with Bane in
  neither top 5), pointing to the Shuffle stream's call-count / a subtle shuffle
  difference. Needs full-deck introspection on both sides (the combat obs summary doesn't
  expose ordered draw-pile) to pin down.
Enemy HP already matches exactly; these two close the opening-hand gap for full combat
exact-match.

**Robustness note:** rapid abandon→re-embark→`debug_start_encounter` cycling crashes
the game (error popup); a single clean sequence with generous waits is stable. Harden
the debug/menu actions with settled-state guards before running unattended sweeps.

### Seed alignment — solved, with RNG parity already validated (2026-08-15)

Question: can a real run be aligned to an emulator trace via a seed? **Yes.**

- **Two ways to control the seed.** (1) Custom mode: the mod exposes
  `menu_select "custom"` (`_customButton`) whose embark path calls
  `Lobby.SetSeed(seed)` — standard mode rejects this (`Seed should not be changed in
  standard mode!`), custom accepts a chosen seed. (2) Read-back: after the save
  writes, the seed is in `compendium.current_run` and the save file.
- **Careful — the API's reported `seed` is the input field, not the generator.** The
  save has `/rng/seed = "0"` (custom-input, 0 for a default standard run) *and*
  `/players[0]/rng/seed = 3452614542` (the derived generation seed). The API surfaces
  the former. Our API-embarked "standard" run actually ran with input seed **`"0"`**.
- **RNG parity for seed `"0"` is already validated in-emulator and passing.**
  `RunEngineTests.RunRngSet_MatchesPythonPinnedNamedStreams`: `new RunRngSet("0")` →
  `Seed == 3452614542u` (exactly the real save's `players[0].rng.seed`) plus pinned
  values for every named stream (UpFront, Shuffle, CombatCardGeneration, MonsterAi,
  …). This test is among the 198 that pass — so the emulator reproduces the real
  game's RNG streams for this seed. **The hardest parity layer is already done.**
- **Enemy-stat parity checks out.** Real first combat = two Corpse Slugs at 27 & 29
  HP; emulator `EnemyDef(CorpseSlug, MinHp: 27, MaxHp: 29)`. Matches.
- **Encounter-identity quick-compare diverged — but the comparison was confounded,
  not a proven bug.** Emulator seed-`"0"` first combat via *auto-navigation* was
  `fuzzy-wurm-crawler` (id 8) vs the real run's `corpse-slugs` (id 9). Two
  uncontrolled variables: (a) different map path (real: chose node 0; emulator:
  first-valid-action) — StS2 assigns encounters per node, so path matters; (b)
  ascension (real A8 vs emulator default ~A0). A real differential test must match
  **(seed, ascension, action sequence)** — this quick check matched none but seed.

**Takeaway:** alignment is mechanically solved and the RNG foundation is proven. The
remaining work to run a rigorous live differential test is (1) adapt the driver
scripts to v0.107.1 (seedless-embark + read-back or custom-mode set-seed; Neow
timing; `battle` key), (2) pin ascension on both sides, (3) replay the *same* action
sequence in the emulator and diff — the intended `trace_real_game` → `compare_traces`
loop.

### The automated harness needs unpublished mod actions (2026-08-15)

Investigated adapting the driver scripts and hit a hard architectural blocker:

- `validate_real_game_trace.py` (the differential harness) drives combats via mod
  actions **`debug_start_encounter`** and **`debug_force_play_phase`** — it jumps the
  real game straight into a chosen encounter, then seed-searches for a match.
- **These actions exist in no public mod.** Not in upstream `Gennadiyev/STS2MCP`,
  and not in `Zamiell/STS2MCP` (public fork, last commit 2026-05-13). Per the
  emulator's `AGENTS.md`, Zamiell runs a *local* `D:\Repositories\STS2MCP` with added
  APIs (`start_replay`, and evidently these debug actions). The harness was written
  against that unpublished build.
- So "adapt the scripts" is not sufficient — the harness depends on **mod
  capabilities that aren't published**. The public mod gives live state + manual
  navigation + `SetSeed` (custom mode), but not force-encounter setup.

**Concrete direct comparison done anyway** (live seed-0 run's natural Corpse-Slug
combat vs emulator `Sts2CombatEnv(seed=0, encounter="corpse-slugs")`): **enemies and
the full 11-card deck match exactly**; the **5-card opening draw differs** — expected,
because a *direct* combat setup has a different shuffle-RNG context than a *natural
in-run* combat, and the live run also carries a Neow **Kaleidoscope** relic. This is
precisely the artifact `debug_start_encounter` exists to eliminate.

**Paths to a rigorous automated differential test (decision needed):**
1. **Implement `debug_start_encounter` + `debug_force_play_phase` in the public mod
   ourselves.** We have the decompiled game combat-init code to reference. Unlocks
   the existing harness directly. Focused mod-dev task; the highest-leverage option.
2. **Build a bespoke navigation-based test** on the public API: custom-mode set-seed
   → drive to a natural combat → capture (`battle`) → compare to an emulator *run*
   (not direct) trace at the same seed/actions. Avoids mod-dev but is more
   navigation-fragile and needs the v0.107.1 script fixes.
3. **Spot-check manually** for now (as done above) and defer automation.

### Chose path 1 — debug actions implemented in the mod (2026-08-15)

Reconstructed the two missing debug actions from the decompiled game APIs and added
them to our STS2MCP build (`../STS2MCP/McpMod.Debug.cs`, wired into the action switch
in `McpMod.Actions.cs`):

- **`debug_start_encounter`** — looks up the `EncounterModel` by type name from
  `ModelDb.AllEncounters`, builds `new CombatRoom(encounter.ToMutable(), runState)`,
  and enters it via `RunManager.Instance.EnterRoom(room)` (fire-and-forget; caller
  polls). Reconstructed from `CombatRoom`/`RunManager`/`CombatManager` decompiled
  source.
- **`debug_force_play_phase`** — reports readiness using the mod's `IsPlayPhase(player)`
  helper (note: v0.107 moved play-phase onto `player.PlayerCombatState.Phase`, no
  longer `CombatManager.IsPlayPhase`). The play phase transition happens naturally a
  beat after entry, so the caller's poll suffices.

Builds clean against v0.107.1; installed; **verified live**: from a seedless-embarked
run at Neow, `debug_start_encounter CorpseSlugsWeak` lands directly in that combat —
correct enemies, reaches play phase (`is_play_phase: true`), proper Ironclad opening
(5-hand / 6-draw / 3 energy), and — because it jumps from the Neow *event* — **no Neow
relic** (clean Burning-Blood-only state).

**First controlled comparison surfaced a discrepancy — now fully resolved:** live
`CorpseSlugsWeak` = **2** slugs (27, 29) vs emulator `corpse-slugs` = **3** (27, 28,
29). Root cause traced (not a model bug):

- The game has `CorpseSlugsWeak` (2 slugs) and `CorpseSlugsNormal` (3 slugs). The
  emulator models both as one encounter: `CreateCorpseSlugsEncounter(rng, weak)`,
  `weak = completedCombatRoomsBeforeCurrent is >= 0 and < 3` (early combat → weak).
- `Sts2CombatEnv` → `Sts2_ResetEncounter` → `CombatFactory.Reset` defaults
  `completedCombatRoomsBeforeCurrent = -1`, which fails `>= 0` → **always non-weak
  (3-slug)**. The live combat was the run's first (0 prior) → weak (2-slug). So the
  comparison was normal-vs-weak, my setup error.
- **Real harness limitation exposed:** `validate_real_game_trace.emulator_initial_summary`
  uses that same direct `Sts2CombatEnv`, so it can't produce weak variants — yet it
  maps many encounters to `*Weak` live combats (`corpse-slugs → CorpseSlugsWeak`,
  `toadpoles → ToadpolesWeak`, …). It would mismatch enemy counts on every weak-mapped
  first-floor encounter.
- (Opening hands also differ, expected: a *direct* emulator combat vs an *in-run* debug
  combat consume RNG at different offsets.)

**Fix IMPLEMENTED (2026-08-15):** threaded the weak-combat context through the sim so
the direct combat env can produce weak variants.
- C#: new `CombatFactory.Reset(state, rng, deckIds, encounterId,
  completedCombatRoomsBeforeCurrent)` overload → new `NativeCombat.Reset(deckIds,
  encounterId, completedCombatRooms)` → **new native export `Sts2_ResetEncounterWeak`**
  (kept `Sts2_ResetEncounter` for ABI stability). Bumped `NATIVE_API_VERSION` 10→11.
- Python: `native.reset_encounter(..., completed_combat_rooms=-1)` dispatches to the new
  export when set; `_REQUIRED_NATIVE_API_VERSION` 11; `Sts2CombatEnv(..., completed_combat_rooms=-1)`
  + a `completed_combat_rooms` reset-option.
- Verified: `Sts2CombatEnv(seed=0, encounter="corpse-slugs", completed_combat_rooms=0)`
  → **2 slugs at 27/29, exactly matching the live `CorpseSlugsWeak`**; default (-1)
  still 3 slugs; **198 C# + 17 Python tests still green**.
- Remaining (harness wiring): have `emulator_initial_summary` pass
  `completed_combat_rooms=0` when the live-mapped encounter is a `*Weak` variant, so the
  seed-search compares like-for-like on first-floor combats.

**Remaining to run the harness unattended:** `start_seeded_run` still uses standard-mode
seeded embark (blocked on v0.107.1) — switch it to custom-mode `SetSeed` or seedless
+ read-back; then the seed-search comparison in `validate_real_game_trace.py` can run.

**Mod home:** the debug actions are committed to **`github.com/evangambit/STS2MCP`**
(a fork of `Gennadiyev/STS2MCP`, our commit on top of upstream's v0.107 compat fix).
Local checkout at `../STS2MCP` has `origin`=fork, `upstream`=Gennadiyev; rebuild with
`dotnet build STS2_MCP.csproj -c Release -o out/STS2_MCP -p:STS2GameDir="<game dir>"`
and copy `out/STS2_MCP/STS2_MCP.dll` + `mod_manifest.json` into the game's
`mods/STS2_MCP/`.

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

1. Drive a real run through the STS2MCP API (`start_real_game_run.py`), adapting to
   v0.107.1's menu shape if needed; then `trace_real_game.py` → `compare_traces.py`
   against a deterministic emulator trace — the first live differential test.
2. Re-validate the 6 value-drift tests against fresh v0.107.1 traces, then commit the
   move to current-patch data (or stay pinned to baseline for now).
3. Begin the AlphaZero layer: MCTS over the sim (C#), then the value/policy net
   (Python) + batched inference bridge, then self-play.
