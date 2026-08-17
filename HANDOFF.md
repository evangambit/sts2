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
`current_run.save` — plain JSON recording exactly what the game generated
(`acts[i].id`, `rooms.{normal,elite}_encounter_ids`, `boss_id`, `saved_map.points`).
**No need to drive the game**; just have a run saved, and note the crashed-embark save
is still a valid capture. Verified on **two seeds**:

| section | "ABCDEF" (Overgrowth, A8) | "AAB" (Overgrowth, A8) |
|---|---|---|
| act | PASS | PASS |
| normal encounters | PASS 15/15 | PASS 15/15 |
| elite encounters | PASS 15/15 | PASS 15/15 |
| boss | PASS (TheKin) | PASS (CeremonialBeast) |
| map | PASS (exact) | 15/16 rows; 61/61 nodes, row 1 one column off |

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

**Open residual — "AAB" map row 1.** Node counts match exactly (61 vs 61) and 15 of 16
rows are column-for-column identical. Row 1 is `{0,3,5}` where live is `{1,3,5}`. The
raw path starts give 5 distinct columns `{0,1,2,3,5}` and pruning cuts row 1 to 3 — the
game drops `{0,2}`, we drop `{1,2}`. So a single pruning choice differs. Ruled out:
`StraightenPaths` (that node is not a kink — its parent is at col 3, child at col 0),
`SpreadAdjacentMapPoints` (`GetAllowedPositions` intersects to empty there, so it cannot
move), and `CenterGrid` (shifts the whole grid uniformly, so it cannot affect one row).
Best remaining lead: **path/segment enumeration order**. The game stores
`MapPoint.parents`/`Children` in a `HashSet<MapPoint>` while we use `List`, so
`FindAllPaths` may enumerate children in a different order, changing which segment
survives `PruneAllButLast`.

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
2. ~~Map generation~~ — **DONE. `verify_run_generation.py` now reports ALL SECTIONS
   MATCH** for `"ABCDEF"`: act, 15/15 normals, 15/15 elites, boss, and the map exact on
   every row, column and point type (64 nodes incl. start + boss). Pinned by
   `RunGeneration_MatchesLiveCaptureForAbcdef`.
   Two defects, both in post-prune type repair, and the second only visible once the
   first was fixed:
   - **`RepairPrunedPointTypes` repaired elites toward a hardcoded 5 while
     `AssignPointTypes` placed 8.** So with 7 elites on the map, repair computed
     `5 - 7 = -2`, decided nothing was missing, returned false — and `PruneAndRepair`
     broke out of its loop after a single pass. The game computes `8 - 7 = 1`, converts
     a Monster, returns true, and **prunes again**. That missing second pass was the
     entire 2-node gap. Both sites now use `RunConstants.MapEliteCount` /
     `MapShopCount`. **Lesson: the type budget must be one constant** — assignment and
     repair silently disagreeing is invisible until you diff a real map.
   - **`CanBeModified` was not modelled.** The game sets it false on the forced rows
     (row 1, treasure row, final rest row) and repair only considers
     `PointType == Monster && CanBeModified`. We offered row 1's monsters as repair
     candidates, which changed both the shuffle length and the chosen node — repair
     converted (0,8) where the game converted (0,12). `RunMapNode.CanBeModified` now
     exists and is set in `AssignPointTypes`.
   Also fixed earlier in the hunt: segment keys embed point types as integers and sort
   in a SortedDictionary, so they must use the **game's** `MapPointType` values, not our
   node-type numbering (`GameMapPointType`).
3. **Harden the debug/menu mod actions.** Partly done — `start_real_game_run.settle()`
   guards menu transitions, the abandon path is no longer driven, and the embark crash
   has a documented route around it. Remaining: the crash itself is a *game* bug (see
   Gotchas); `--jump-encounter` avoids it.
4. **Implement `start_replay`** in the STS2MCP fork -> clean deterministic full-run
   capture (design in docs/replay-verification.md SS1).
