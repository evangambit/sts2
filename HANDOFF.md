# Handoff — Slay the Spire 2 AI project

Orientation for a future agent. **PLAN.md** has the full design + chronological
history and the *why* behind every decision; **docs/replay-verification.md** has the
full-run verification design. This file is the *current state + how-to + gotchas +
next steps*. Read this first, then PLAN.md for depth.

## What this is

Building an AlphaZero-style agent for **Slay the Spire 2** (a fast headless simulator +
MCTS + a value/policy net). The simulator is a **fork of Zamiell's StS2 emulator**
(C# NativeAOT core + Python/Gymnasium). Current phase: making the emulator bit-exact
against the live game via differential testing, before building the AlphaZero layer.

The game is **StS2 v0.107.1**, C# on Godot 4, installed at:
`~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app`

## Repos & directories (under `~/Projects/STSS/`)

| Dir | Repo | What |
|---|---|---|
| `emulator/` | **github.com/evangambit/sts2** | The emulator fork (this repo). C# sim + Python gym + scripts + docs. |
| `STS2MCP/` | **github.com/evangambit/STS2MCP** | Our fork of Gennadiyev's STS2MCP mod (exposes game state/actions over HTTP). We added the custom actions below. `origin`=our fork, `upstream`=Gennadiyev. |
| `STS2MCP-zamiell/` | Zamiell/STS2MCP | Reference-only clone (checked for debug actions; **not used** — safe to delete). |

Both repos are clean and pushed as of this handoff. `decompiled/` in the emulator repo
is **gitignored** (MegaCrit's copyrighted code) — regenerate locally, see below.

## Environment (all installed, macOS arm64)

- **.NET SDK 9.0.317** at `~/.dotnet` (matches the `net9.0` target; installed via
  Microsoft's `dotnet-install.sh` — Homebrew's cask needs sudo we can't supply).
- **ilspycmd 8.2.0.7535** (global tool; latest 11.x has a broken package — pin 8.2).
- **uv 0.12.5** at `~/.local/bin`; **CPython 3.12.14** venv at `emulator/.venv`
  (system Python 3.14 is too new for the pinned torch/numpy).
- **Xcode CLT + clang** present (needed for NativeAOT native linking).

Put this in `~/.zshrc` (or prepend each shell) — nothing works without it:
```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
```

## How to build / test / run

```bash
cd ~/Projects/STSS/emulator

# C# unit tests (currently 209 pass)
dotnet test src/Sts2Emulator.Tests/

# Build the NativeAOT dylib the Python layer loads (→ out/Sts2Emulator.dylib)
bash scripts/build.sh osx-arm64

# Python gym tests (45 pass) — drives the live dylib via ctypes
uv run python -m unittest discover -s tests/python

# Regenerate game data / decompiled source for the current patch
bash scripts/decompile.sh "<game dir>"        # → decompiled/ (gitignored), needs ilspycmd
python scripts/extract_data.py                # → src/Sts2Emulator/Generated/*.g.cs
python scripts/diff_patch.py                  # summarize data drift vs baseline
bash scripts/patch_update.sh "<game dir>"     # runs the whole chain + build + test
```

`<game dir>` = `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2`
(the scripts auto-detect it on macOS). The decompile/extract scripts were adapted for
macOS (find `sts2.dll` under any `data_sts2_<platform>` dir; `shasum` fallback).

**Gotcha:** `src/sts2_gym/native.py` has an mtime freshness guard — if you touch any
`src/Sts2Emulator` source without rebuilding the dylib, Python calls fail. Re-run
`scripts/build.sh osx-arm64`, or set `STS2_ALLOW_STALE_NATIVE=1` for intentional stale
runs.

## The mod (STS2MCP) — build / install / API

```bash
cd ~/Projects/STSS/STS2MCP
GAMEDIR="~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
dotnet build STS2_MCP.csproj -c Release -o out/STS2_MCP -p:STS2GameDir="$GAMEDIR"
# install (mod loads at game boot → RESTART the game after installing):
cp out/STS2_MCP/STS2_MCP.dll   "$GAMEDIR/SlayTheSpire2.app/Contents/MacOS/mods/STS2_MCP/STS2_MCP.dll"
cp mod_manifest.json           "$GAMEDIR/SlayTheSpire2.app/Contents/MacOS/mods/STS2_MCP/mod_manifest.json"
```

- **API base:** `http://localhost:15526`. `GET /api/v1/singleplayer` → current state;
  `POST /api/v1/singleplayer {"action": ...}` → drive it; `GET /api/v1/compendium` →
  profile/run info (incl. `current_run.seed`).
- **Launch game:** `open "steam://rungameid/2868840"`. **Kill:** `pkill -9 -if "slay the spire 2"`.
  API health (`GET /`) comes up ~8s before `/api/v1/singleplayer` is ready.
- **Custom actions we added** (reconstructed from decompiled game APIs, see
  `McpMod.Debug.cs` / `McpMod.CustomRun.cs`):
  - `debug_start_encounter {encounter:"CorpseSlugsWeak"}` — jump straight into an encounter.
  - `debug_force_play_phase` — report play-phase readiness.
  - `return_to_main_menu` — save-and-quit to menu (the harness's abandon flow needs it).
  - **Custom-run screen support** — `singleplayer → custom` now reports as
    `character_select` (`custom_run:true`); `menu_select` drives it: select character,
    `confirm` with a `seed` (custom mode accepts `Lobby.SetSeed`), `ascension` (level in
    the `seed` field). This is how you start a **seeded** run.

## Current state — what's proven

- **Emulator is patch-current & fully working on macOS**: builds, 209 C# + 45 Python
  tests pass, NativeAOT dylib + ctypes bridge live.
- ✅ **Opening hand is bit-exact vs the live game** — the current headline result.
  Live `"ABCDEF"` custom run at A8 → `debug_start_encounter CorpseSlugsWeak`; the
  emulator reproduces the **entire 11-card shuffled deck in order** (hand + draw pile),
  verified by `scripts/compare_draw_pile.py` against the committed fixture
  `tests/fixtures/combat/ABCDEF-corpse-slugs.json`. Odds of coincidence 1 in 13,860.
- ✅ **Enemy generation matches** (`[28,29]`) — and now *causally*, since the RNG is
  correct. Note this was previously reported as proof while the RNG was still wrong,
  where it had ~17% odds of being luck; it is corroborating evidence now, but it is
  still a single 2-enemy sample and deserves more.
- ✅ **RNG is now the game's actual generator.** `Core/Rng/MegaRandom.cs` is a faithful
  port of the game's `MegaRandom` — **Xoshiro256\*\* seeded via Splitmix64** — and
  `GameRng` mirrors the game's `Rng` wrapper method-for-method (its `Counter`, and
  `NextBool` as `Next(2) == 0` rather than MegaRandom's own sign-bit variant).
  `CountingRandom` is backed by it too. **The old `DotNetRandom` (a port of .NET's
  legacy subtractive Random) was deleted** — it was the wrong algorithm and the root
  cause of every shuffle divergence; do not reintroduce it.
  Two subtleties worth keeping: the range mapping is `(int)(NextDouble() * max)`, so
  reproducing the game means reproducing that bias exactly; and `NextGaussianInt` uses
  plain `Math.Round` (banker's/to-even), not away-from-zero.
- ✅ **Run generation is exact for BOTH act-1 acts.** Act, encounter sequences, boss and
  the whole map match live captures on two Overgrowth seeds and — new — on an
  **Underdocks** one (`"UNS55LCMKP"`, A8, exact 64-node map). Underdocks had been
  modelled entirely from decompiled source and never observed; nothing needed fixing.
- ✅ **Act-1 selection verified 88/88 on the installed build, 43 of them Underdocks**,
  by replaying the profile's own run history (`scripts/verify_act_selection.py`). A
  single capture can never do better than 50/50 on a coin flip; this is the sample that
  makes the roll a result rather than a guess.
- **Seed derivation matches the game** (test `RunRngSet_DerivesGameSeedForStringSeed`:
  `RunRngSet("ABCDEF").Seed == 3334281563u`).
- **Enemy HP now rolled faithfully** (was hardcoded `fixedHp`; now uses the game's
  Niche stream + unique-HP set — commit `123fecf`).

## Key facts / mental model (save yourself the rediscovery)

- **Seeds:** the game's *input* seed is a string (e.g. `"ABCDEF"`); it derives a uint
  *gen seed* (`"ABCDEF"→3334281563`, `"0"→3452614542`). The emulator's `RunRngSet(str)`
  reproduces this. **Seedless standard runs use a RANDOM seed each time** — use **custom
  mode** for a chosen seed.
- **The direct combat env ↔ game seed bridge:** `Sts2CombatEnv(int seed)` sets its per-
  stream RNGs to `GameRng(seed, "<stream>")`. So passing the game's *derived gen seed*
  (3334281563) makes the combat env use the same streams as the `"ABCDEF"` run. That's
  why the enemy match works.
- **Named RNG streams** (`RunRngSet`): `Niche` (enemy HP, `SetUniqueMonsterHpValue`),
  `Shuffle` (deck), `CombatCardGeneration`, `MonsterAi`, etc. Each is
  `GameRng(seed, "<name>")`. The emulator run env (`RunEngine`) advances each to the
  game's CallCount; the **direct combat env assumes fresh streams** (CallCount 0) — fine
  for first-combat, a limitation otherwise.
- **Weak encounter variants:** `completed_combat_rooms` in `[0,3)` selects the weak
  variant (e.g. CorpseSlugsWeak = 2 slugs vs Normal = 3). Wired through
  `Sts2CombatEnv(..., completed_combat_rooms=)` and `Sts2_ResetEncounterWeak` (native
  API v11; the API is at v12 as of the pile-introspection export).
- **Ascension:** the emulator models high ascension (`ToughEnemies` values). Live runs
  at A8 give player 64/80 HP and CorpseSlug 27–29, matching the emulator.
- **Save file** (custom run): `~/Library/Application Support/SlayTheSpire2/steam/
  76561198104489966/profile1/saves/current_run.save` — `rng.seed` = input string,
  `players[0].rng.seed` = gen seed.

## Gotchas / known issues

- ✅ **`scripts/extract_data.py` is fixed and safe to run.** It used to renumber every
  id (ids were a counter over `sorted(glob(...))`, so one added card shifted everything
  after it) and drop the 10000-range status cards. **`data/id_map.json` now freezes the
  mapping**: known names keep their id, new names append, removed names keep theirs
  reserved so an id is never recycled onto different content. Re-running is now
  reproducible, and `scripts/patch_update.sh` is no longer destructive.
  Seed/re-freeze the map with `scripts/build_id_map.py` (deliberate action only).
- **`Folly` and `Writhe` are missing from `Cards.g.cs`** — both are canonically Innate
  cost-`-1` curses dropped by the same `cost < 0` filter. Harmless today (starter decks
  have neither) but they'd be *unknown cards*, not merely misordered, if ever drawn.
- ⚠️ **Embark crash — almost certainly OUR mod's doing, not a game bug.** Mod-driven
  embarks NRE in `NRunMusicController.UpdateTrack()` (its `_runState` is still
  `NullRunState`, so `CurrentRoom` is null) from `RunManager.EnterRoomInternal`, giving
  the "internal error!" popup. An earlier version of this file called it an unfixable
  game bug — **that was wrong.** The owner has hundreds of hours of normal play without
  it, and a *manual* embark on the same seed and profile worked immediately after a
  scripted one crashed.
  **Leading explanation, and it looks strong:** the game ships its own automation
  (`MegaCrit.Sts2.Core.AutoSlay.AutoSlayer`) which drives the UI with the *same*
  `NClickableControl.ForceClick` we use — but it also sets
  `NonInteractiveMode.AutoSlayerCheck = () => IsActive`. And the crashing line is
  literally guarded: `UpdateTrack()` does `if (!NonInteractiveMode.IsActive) { ...
  _runState.CurrentRoom.RoomType ... }`. MegaCrit's automation never reaches it; ours
  does, and loses a race against `NRun._Ready`.
  **Proposed fix (untested):** have the mod enable non-interactive mode while the
  harness is driving, mirroring AutoSlayer. All 31 `NonInteractiveMode.IsActive` call
  sites were checked and every one is timing, pausing or audio (`Cmd.Wait`,
  `ActionExecutor`, `CombatManager.Pause/Unpause`, `SfxCmd`, `NAudioManager`) — **none
  touch RNG, card effects or rules**, so it is safe for differential testing and would
  also skip animation waits, making capture much faster.
  **Until that is tested**, the workaround still applies: the crash fires *after* the run
  is created and written to `current_run.save`, and loading that save works cleanly, so
  restart → **Continue** → `--jump-encounter`.
- **`AbandonRun` also throws** when `current_run.save.backup` is absent ("Error deleting
  path … Failed"). Independent of the above; the preflight in `compare_draw_pile.py`
  refuses to drive the in-game abandon unless `--abandon` is passed.
  **Fix: hand it the file it wants to delete.** `cp current_run.save
  current_run.save.backup` first, then `menu_select abandon_run` and confirm the popup
  (`menu_select yes` — the abandon raises a yes/no popup, and the main menu only offers
  `singleplayer` again once it is answered). Verified working.
  **And one data point on the embark crash above:** with the previous run abandoned that
  way, `start_real_game_run.py UNS55LCMKP --ascension 8` embarked **cleanly on the first
  try** — no popup, no `NRunMusicController` NRE in `godot.log`, run alive at Neow — and
  that is how the Underdocks capture was taken. n=1, so it does not settle the
  NonInteractiveMode theory; but it does mean a mod-driven embark is *not* doomed, and it
  points at the failed-abandon teardown as at least a contributing state. Worth an A/B
  before investing in the mod change.
- **Deleting `current_run.save` does nothing — Steam Cloud restores it** on the next
  launch (history files come back too). Removing a run means abandoning it in game (see
  above), not moving the file aside.
- **Seeded runs need CUSTOM mode.** Standard mode rejects a chosen seed outright
  ("Seed should not be changed in standard mode!"). `start_seeded_run` defaults to
  `mode="custom"` for this reason.
- **Driving the game hard CRASHES it.** Rapid `abandon → re-embark →
  debug_start_encounter` cycling triggers an error popup (`report_bug`, needs restart).
  A *single* clean sequence with generous `time.sleep` waits is stable. **Follow-on:**
  add settled-state guards to the debug/menu actions before any unattended sweep.
- **macOS NativeAOT exports:** the csproj uses `[UnmanagedCallersOnly(EntryPoint=...)]`
  (cross-platform) — we removed the original Windows-only `/EXPORT:` linker args. If you
  add a native export, add its `[UnmanagedCallersOnly(EntryPoint="...")]` and a matching
  `native.py` binding; bump `NATIVE_API_VERSION` + `_REQUIRED_NATIVE_API_VERSION` together.
- The harness (`scripts/validate_real_game_trace.py`) depends on `debug_start_encounter`
  etc. — those are **our mod additions**, not in any public STS2MCP. Keep the fork.
- `start_replay` (for clean full-run capture) is **not built** — investigated; it's a
  self-contained mod action to add (see docs/replay-verification.md), NOT a RunReplays dep.

## Next work (prioritized, with pointers)

**Combat start is bit-exact** (opening hand + enemies, see "what's proven"). The open
front is now *run generation*: what the engine rolls up front for a seed.

`scripts/verify_run_generation.py` compares the emulator against a live
`current_run.save` — plain JSON recording exactly what the game generated
(`acts[i].id`, `rooms.{normal,elite}_encounter_ids`, `boss_id`, `saved_map.points`).
**No need to drive the game**; just have a run saved, and note the crashed-embark save
is still a valid capture. Verified on **three seeds — now including an Underdocks act 1**:

| section | "ABCDEF" (Overgrowth, A8) | "AAB" (Overgrowth, A8) | "UNS55LCMKP" (**Underdocks**, A8) |
|---|---|---|---|
| act | PASS | PASS | PASS |
| normal encounters | PASS 15/15 | PASS 15/15 | **PASS 15/15** |
| elite encounters | PASS 15/15 | PASS 15/15 | **PASS 15/15** |
| boss | PASS (TheKin) | PASS (CeremonialBeast) | **PASS (WaterfallGiant)** |
| map | PASS (exact) | **PASS (exact)** | **PASS (exact, 64 nodes / 17 rows)** |

**Underdocks needed nothing fixed — every act-specific branch was already right.** Worth
knowing exactly what that capture cleared, because all of it was modelled from the
decompiled act and never observed: its four weak / ten normal / three elite / three boss
pools *and their order* (declaration order, per `Acts/Underdocks.cs`), and — the piece
most likely to have been wrong — the up-front RNG burn in `RunMapGenerator`, which is
`202 + (underdocks ? 57 : 60)` calls. That 57 was *derived*, not measured: Underdocks
declares 3 fewer act events than Overgrowth (10 vs 13; +18 shared either way, matching
the live save's 31 `event_ids`), and everything else up front was assumed act-independent
even though Underdocks differs elsewhere (one `BgMusicOptions` entry vs two, its own
background dir). Off by one call and the entire encounter sequence and map would desync,
so the exact map match is strong evidence the whole burn is right.

**The second seed earned its keep — it caught three defects one sample could not:**
- **Act selection was wrong in mechanism and stream.** It was `NextBool()` on the
  *unnamed* raw-seed stream. The game uses `rng.NextItem` over the unlocked acts for
  that index on a dedicated **`"act_selection"`** stream
  (`StartRunLobby.BeginRunLocally`). "ABCDEF" passed by luck — with a two-way roll a
  single sample cannot distinguish a correct model from a coin flip.
  ⚠️ **Not seed-pure**: the candidate list is whatever the *profile* has unlocked, and
  the game force-selects an unlocked-but-undiscovered alt act instead of rolling. We
  model the mature-profile case. Same caveat as boss discovery.
- **`NibbitsNormal` was wrongly tagged `Nibbit`.** Only `NibbitsWeak` declares that tag;
  `NibbitsNormal` overrides nothing and inherits the empty default. The bogus tag made
  the no-repeat rule block the game's legitimate `NibbitsWeak -> NibbitsNormal` run,
  shifting the whole remaining sequence by one (2/15 -> 15/15 once fixed).
- **The map stream was keyed on act *identity*, not act *index*** (`state.Act - 1`).
  An Underdocks act 1 would have read `"act_2_map"` and desynced the entire map. The
  index for act 1 is always 0.
- Two more invented names corrected: `Nibbit` -> `NibbitsWeak`, `Nibbits` ->
  `NibbitsNormal` (old Python encounter strings still resolve as aliases).

**Run generation is now exact on both captured seeds.** The last residual — "AAB" row 1
holding a node at the wrong column — was **edge insertion order**. The game wires the
start node's children with `ForEachInRow`, which walks grid columns 0..6; we wired them
in *path-draw* order (the order the 7 starts were rolled). That insertion order becomes
the child-enumeration order in `FindAllPaths`, which sets the order segments land in
their duplicate group — and `PrunePaths` shuffles each group before keeping one, so a
different starting order prunes a different node. Same fix applied to the row-15 → boss
edges. Worth remembering: **wherever the game uses `ForEachInRow`, order is column
order, and it is load-bearing, not cosmetic.**

### Ground-truth fixtures (committed)

Live captures are **destroyed by the next run** — starting a new run overwrites
`current_run.save`. That already cost us "ABCDEF"'s full map ground truth, which now
survives only as partial literals in `RunGeneration_MatchesLiveCaptureForAbcdef`.
So captures are committed:

- `tests/fixtures/run_generation/AAB.json` — distilled from a live save: act,
  encounter id sequences, boss, and the full `saved_map`. **Profile data is stripped**
  (no `unlock_state`, play history or account id); a test asserts that stays true.
- `tests/fixtures/run_generation/UNS55LCMKP.json` — the same, for an **Underdocks**
  act 1. Keep both: act 1 is a coin flip and the two acts run down different branches,
  so one fixture per act is the minimum that exercises them.
- `tests/fixtures/act_selection/v0.107.1.json` — 88 (seed -> rolled act) pairs, 43 of
  them Underdocks, distilled from the profile's own run history (see below). Seeds and
  acts only, no account id or timestamps.
- `tests/fixtures/combat/ABCDEF-corpse-slugs.json` — the ordered-pile capture proving
  the opening hand.
- `tests/python/test_live_fixtures.py` runs the **real comparison code** against them,
  so the full structure is checked rather than a hand-transcribed subset. Each run-
  generation fixture gets the full comparison via the `RunGenerationChecks` mixin — add
  a capture by naming it in a two-line subclass.
- Every fixture is checked for two preconditions rather than trusting them: the **game
  version stamp** (all fixtures must agree, and `verify_run_generation.py` shouts when
  the installed game has moved on) and the **profile facts** — act selection and boss
  discovery read the profile, so a capture from a fresher account is not comparable.
  The boss check reads *the captured act's own* `BossDiscoveryOrder`.

Capture more with:
```bash
python scripts/verify_run_generation.py --save-fixture tests/fixtures/run_generation/<SEED>.json
python scripts/compare_draw_pile.py --seed <SEED> --encounter <enc> --jump-encounter \
    --save-live-json tests/fixtures/combat/<SEED>-<enc>.json
```
`verify_run_generation.py --fixture <path>` then re-runs offline, with no game needed.

### Act selection is verified in bulk, from the profile's run history

`saves/history/*.run` keeps one record per finished run, carrying its `seed` and the
`acts` it rolled — **hundreds of free (seed -> act) ground-truth pairs**, no game
driving and no capture needed. `scripts/verify_act_selection.py` replays them through
the emulator: **88/88 on the installed v0.107.1, 45 Overgrowth / 43 Underdocks.** That
turns the act-1 coin flip from "one sample, 50% odds of luck" into a real result.

Read the per-build breakdown carefully: older builds sit near 50%, which is expected,
not a regression. A record is ground truth for *its own* patch, and early runs also hit
`GetRandomList`'s force-select path (an unlocked-but-undiscovered alt act is taken
instead of rolled) rather than rolling at all. Only the installed build's rows are a
statement about the emulator as it stands.

The history records carry no rooms and no map, so this verifies **act selection only** —
the rest still needs a `current_run.save` capture per act.

**Wanted next:** a re-capture of "ABCDEF" run generation (its save is gone), and an
Underdocks *combat* capture — `compare_draw_pile.py` ground truth is still Overgrowth-
only, and the "UNS55LCMKP" run is sitting at Neow, ready to jump into an encounter.

### Introspection & verification tooling (built)
- `scripts/compare_draw_pile.py` — emulator vs live ordered piles. `--live-json`
  re-diffs a saved capture offline; `--jump-encounter` avoids the lobby crash.
- `scripts/verify_run_generation.py` — the table above, straight from a save.
- `scripts/verify_act_selection.py` — act 1 vs the whole run history; `--fixture` re-runs
  it offline, `--all-builds` shows older patches for context.
- `scripts/start_real_game_run.py <SEED> --ascension 8` — embark a seeded custom run.
  Pass `--ascension 8`: the emulator models A8, and a capture at another level is not
  comparable (the elite budget differs, so encounters and map both diverge).
- Emulator: `Sts2_GetPile` -> `env.get_pile(...)`; run-generation lists 11-14 on
  `Sts2Run_GetStateList` (normal/elite/event sequences, and `[act, boss, map_nodes]`).
  Native API **v13**.
- Live: our STS2MCP fork emits `draw_pile_ordered` / `discard_pile_ordered` /
  `hand_ordered` under `result["player"]`. The stock `draw_pile` is **sorted for
  display**, which is why ordered comparison was impossible before.
- `sts2_gym.game_seed("ABCDEF") -> 3334281563` — no more 500k brute-force seed search.
- **Encounter names were corrected** against the game's act pool: `ShrinkerAndFuzzy` ->
  `OvergrowthCrawlers`, `LargeSlimes` -> `SlimesNormal`, `SlimeAndFlyconid` ->
  `FlyconidNormal`, `JaxfruitAndFlyconid` -> `SnappingJaxfruitNormal`. The emulator had
  invented those four labels. Old Python encounter strings still resolve as aliases.

## Patch playbook (when a new StS2 build lands)

```bash
python scripts/patch_refresh.py           # report: what changed, what broke, what is stale
python scripts/patch_refresh.py --apply   # also decompile + extract + diff
```

`patch_refresh.py` does everything mechanical and **classifies** the fallout:

- Detects the installed Steam buildid against `data/game_version.json`.
- Re-decompiles and re-extracts (ids are stable, so this is safe now). Extraction
  **aborts with exit 1** if a card-id constant names a card the patch removed.
- Splits test failures into **mechanism** (an algorithm changed — investigate, do NOT
  re-baseline) and **content** (values moved — check each against the decompiled
  source before updating).
- Lists stale fixtures by version stamp and prints the exact re-capture commands.
- Records the new build **only** once tests pass and fixtures are current, so the
  recorded version always means "verified against this build".

**Expectations sourced from the game are regenerated for you.** Re-capture a fixture
with `--save-fixture`, then run `scripts/generate_capture_tests.py` — it rewrites
`Generated`-style C# capture assertions (`RunGenerationCaptures.g.cs`) straight from
the fixtures. That is not a rubber stamp: only the *game* side moves, so an emulator
regression still fails the comparison.

**What it will not do is rewrite expectations from the emulator's own output.** Auto-updating an
assertion to whatever the code now produces turns a regression detector into a rubber
stamp — the failing DarkEmbrace test is precisely how the Exhaust-flag bug surfaced,
and a script that "fixed" it would have buried a defect affecting ~30 cards. Ground
truth also cannot be regenerated from the emulator by definition; it has to come from
the game.

### The modelled profile (why generation is seed-deterministic)

Two decisions read the **profile**, not the seed: Act 1 is rolled only among *unlocked*
acts (an unlocked-but-undiscovered one is force-selected instead), and the boss is
overwritten by the first Act-1 boss the profile has never seen
(`ActModel.ApplyDiscoveryOrderModifications`). The emulator models **one fixed profile —
everything unlocked and already discovered** — which collapses both to plain rolls and
is what self-play will need. Captures record `profile.all_act1_bosses_seen` and
`all_act1_acts_discovered`; `verify_run_generation.py` prints **PROFILE MISMATCH** and a
test fails if a fixture came from a fresher account, because such a capture encodes
different rules and is not comparable.

## One-shot smoke test (verify the whole stack works)

```bash
cd ~/Projects/STSS/emulator
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
dotnet test src/Sts2Emulator.Tests/        # 208 pass
bash scripts/build.sh osx-arm64            # → out/Sts2Emulator.dylib
uv run python -m unittest discover -s tests/python   # 45 pass
```
