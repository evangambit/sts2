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

# C# unit tests (currently 207 pass)
dotnet test src/Sts2Emulator.Tests/

# Build the NativeAOT dylib the Python layer loads (→ out/Sts2Emulator.dylib)
bash scripts/build.sh osx-arm64

# Python gym tests (25 pass) — drives the live dylib via ctypes
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

- **Emulator is patch-current & fully working on macOS**: builds, 207 C# + 25 Python
  tests pass, NativeAOT dylib + ctypes bridge live.
- ✅ **Opening hand is bit-exact vs the live game** — the current headline result.
  Live `"ABCDEF"` custom run at A8 → `debug_start_encounter CorpseSlugsWeak`; the
  emulator reproduces the **entire 11-card shuffled deck in order** (hand + draw pile),
  verified by `scripts/compare_draw_pile.py` against the capture in
  `/tmp/live_abcdef.json`. Odds of coincidence 1 in 13,860.
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

- **`scripts/extract_data.py` CANNOT reproduce `src/Sts2Emulator/Generated/*.g.cs` — do
  not run it blind.** Both came from the single "Initial commit", but re-running the
  script against the current `decompiled/` **renumbers every card/enemy/power/relic id**
  and **drops the 10000-range status cards** (Infection, Burn, Disintegration, Wound,
  Wither, SpoilsMap — the script's `SPECIAL_CARD_IDS` only maps `AscendersBane`/`Dazed`,
  so its `cost < 0` filter discards them). That renumbering silently broke 6 tests when
  first tried. The committed id order is *not* any filename sort (case-sensitive or not),
  so it came from some upstream process this script doesn't replicate. **This means
  `scripts/patch_update.sh` is effectively broken** — it chains `extract_data.py`.
  Fixing the extractor to be id-stable is a prerequisite for any future patch bump.
  (The Innate flags were therefore applied to `Cards.g.cs` surgically by name, ids
  untouched; `extract_data.py` also has the correct Innate logic for when it's fixed.)
- **`Folly` and `Writhe` are missing from `Cards.g.cs`** — both are canonically Innate
  cost-`-1` curses dropped by the same `cost < 0` filter. Harmless today (starter decks
  have neither) but they'd be *unknown cards*, not merely misordered, if ever drawn.
- **Embarking a run through the lobby CRASHES the game — loading a save does not.**
  Every mod-driven embark NREs in `NRunMusicController.UpdateTrack()` (its `_runState`
  is still `NullRunState`, whose `CurrentRoom` is null) from `RunManager.EnterRoomInternal`,
  giving the "internal error!" popup. **It is a game bug in a UI path — not fixable from
  our side, so route around it.** Ruled out by log evidence, so don't re-chase these:
  menu churn / rapid actions, the abandon flow, window focus/backgrounding, and the
  10s startup 'Common' preload overlapping the embark. All four correlate with *both*
  successes and failures.
  **The crash is recoverable** — it fires *after* the run is created and written to
  `current_run.save`, and loading that save uses a different path
  (`isRestoringRoomStackBase`) that works cleanly (verified: `Continuing run with
  character: CHARACTER.IRONCLAD` → Event Room → zero errors). **The working loop:**
  1. `compare_draw_pile.py --start-run …` → embark → game crashes (save is written).
  2. `pkill -9 -if "slay the spire 2"; sleep 3; open "steam://rungameid/2868840"`.
  3. Click **Continue** — *not* New Run, and do **not** abandon.
  4. `compare_draw_pile.py --jump-encounter …` → captures without touching the lobby.
  The script detects the popup and prints this recipe (`explain_embark_crash`).
- **`AbandonRun` also throws** when `current_run.save.backup` is absent ("Error deleting
  path … Failed"). Independent of the above; the preflight in `compare_draw_pile.py`
  refuses to drive the in-game abandon unless `--abandon` is passed.
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
`current_run.save` — which is plain JSON and records exactly what the game generated
(`acts[i].id`, `rooms.{normal,elite}_encounter_ids`, `boss_id`, `saved_map.points`).
**No need to drive the game**; just have a run saved. Current result for `"ABCDEF"`:

| section | result |
|---|---|
| act | PASS (OVERGROWTH) |
| normal encounters | **PASS — 15/15 in order** |
| elite encounters | **PASS — 15/15 in order** |
| boss | **PASS** (TheKin) |
| map | FAIL — 13/16 rows exact; rows 10-12 differ |

1. ~~Elite pool / boss selection~~ — **DONE**, both were the same root cause and are
   now exact (regression test `RunGeneration_MatchesLiveCaptureForAbcdef`). Two defects,
   worth remembering because the pattern will recur for other acts:
   - **Pool order must be the act's encounter-*declaration* order** (the game builds
     `AllEliteEncounters`/`AllBossEncounters` by filtering `AllEncounters`, declared
     alphabetically in e.g. `Acts/Overgrowth.cs`). It is **not** `BossDiscoveryOrder` —
     that list only feeds `ActModel.ApplyDiscoveryOrderModifications`, an unlock-
     progression override that overwrites the boss with the first one the *profile*
     has never seen. On a profile that has seen them all (this one: 390 runs) it does
     nothing, so boss selection is a plain roll. **Note this means boss choice is not a
     pure function of the seed on a fresh profile.**
   - **Elites go through the same no-repeat draw as normals.** The game calls
     `AddWithoutRepeatingTags` for elites too, so an elite never immediately repeats;
     we were doing a plain indexed draw. The first 9 matched by luck and diverged at
     the 4th bag refill.
   - The boss is rolled from the **same stream immediately after the 15 elites**, so a
     wrong elite draw count silently corrupts the boss. Fixing the elites fixed the boss.
   - ⚠️ Underdocks had both defects too and is fixed by the same rule, but is
     **unverified** — the only live capture so far is an Overgrowth run.
2. **Map generation — 13/16 rows exact, 2 nodes over. Cause still unknown; the whole
   pruning port has been audited line-by-line against the game and matches.**
   `verify_run_generation.py` diffs map structure row by row (native list 15). Rows 0-9
   and 13-16 are exact; rows 10-12 differ, and the emulator keeps **2 nodes the game
   prunes** (66 vs 64 incl. start+boss).

   **Ruled out — do not re-audit these, they were compared against the decompiled
   source and match:**
   - Stream/seed: uses `ActMapRng` -> `act_1_map`. (An older note here claiming it used
     the bare run seed was wrong; that was the act coin-flip.)
   - `GetMapPointTypes`: both draws, in order, right distributions.
   - Counts: 8 elites (A8 SwarmingElites), 3 shops, 12 unknowns, 6+5 rests.
   - Path generation: 7 paths, `i==1` distinct-start retry, per-step
     `StableShuffle([-1,0,1])`, clamping, loop bounds. **RNG accounting confirms it**:
     207 draws before type assignment = 7 starts + 7 paths x 14 steps x 2 + 4 gaussian,
     i.e. exactly right with no retries.
   - Forced rows: row 1 monster, row 9 treasure, row 15 rest — all match live.
   - Type-assignment structure: 3-pass loop, `StableShuffle` of unassigned points,
     `GetNextValidPointType` queue rotation, and every validity rule
     (`IsValidForLower` row<6, `IsValidForUpper` row>=13, parents∪children, children,
     siblings-via-parents' children). Start/boss are correctly excluded (the game's
     `GetAllMapPoints` walks only the Grid, and neither is in it).
   - Pruning: `FindAllPaths`, `AddSegmentsToDictionary`, `GenerateSegmentKey` (incl. the
     `row == 0` start-node special case), `IsValidSegmentStart/End`, `OverlappingSegment`,
     `PruneAllButLast`, `PruneSegment`, `BreakAParentChildRelationship...`, `IsInMap`,
     and the SortedDictionary/Ordinal iteration order. The only textual difference is a
     missing `&& !IsRemoved(grid, n)` in `PruneSegment`, which is **vacuous** —
     `RemoveChildPoint` unlinks both directions, so a removed node can never still be in
     someone's parents list.
   - Pruning is **not** stopping early: instrumented, it runs to exhaustion
     (7 duplicate groups -> 0, 71 -> 66 nodes, 6 RNG draws), then repair finds nothing.

   **Where to look next.** Both sides reach a no-duplicates fixed point, just different
   ones (66 vs 64), so the graphs must already differ *before* pruning even though the
   draw count is right. Best remaining hypotheses, in order:
   a. `MapPostProcessing.CenterGrid` / `SpreadAdjacentMapPoints` / `StraightenPaths` —
      these were never compared against the decompiled source (273 lines) and
      `StraightenPaths` in particular could merge nodes.
   b. The pre-prune graph itself: instrument the emulator to dump the raw 71-node graph
      with edges, and reason backwards from the live `saved_map.points[].children`.
   c. Node identity/dedup in `GetOrCreate` when two paths cross the same coord.
3. **Harden the debug/menu mod actions.** Partly done — `start_real_game_run.settle()`
   guards menu transitions, the abandon path is no longer driven, and the embark crash
   has a documented route around it. Remaining: the crash itself is a *game* bug (see
   Gotchas); `--jump-encounter` avoids it.
4. **Implement `start_replay`** in the STS2MCP fork -> clean deterministic full-run
   capture (design in docs/replay-verification.md SS1).
5. **Full-run replay harness** -> the primary fidelity metric (docs/replay-verification.md).
6. Then the **AlphaZero layer**: MCTS over the sim (C#) -> value/policy net (Python) ->
   self-play (PLAN.md SS2, SS5).

### Introspection & verification tooling (built)
- `scripts/compare_draw_pile.py` — emulator vs live ordered piles. `--live-json`
  re-diffs a saved capture offline; `--jump-encounter` avoids the lobby crash.
- `scripts/verify_run_generation.py` — the table above, straight from a save.
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

## One-shot smoke test (verify the whole stack works)

```bash
cd ~/Projects/STSS/emulator
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
dotnet test src/Sts2Emulator.Tests/        # 207 pass
bash scripts/build.sh osx-arm64            # → out/Sts2Emulator.dylib
uv run python -m unittest discover -s tests/python   # 25 pass
```