5. **Full-run replay harness** -> the primary fidelity metric (docs/replay-verification.md).
6. Then the **AlphaZero layer**: MCTS over the sim (C#) -> value/policy net (Python) ->
   self-play (PLAN.md SS2, SS5).

### Ground-truth fixtures (committed)

Live captures are **destroyed by the next run** — starting a new run overwrites
`current_run.save`. That already cost us "ABCDEF"'s full map ground truth, which now
survives only as partial literals in `RunGeneration_MatchesLiveCaptureForAbcdef`.
So captures are committed:

- `tests/fixtures/run_generation/AAB.json` — distilled from a live save: act,
  encounter id sequences, boss, and the full `saved_map`. **Profile data is stripped**
  (no `unlock_state`, play history or account id); a test asserts that stays true.
- `tests/fixtures/combat/ABCDEF-corpse-slugs.json` — the ordered-pile capture proving
  the opening hand.
- `tests/python/test_live_fixtures.py` runs the **real comparison code** against them,
  so the full structure is checked rather than a hand-transcribed subset. The AAB map
  residual is pinned as "mismatching rows == {1}" — fixing it will fail the test and
  ask to be updated, which is the intent.

Capture more with:
```bash
python scripts/verify_run_generation.py --save-fixture tests/fixtures/run_generation/<SEED>.json
python scripts/compare_draw_pile.py --seed <SEED> --encounter <enc> --jump-encounter \
    --save-live-json tests/fixtures/combat/<SEED>-<enc>.json
```
`verify_run_generation.py --fixture <path>` then re-runs offline, with no game needed.

**Wanted next:** a re-capture of "ABCDEF" run generation (its save is gone), and any
seed that actually rolls **Underdocks** — still entirely unverified.

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

## Patch playbook (when a new StS2 build lands)

Tests are tiered by what they are pinned to; treat a failure differently in each.

1. `bash scripts/decompile.sh "<game dir>"` then `python scripts/extract_data.py`.
   Ids are now stable (`data/id_map.json`), so this is safe. **Watch for `NEW ...`
   lines** — those are ids appended for content this patch introduced.
2. `python scripts/diff_patch.py` to triage what actually changed.
3. **Tier 1 — mechanism tests** (RNG, seed derivation, shuffle, turn-1 reorder, map
   pruning). These should stay green through any *content* patch. A failure means an
   algorithm changed — **investigate, never re-baseline casually.**
4. **Tier 2 — content tests.** Expect churn when values change. The `...OutputsAreLocked`
   RNG pins are locks over our own output, not ground truth; re-pin them only once
   Tier 1 is green.
   ✅ **`IC`/`CL`/`SI`/`AN`/`ST` are now generated** into
   `Generated/CardIds.g.cs` from the freshly extracted card data, so a constant can
   never disagree with `Cards.g.cs`. Membership stays curated in
   `data/card_id_classes.json` (the card data carries no character/colour), but if a
   patch renames or removes a card, extraction **fails with exit 1** naming the dead
   constants rather than letting one point at whatever else took its id.
5. **Tier 3 — live fixtures.** Re-capture them: they are ground truth for one patch
   only. Fixtures carry a `game` stamp (Steam buildid + release), and
   `verify_run_generation.py` prints a loud **GAME VERSION MISMATCH** when the stamp
   does not match the installed game, so stale captures cannot masquerade as bugs.
6. Rebuild the dylib and re-run everything (`208 C# + 35 Python` at time of writing).

**The failure mode to watch for is a *quiet* one:** data that extracts without error but
is wrong. That is what happened with the keyword flags below — regenerating surfaced ~30
cards wrongly marked `Exhaust`, and only a behavioural test caught it.

## One-shot smoke test (verify the whole stack works)

```bash
cd ~/Projects/STSS/emulator
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
dotnet test src/Sts2Emulator.Tests/        # 207 pass
bash scripts/build.sh osx-arm64            # → out/Sts2Emulator.dylib
uv run python -m unittest discover -s tests/python   # 25 pass
```
