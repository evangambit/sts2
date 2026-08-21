# Handoff — Slay the Spire 2 AI project

Orientation for a future agent. **PLAN.md** has the full design + chronological
history and the _why_ behind every decision; **docs/replay-verification.md** has the
full-run verification design. This file is the _current state + how-to + gotchas +
next steps_. Read this first, then PLAN.md for depth.

## What this is

Building an AlphaZero-style agent for **Slay the Spire 2** (a fast headless simulator +
MCTS + a value/policy net). The simulator is a **fork of Zamiell's StS2 emulator**
(C# NativeAOT core + Python/Gymnasium). Current phase: making the emulator bit-exact
against the live game via differential testing, before building the AlphaZero layer.

The game is **StS2 v0.107.1**, C# on Godot 4, installed at:
`~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app`

## Repos & directories (under `~/Projects/STSS/`)

| Dir                | Repo                              | What                                                                                                                                                      |
| ------------------ | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `emulator/`        | **github.com/evangambit/sts2**    | The emulator fork (this repo). C# sim + Python gym + scripts + docs.                                                                                      |
| `STS2MCP/`         | **github.com/evangambit/STS2MCP** | Our fork of Gennadiyev's STS2MCP mod (exposes game state/actions over HTTP). We added the custom actions below. `origin`=our fork, `upstream`=Gennadiyev. |
| `STS2MCP-zamiell/` | Zamiell/STS2MCP                   | Reference-only clone (checked for debug actions; **not used** — safe to delete).                                                                          |

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

# C# unit tests (currently 800 pass)
dotnet test src/Sts2Emulator.Tests/

# Build the NativeAOT dylib the Python layer loads (→ out/Sts2Emulator.dylib)
bash scripts/build.sh osx-arm64

# Python gym tests (140 pass) — drives the live dylib via ctypes
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
  - `debug_add_card {card:"MOLTEN_FIST"|"MoltenFist", upgraded:false, pile:"hand"}` — put a
    chosen card in a chosen pile. Takes the entry id **or** the C# class name, so callers
    need no id map. Must clone via `CombatState.CreateCard`, not `ToMutable()`: an
    ownerless clone NREs inside `AddGeneratedCardToCombat` and the action answers "ok"
    while nothing appears.
  - `debug_set_energy {amount:9}` — so a card's cost never decides whether a capture happens.
  - `debug_add_power {power:"VULNERABLE_POWER", amount:2, target:"CORPSE_SLUG_0"|"player"}` —
    stage a power so a capture can reach a card's conditional branch. `PowerCmd.Apply`
    needs a choice context and stalls unless its task is driven the way `CombatManager`
    drives its own hooks (`HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion`).
  - `return_to_main_menu` — save-and-quit to menu (the harness's abandon flow needs it).
  - **`rooms_entered`** in every state payload — a count of `RunManager.RoomEntered`
    events, i.e. room entries that have finished. Room entry is async and the state
    reports a run before it is done entering; waiting for this counter to advance is
    what stopped the embark crash. **Reinstall the mod** if `rooms_entered` is missing.
  - **Custom-run screen support** — `singleplayer → custom` now reports as
    `character_select` (`custom_run:true`); `menu_select` drives it: select character,
    `confirm` with a `seed` (custom mode accepts `Lobby.SetSeed`), `ascension` (level in
    the `seed` field). This is how you start a **seeded** run.

## Current state — what's proven

- **Emulator is patch-current & fully working on macOS**: builds, 800 C# + 140 Python
  tests pass, NativeAOT dylib + ctypes bridge live.
- ✅ **Combat starts are exact across 32 live captures** — 16 encounters (both pools,
  both acts) x 2 seeds, matching on the whole shuffled deck in order, enemy roster and
  HP, opening intents, and player HP. This supersedes the old headline (one "ABCDEF"
  CorpseSlugsWeak capture) and the "single 2-enemy sample" caveat on enemy generation.
  See "Combat" below for the four defects that got it there.
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
- ✅ **Run generation is exact for BOTH act-1 acts, across 30 live captures.** Act,
  encounter sequences, boss, the whole map _and its edges_ match on every seed swept so
  far, Overgrowth and Underdocks alike. Underdocks had been modelled entirely from
  decompiled source and never observed; it needed nothing fixed. The **map** did — three
  defects that the three hand-picked fixtures all missed and a batch sweep found in its
  first 16 seeds (see "Sweeping seeds").
- ✅ **Act-1 selection verified 88/88 on the installed build, 43 of them Underdocks**,
  by replaying the profile's own run history (`scripts/verify_act_selection.py`). A
  single capture can never do better than 50/50 on a coin flip; this is the sample that
  makes the roll a result rather than a guess.
- **Seed derivation matches the game** (test `RunRngSet_DerivesGameSeedForStringSeed`:
  `RunRngSet("ABCDEF").Seed == 3334281563u`) — and seeds are now **canonicalized first**,
  as `StartRunLobby` does, so lowercase or an `I`/`O` no longer derives a different run
  than the game would.
- **Enemy HP now rolled faithfully** (was hardcoded `fixedHp`; now uses the game's
  Niche stream + unique-HP set — commit `123fecf`).
- ✅ **Every Ironclad card — all 92 — has tests**, written against `decompiled/` with the
  source cited per file, never against emulator output. `CardCoverageTests` is the guard:
  `scripts/generate_card_coverage.py` scrapes the `case` labels out of `CardEffects.Apply`
  into `ImplementedCards.g.cs`, and implementing a card now fails the build until it is
  tested or explicitly deferred in `Pending` (**389 left**).
  Caveat worth knowing: the guard only sees cards with their own `case`. Strike, Defend
  and Giant Rock run on the generic damage-and-block path and were invisible to it — they
  have tests now, but an empty `Pending` still means "every card with effect code", not
  "every card".
- ✅ **Per-card ground truth from the running game.** `scripts/capture_card.py` stages one
  card (plus powers, plus energy) in a live combat, plays it, and commits the before/after;
  `scripts/generate_card_capture_tests.py` renders those into `Cards/CardCaptures.g.cs`.
  Eight captures so far. Most confirm what the decompiled source already said; one
  answered a question the source **cannot**: `Hook.ModifyDamage` carries a `decimal` with
  no rounding step anywhere, so what happens to a fractional multiplier is unknowable from
  the code — the game truncates (6 x 1.75 = 10, not 11).
- ✅ **The three combat RNG streams are separate, as the game keeps them.** Target choice
  is `combat_targets` (`CombatState.TargetRng`), picking an existing card to act on is
  `combat_card_selection`, rolling up a new card is `combat_card_generation`. All three
  used to draw from the combat rng, which desynchronises the stream for everything
  downstream — the same failure `AiRng` was made to fix. Stampede is the trap: it picks a
  card from hand like Thrash does but reads `Rng.Shuffle`. Check the decompiled effect for
  which `Rng.*` it reads; do not infer from what the effect looks like.
- ✅ **47 relics have combat effects and 9 more act only between combats, chosen by mechanic rather than by act.** Nearly every
  relic can appear in nearly any act, so "act 1 relics first" is not a useful axis; the set
  instead covers each hook archetype a policy has to reason about — combat-start buffs,
  turn-scheduled effects, per-card-played counters (Shuriken, Kunai, Ornamental Fan, Letter
  Opener, Nunchaku, Permafrost, Mummified Hand), damage-triggered draws and block (Centennial
  Puzzle, Self-Forming Clay), HP thresholds, and the +1 energy family with its downsides
  (Ectoplasm's gold, Sozu's potions, Velvet Choker's six-card turn, Spiked Gauntlets'
  pricier Powers, Philosopher's Stone's stronger enemies, Blessed Antler's Dazed) — which
  meant giving gold gain and potion acquisition single chokepoints to hang a relic off.
  The last batch added the relics that read the previous turn (Art of War, Pocketwatch),
  the turn-end ones (Cloak Clasp, Screaming Flagon, Stone Calendar, Parrying Shield), the
  rest of the per-card-played family (Kusarigama, Tuning Fork, Ivory Tile) and the two
  between-combat Commons (Meal Ticket, Regal Pillow). The run-level set covers the deck
  (War Paint, Whetstone, and the three eggs), the reward screen (White Beast Statue), the
  shop (Membership Card), the rest site (Tiny Mailbox) and death itself (Lizard Tail), and
  it needed three more chokepoints: every deck addition now runs through
  `RunNonCombatEffects.AddCardToDeck`, the potion roll keeps drawing when a relic forces
  the reward, and `RunState.UsedUpRelics` carries a spent one-per-run relic across the
  combat boundary that rebuilds relic state from ids.
  Of the 298 decompiled relics, **146 have an
  in-combat hook**; the rest are run-level (rewards, shops, rest sites). Counting by rarity,
  the in-combat ones are 20 Common, 25 Uncommon, 29 Rare, 11 Shop, 18 Event, 35 Ancient
  (boss) and 8 Starter. Energy relics (`ModifyMaxEnergy`) are almost entirely Ancient/boss
  and are the biggest remaining gap.

- ✅ **Card selection is a real decision, not an omission.** Headbutt, upgraded True Grit,
  Burning Pact and Brand raise a `CombatState.PendingSelection`; while it is open
  `ValidActions` offers only the candidates and `Step` reads an action as the answer. The
  observation carries the open choice, which is what took the **native API to v17 and the
  run API to v10** — rebuild the dylib and expect any policy trained on the old 164-wide
  observation to be invalid. Auto-played and nested cards (Havoc, Hellraiser, Stampede)
  still resolve their choice automatically; the engine cannot hand a selection back from
  inside a queue it is draining.

## Key facts / mental model (save yourself the rediscovery)

- **Seeds:** the game's _input_ seed is a string (e.g. `"ABCDEF"`); it derives a uint
  _gen seed_ (`"ABCDEF"→3334281563`, `"0"→3452614542`). The emulator's `RunRngSet(str)`
  reproduces this. **Seedless standard runs use a RANDOM seed each time** — use **custom
  mode** for a chosen seed.
- **The direct combat env ↔ game seed bridge:** `Sts2CombatEnv(int seed)` sets its per-
  stream RNGs to `GameRng(seed, "<stream>")`. So passing the game's _derived gen seed_
  (3334281563) makes the combat env use the same streams as the `"ABCDEF"` run. That's
  why the enemy match works.
- **Named RNG streams** (`RunRngSet`): `Niche` (enemy HP, `SetUniqueMonsterHpValue`),
  `Shuffle` (deck), `CombatCardGeneration`, `MonsterAi`, etc. Each is
  `GameRng(seed, "<name>")`. The emulator run env (`RunEngine`) advances each to the
  game's CallCount; the **direct combat env assumes fresh streams** (CallCount 0) — fine
  for first-combat, a limitation otherwise.
- **Weak encounter variants:** `completed_combat_rooms` in `[0,3)` selects the weak
  variant (e.g. CorpseSlugsWeak = 2 slugs vs Normal = 3). Wired through
  `Sts2CombatEnv(..., completed_combat_rooms=)` and `Sts2_ResetEncounterWeak` (added at
  native API v11). Version numbers quoted through this file are the version a feature
  _landed at_, not the current one — the current pair is whatever
  `NativeExports.NATIVE_API_VERSION` and `RunNativeExports.RUN_NATIVE_API_VERSION` say,
  with `src/sts2_gym/native.py` pinned to match (v17 and v10 at the time of writing).
  A mismatch fails loudly on load rather than misreading the observation.
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
  have neither) but they'd be _unknown cards_, not merely misordered, if ever drawn.
- ✅ **Embark crash — FIXED. It was our harness tearing the run down mid-entry.**
  Left here in full because two earlier diagnoses in this file were wrong, and the way
  it was settled is the point: read the log, do not theorise.
  `NGame.StartNewSingleplayerRun` is **async**. It generates the run, writes
  `current_run.save`, and only _then_ awaits `RunManager.EnterAct -> EnterRoomInternal`,
  which preloads the room's assets. The mod reports a non-menu state as soon as the run
  state exists — i.e. in the middle of that tail. The harness took that as "done" and
  moved on to the next seed, whose first act is `return_to_main_menu`. Every crash log
  shows the same order:

  ```
  Embarking on a CUSTOM IRONCLAD run ... Seed: X
  Wrote 44101 bytes to ... current_run.save
  Preloading 'Event Room' assets... count=2
  [Startup] Time to main menu            <-- our save-and-quit lands HERE
  Preloading 'Event Room' Complete: 389ms
  [ERROR] Exception starting custom singleplayer run : NullReferenceException
             at RunManager.EnterRoomInternal
  ```

  A _successful_ embark has `Complete` before the quit. That is the whole bug: the NRE
  is the in-flight entry touching state we had just deleted.
  **The fix** is `wait_for_run` in `start_real_game_run.py` waiting for the game's own
  completion signal. The mod now counts `RunManager.RoomEntered` events — fired as the
  very last statement of `EnterRoomInternal` — and reports `rooms_entered` in every
  state payload; callers read it before embarking and wait for it to advance. **34
  consecutive embarks with retries disabled, zero crashes**, against ~1-in-5 before.
  ⚠️ **Requires the current STS2MCP build** — reinstall the mod (and restart the game)
  or the harness silently falls back to a weaker proxy.
  **Two things this was NOT**, both measured rather than argued:
  - _Not_ `NonInteractiveMode`. The theory was good — the crashing line is guarded by
    it, and MegaCrit's own AutoSlayer sets the hook we did not — so the mod grew a
    `set_non_interactive` action and it was A/B'd: **2 crashes in 12 embarks with it on,
    the same rate as off.** The action was removed again. (The audit stands, if it is
    ever wanted for speed: all ~30 `IsActive` sites are audio, animation waits or
    pause/unpause — none touch RNG, card effects or rules.)
  - _Not_ the lobby "reporting ready before it is", which is what the timeout message
    ("Timed out waiting for menu screen 'main'") made it look like. That message was a
    downstream symptom: the game was sitting on the un-dismissable `report_bug` popup
    from a crash one seed earlier.
    A capture taken before the crash is still valid — the save is written first — so old
    crashed-embark fixtures remain good ground truth.

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
  NonInteractiveMode theory; but it does mean a mod-driven embark is _not_ doomed, and it
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
  A _single_ clean sequence with generous `time.sleep` waits is stable. **Follow-on:**
  add settled-state guards to the debug/menu actions before any unattended sweep.
- **macOS NativeAOT exports:** the csproj uses `[UnmanagedCallersOnly(EntryPoint=...)]`
  (cross-platform) — we removed the original Windows-only `/EXPORT:` linker args. If you
  add a native export, add its `[UnmanagedCallersOnly(EntryPoint="...")]` and a matching
  `native.py` binding; bump `NATIVE_API_VERSION` + `_REQUIRED_NATIVE_API_VERSION` together.
- The harness (`scripts/validate_real_game_trace.py`) depends on `debug_start_encounter`
  etc. — those are **our mod additions**, not in any public STS2MCP. Keep the fork.
- `start_replay` (for clean full-run capture) is **not built** — investigated; it's a
  self-contained mod action to add (see docs/replay-verification.md), NOT a RunReplays dep.

## The run layer is now honestly unverified

Act-1 combat is verified end to end. The run layer around it was not — it was *fitted*.
Removed in one pass: **1,530 lines** across `RunEngine`, `RunMapGenerator`,
`RunRewardGenerator` and `RunNonCombatEffects` whose only job was to make two captured
runs replay cleanly. `ApplyRetainedTrace*`, `TryGenerateRetainedTraceCombatRewards`,
`ApplyRetainedTraceCardReward`, `ApplyRetainedTraceShop`, `TryEnterRetainedInstant5Event`,
plus 36 seed-keyed and 19 state-fingerprint inline blocks. They forced player HP and gold,
enemy HP, map coordinates, act transitions, treasure gold and the three offered cards; five
of them declared a combat won outright, and one converted a loss into a non-loss.

What that means, stated plainly:

- **The suites never covered any of it.** All 841 C# and 195 Python tests pass with every
  line removed, and the two seeds it keyed on (`7MS1YN8NWB`, `FKSYQMYRRV`) appear nowhere
  in `scripts/`, `tests/` or the docs — the traces they were fitted to are not in the repo.
- **Some of it fired in ordinary runs.** The seed-gated blocks were inert for everyone
  else, but the fingerprint ones — `Floor == 5 && PlayerHp == 74 && Gold == 120` — key on
  state alone. Random play reaches floors 2-8, so a trained agent sits squarely in range.
- **Behaviour is otherwise unchanged.** 40 random-but-legal act-1 runs give byte-identical
  results before and after: same endings, same median length, same per-phase step counts.
  That is evidence the removal was safe, NOT evidence the fingerprints never fired — those
  40 runs simply never matched one.

So the run layer's end-to-end status went from "green, dishonestly" to "unmeasured,
honestly".

### The first honest measurement

Two fresh traces are committed under `tests/fixtures/run_trace/` (A0 and A8, seed
QS2GYXRKWN, ~115 steps each, floors 1-10, covering monster, rewards, map, event,
card_reward, shop, rest_site, treasure and card_select). Replayed through the cheat-free
engine, the A8 one reports:

    step 23 field state_type: reference='rewards' emulator='monster'
    step 23 field player.hp: reference=59 emulator=53
    step 28: emulator trace ended before reference boundary

**Steps 0-22 match exactly** — deck, HP, gold, enemies, intents and phases through a Neow
event, a floor-1 combat, its rewards, a map move and most of a floor-2 combat. The single
divergence: the player's card at step 22 kills the Fuzzy Wurm Crawler live (7 HP left) and
the run moves to `rewards` with HP 53 -> 59, which is Burning Blood's end-of-combat heal.
The emulator is still in `monster` at 53, so it never reaches the reward path. Burning
Blood's heal IS modelled (`RunRewardGenerator.cs:363`), so the question is why that combat
did not end — reproduce at steps 22-23 of `QS2GYXRKWN-a8.json`.

Worth noting what this catches that the combat sweep cannot: the sweep enters a combat
directly with `debug_start_encounter` and a starter deck, while here the combat is reached
through the run, with the deck, relics and stream positions the run actually produced.

**That reading was too generous, and the cause is 22 steps earlier.** "Steps 0-22 match"
described the replay's five boundary fields — `state_type`, `run.floor`, `player.hp`,
`player.max_hp`, `player.gold`, compared only at phase transitions. The trace records far
more than that, so the replay now also reports the first step at which each of a wider
set parts company:

    first divergence in state_type at step 1:  reference='rewards'  emulator='map'
    first divergence in player.hand at step 7: reference=[…, ASCENDERS_BANE, …, COLLISION_COURSE]
                                               emulator=[…, DEFEND, …, PILLAGE]
    first divergence in battle.enemies at 12:  reference=[(33, 0)]  emulator=[(41, 0)]

The real defect is at step 1: **Neow's Kaleidoscope bonus is not modelled as card
rewards.** The live game answers "obtain 2 card rewards from other characters" with two
card-reward screens — Calcify/Prepared/Skim, then Acrobatics/Collision Course/Boost Away —
and the player picks one from each. The emulator goes straight to the map, having added
three cards silently: its deck is 14 where the run took 11 + 2, and it holds a Pillage the
game never offered. Everything after is downstream — a different deck draws different
hands, so the floor-2 combat plays out differently and does not end when the game's does.

This is also the first bug the answer-key removal exposed: `ApplyRetainedTraceCardReward`
used to overwrite `RewardCards` with fixed ids, which is exactly the screen involved.

### And the cause of THAT was one character

Kaleidoscope was never reached, because **Neow offered the wrong three relics — in every
run ever generated**. `EventModel` seeds each event with
`Seed + (IsShared ? 0 : GetPlayerSlotIndex(Owner)) + hash(Id.Entry)`, and a solo run's
only player is slot 0; `RunRngSet.NeowRng` defaulted that term to 1. A different stream
gives a different first roll, so every option was wrong from the very first decision of
the run. With `netId = 0`, seed QS2GYXRKWN offers Kaleidoscope, Nutritious Oyster and
Neow's Bones — matching the capture exactly.

Kaleidoscope itself is now modelled as what it is: **two card rewards from other
characters**, each offering three cards drawn one per other-character pool, the pools
shuffled on the Niche stream. It used to add two random *Ironclad* cards straight to the
deck and burn 18 Rewards RNG calls to keep the stream roughly aligned. That needed the
character card pools, which nothing had: a `CardDef` says nothing about whose card it is,
so `extract_data.py` now emits `CardPools.g.cs` (Ironclad 87, Silent 88, Defect 87,
Necrobinder 88, Regent 88, Colorless 64) from the game's `CardPoolModel` declarations.

The replay moved accordingly — it used to stop dead at step 24, and now runs past step 50:

| field | first divergence before | after |
| --- | --- | --- |
| `player.hand` | step 7 (no Ascender's Bane, a Pillage the game never offered) | step 7, one card of three |
| `battle.enemies` | step 12 | step 32 |
| `player.hp` | step 23 | step 46 |
| `player.gold` | — | step 24 |

#### Chasing the offered cards

The pools were right from the start — both rewards pick Necrobinder/Silent/Defect then
Silent/Regent/Defect, exactly as the capture does, so the Niche shuffle is correct. The
cards within them were not, and two real defects came out of it:

- **`CardRarityById` was a hand-written table of 144 ids that defaulted to Common.** Built
  from the Ironclad pool, it agreed with the extracted data on every id it carried — but
  **249 Uncommon and Rare cards were missing from it** and so read as Common, which let a
  Common roll hand back a Rare. Kaleidoscope draws from other characters' pools and hit it
  on nearly every card. `CardDef` has carried the real rarity since the extractor was
  written; `RarityOf` now reads it.
- **A non-encounter card rolls its rarity differently.** `CardFactory.RollForRarity` only
  takes `Roll` — which reads and grows the running rare-chance offset — when the source is
  an ENCOUNTER. Everything else takes `RollWithBaseOdds`, which ignores the offset AND
  compares against the *flat* uncommon odds rather than rare + uncommon, so its uncommon
  band is narrower. Kaleidoscope creates with `CardCreationSource.Other`.

**Both rewards now match the capture card for card** — Calcify, Prepared, Skim, then
Acrobatics, Collision Course, Boost Away. Two more defects had to go first, and the way in
was to stop reasoning about it: compute what stream values the live picks REQUIRE (a card
at index 14 of 20 needs a draw in [0.70, 0.75)), then search the emulator's stream for an
alignment that satisfies all twelve constraints at once. Six near-binary rarities can
agree by luck; six pick indices cannot.

- **The Rewards stream was seeded off by one, exactly as Neow was.** `Player.cs` seeds
  `PlayerRngSet` with `hash(seed) + RunState.GetPlayerSlotIndex(this)`, and a solo run's
  only player is slot 0; `PlayerRngSet` defaulted it to 1. That is every card reward,
  every shop and every transformation in the run, off the wrong stream.
- **A card costs THREE draws, not two.** Rarity, the card, and an upgrade roll — the
  alignment only matched at stride 3. The Kaleidoscope path was not rolling the upgrade,
  so it left the stream a call short per card and everything after read the wrong values.
- **Basic cards were being counted into the Common list.** `RarityOf` mapped Basic to
  Common to preserve the old table's default, which put each pool's starter Strike and
  Defend into every Common reward list — 21 candidates where the game has 20, so the same
  draw landed one card further along. The game compares the true rarity, and Basic simply
  never matches.

**The game logs every offset-based rarity roll** —
`Card rarity: Rolled 0.4538, need < -0.02 for rare (offset = -0.05)` in
`~/Library/Application Support/SlayTheSpire2/logs/godot.log` — which is the fastest way to
settle a rarity question. It logs nothing for `RollWithBaseOdds`, and one log holds runs at
several ascensions: the 0.03 rare / 0.01 growth lines are an A0 run, while A8 shows
0.0149 / 0.005, which is what the emulator uses.

With that, the replay runs to **step 61** — it stopped at 24 this morning — and `player.hand`
and `battle.enemies` no longer diverge anywhere in it. The deck, every hand dealt from it
and every combat fought with it now track the live run through floor 6. What remains:

    first divergence in state_type at step 1:  reference='rewards' emulator='card_reward'
    first divergence in player.hp   at step 60: reference=55  emulator=48
    first divergence in player.gold at step 60: reference=126 emulator=178
    Replay stopped: step 61: unsupported action 'select_card' while emulator phase is 7

#### Screens the emulator was skipping

`state_type` at step 1 was the `RewardsCmd.OfferCustom` wrapper screen, and chasing it
turned up three places where the emulator resolved a screen the player actually answers:

- **Kaleidoscope's two rewards go on the rewards screen at once.** The player claims one,
  picks a card, lands back on the screen, claims the other. The emulator opened the first
  card reward directly. Pending card rewards are now a count rather than a bool, all the
  way out through the observation, so the screen can hold two.
- **Neow stays on screen afterwards.** Its rewards answered, the game returns to the
  ancient with nothing but "Proceed" and waits for it. The emulator went to the map.
- **A rewards screen stays open even when empty.** After the last item is claimed the game
  keeps it up until the player proceeds; the emulator advanced on its own. That one is
  per-combat, not per-run.

The first `state_type` divergence is now **step 29**, and the sequence from Neow through
floor 2's combat, its rewards and the map move matches exactly.

Left at the end of the run: the gold gap at step 60 is 52 in the emulator's favour, the
stop is an event asking for a card the Event phase does not accept, and at step 29 the
live capture takes TWO `proceed` actions to leave a shop where the emulator takes one —
worth checking against the game before modelling, since it may be the tracer rather than
the shop.

Two things the capture turned up on the way:

- **The run layer has no ascension.** `RunEngine.Reset` hardcodes `PlayerHp = 64`, so an
  A0 capture diverges at step 0 on starting HP and every combat after it. The A0 trace is
  committed anyway — it is ground truth and costs a run to re-take — but it cannot be
  replayed until the run layer takes an ascension the way the combat layer does.
- **The auto-player stops at floor 10** with "No proceed button available or enabled" on a
  treasure screen. Not an emulator issue; the tracer's proceed handling for that screen
  needs the same treatment the map move just got.

## Next work (prioritized, with pointers)

**Combat start and run generation are both bit-exact** (see "what's proven"). The open
front is now _per-card correctness_, and it is far larger than the guard used to report.
The game has **five** characters; the emulator has id-constant classes for three of them,
and Defect, Necrobinder and Regent are implemented by name in `ApplyDefectCard` and its
siblings. The guard only scraped the constant cases, so it claimed 235 implemented cards
when the real number is 552.

| pool        | cards | implemented | tested |
| ----------- | ----: | ----------: | -----: |
| Ironclad    |    87 |          85 |     86 |
| Colourless  |    64 |          64 |     64 |
| Silent      |    88 |          88 |     10 |
| Defect      |    88 |          87 |      0 |
| Necrobinder |    88 |          88 |      0 |
| Regent      |    88 |          88 |      0 |
| Event       |    27 |          27 |      1 |
| Token       |    14 |          11 |      1 |
| Curse       |    18 |           3 |      1 |
| Status      |    12 |          10 |      2 |

(Ironclad's counts differ by pool vs id class — the pool excludes a few cards the class
carries, and vice versa; it is tested end to end either way.)

Three whole characters have never had a card verified. Every batch written so far turned
up real defects — thirteen across Ironclad alone — and the per-character routines have had
far less scrutiny than `Apply` did, so expect the yield to be higher there, not lower.

Ironclad and Colourless are both fully covered, and **Ironclad has no approximations
left** — Rampage's per-copy growth, Battle Trance's NoDrawPower and Howl From Beyond's
replay out of the exhaust pile are modelled, and Primal Force's IsTransformable filter
turned out to exclude nothing in combat.

- ⚠️ **Combats now have their own test suite, and the first three encounters found three
  defects.** `Combats\<Encounter>Tests.cs` plus `CombatCoverageTests` mirrors the card
  setup: 87 encounters modelled, **3 tested, 84 pending**. All 42 act-1 encounters (both
  act-1 variants) have rosters and intents; what was missing is anything checking them.
  Walking five turns of Haunted Ship found that its move machine was transcribed as
  `MoveIndex % 3`, so the opening HAUNT came round every third turn when the game enters it
  once and then alternates SWIPE and STOMP forever; that its Swipe and Stomp both used the
  A9 damage branch at A8 (13/12, not 14/15); and that STOMP landed as one hit instead of
  three. Enemy HP was ascension-blind as well — the extractor kept only the A8+ branch, so
  `EnemyDef.HpBand` and both branches are extracted now. **Expect more of this**: 84
  encounters have never been walked past their opening state in C#, and the Python
  live-fixture suite only replays the six that have committed captures.
  Eleven encounters in, the count is **twenty defects**, and the largest group is one
  class: `CombatFactory`'s opening intents were converted to `Ascension.Value` years ago,
  but **`EnemyAI`'s per-turn intents never were** — eleven act-1 enemies were dealing their
  A9 damage at A8 (Inklet, Flyconid, Cubex Construct, Snapping Jaxfruit, Slithering
  Strangler, Fogmog, Living Fog, Gremlin Merc, Sneaky Gremlin, Two-Tailed Rat, Punch
  Construct), on top of both Cultists. A sweep of every monster's `GetValueIfAscension`
  pairs against `EnemyAI` flags **134 more suspect literals**, most of them act 2. Worth
  knowing while reading `CombatFactory`: `ChooseIntents` overwrites every enemy's intent
  immediately after the roster is built, so the opening-intent literals there are
  placeholders — `moveIndex` is what actually selects the opening move.
  The earlier count of **eight defects**: Haunted Ship's cycle, its two
  ascension branches and its single-hit Stomp; Vine Shambler's single-hit Swipe and a
  Tangled that was cleared at the start of the player turn instead of the end, so the
  debuff never taxed a card; both Cultists' Dark Strike and Damp's Ritual on the Deadly
  branch at A8. Plus one parity gap that is not an encounter bug at all: **the observation
  announced an attack's raw move damage**, where `AttackIntent.GetSingleDamage` runs it
  through `Hook.ModifyDamage` first — so a Ritual-stacking cultist told a policy it was
  hitting for nine on the turn it hit for fifteen.

- ✅ **Multiplayer-only cards can no longer be offered.** `CardFactory.FilterForPlayerCount`
  drops every `MultiplayerOnly` card from a pool before anything is rolled from it, and
  `IRunState.CardMultiplayerConstraint` reports `SingleplayerOnly` whenever the run has one
  player — which ours always does. The emulator's pools are copies of the character's full
  card pool and still list them, so the Ironclad reward pool was offering Tank and Demonic
  Shield and the Colourless pool ten more, Intercept, Knockdown and Tag Team among them.
  `CardDef.MultiplayerOnly` is now extracted (21 cards) and the filter is applied once, at
  the top of `ChooseCardWithRarity`, and again when transforming. Two things corroborate
  it: `RareIroncladSingleplayerPool`, built separately, contains none of them, and none of
  the 23 live-recorded reward cards is one. Worth knowing either way: **the card reward
  pools have never been checked against a live capture** — the recorded rewards are pinned
  per floor by `ApplyRetainedTraceCardReward`, which bypasses the pool entirely.

Every card that made a choice for the player now raises a real one (see Card Selection).
What remains are missing mechanics, all Colourless or Silent, and each is self-contained:

| card                | what is missing                                                     |
| ------------------- | ------------------------------------------------------------------- |
| Strangle            | StranglePower; Vulnerable 2 stands in                               |
| Hidden Gem          | replay counts on a draw-pile card                                   |
| Beat Down           | auto-plays Attacks from the discard pile                            |
| Catastrophe         | auto-plays cards off the draw pile                                  |
| Fisticuffs          | block should equal damage **dealt**, not printed damage             |
| Memento Mori        | scales per card discarded this turn; no discard counter exists      |
| Discovery, Jackpot  | roll from the emulator's own pool, not the character's unlocked one |

Intercept, Knockdown and Tag Team are gone from this list: all three are `MultiplayerOnly`
and can no longer be drawn from any pool, so their stand-in powers are unreachable in a
solo run. **Strangle is now the one to fix first**, being the only reachable one left that
changes a number rather than a flavour. Eleven Silent cards also carry an `approximation`
comment in `CardEffects` with no tests at all — those are unverified rather than merely
approximate, and The Gambit is the warning about what hides there.

Also open: the powers and relics that read `combat_card_selection` in the game
(Aggression, Improvement, Bookmark, Jewelled Mask, Power Cell, Drain) — several are not
modelled at all, and the modelled ones were out of scope when the stream was wired up.
Mummified Hand is modelled and reads `CombatState.CardSelectionRng`.

**The relics have no approximations left.** The six they carried were fixed rather than
documented, and each needed a piece of plumbing that did not exist:

- **Velvet Choker** now refuses auto-plays as `Hook.ShouldPlay` does, on both auto-play
  paths, and a refused card goes to its result pile without its effect running. The
  reachable cases are Stampede (fires at turn end, after the allowance is spent) and a
  queued Hellraiser play (the queue drains after the play that filled it was counted).
- **Philosopher's Stone** catches enemies that join mid-combat: all ten spawn sites go
  through `RelicEffects.Spawned`, the emulator's `AfterCreatureAddedToCombat`.
- **Centennial Puzzle** and **Self-Forming Clay** answer damage a card deals the player,
  not just enemy attacks — `Hook.AfterDamageReceived` never asks who dealt it.
- **Ivory Tile** reads what the play actually cost. X cards are printed at zero and take
  the rest of the bar inside their own effect, so `CardDef.HasEnergyCostX` was extracted
  from the game's `CardModel.HasEnergyCostX` (10 cards) rather than inferred from a delta.
- **Tiny Mailbox** offers its two potions on the reward screen instead of forcing them
  into the belt, so the player can decline, or drop a held potion to make room. The screen
  carries one potion at a time, so the second is queued in `RunState.PendingRestPotions`
  and appears when the first is claimed; skipping abandons both, as skipping the screen
  does in the game.
- **Lizard Tail** revives on the hit that killed rather than at the end of the turn, so
  the rest of a multi-hit intent lands on the revived player.

The older run-generation notes below are kept because the _method_ is what matters, not
because that front is still open.

`scripts/verify_run_generation.py` compares the emulator against a live
`current_run.save` — plain JSON recording exactly what the game generated
(`acts[i].id`, `rooms.{normal,elite}_encounter_ids`, `boss_id`, `saved_map.points`).
**No need to drive the game**; just have a run saved, and note the crashed-embark save
is still a valid capture. Verified on **three seeds — now including an Underdocks act 1**:

| section           | "ABCDEF" (Overgrowth, A8) | "AAB" (Overgrowth, A8) | "UNS55LCMKP" (**Underdocks**, A8)    |
| ----------------- | ------------------------- | ---------------------- | ------------------------------------ |
| act               | PASS                      | PASS                   | PASS                                 |
| normal encounters | PASS 15/15                | PASS 15/15             | **PASS 15/15**                       |
| elite encounters  | PASS 15/15                | PASS 15/15             | **PASS 15/15**                       |
| boss              | PASS (TheKin)             | PASS (CeremonialBeast) | **PASS (WaterfallGiant)**            |
| map               | PASS (exact)              | **PASS (exact)**       | **PASS (exact, 64 nodes / 17 rows)** |

**Underdocks needed nothing fixed — every act-specific branch was already right.** Worth
knowing exactly what that capture cleared, because all of it was modelled from the
decompiled act and never observed: its four weak / ten normal / three elite / three boss
pools _and their order_ (declaration order, per `Acts/Underdocks.cs`), and — the piece
most likely to have been wrong — the up-front RNG burn in `RunMapGenerator`, which is
`202 + (underdocks ? 57 : 60)` calls. That 57 was _derived_, not measured: Underdocks
declares 3 fewer act events than Overgrowth (10 vs 13; +18 shared either way, matching
the live save's 31 `event_ids`), and everything else up front was assumed act-independent
even though Underdocks differs elsewhere (one `BgMusicOptions` entry vs two, its own
background dir). Off by one call and the entire encounter sequence and map would desync,
so the exact map match is strong evidence the whole burn is right.

### Sweeping seeds, now that a capture is cheap

Headless embarks turned a capture into ~40 unattended seconds, so run generation is now
verified in **batches** rather than one hand-picked seed at a time:

```bash
python scripts/capture_sweep.py --count 12            # random seeds, both acts
python scripts/capture_sweep.py --count 6 --act underdocks --save-fixtures
```

Per seed it abandons whatever run exists, embarks at A8, reads `current_run.save` and
compares every section. Unattended means unattended: the embark crash that used to hit
~1 seed in 5 was our own race and is fixed (see the gotcha above). **This immediately paid for itself: 3 of the first 16 sweep
captures (19%) failed on the map**, in ways the three hand-picked fixtures had all
missed. All three are fixed (below); the current build is **30/30 captures matching in
every section**, and the last two runs were 10/10 and 12/12 with no capture failures.

**Three map defects the sweep found — all of them "the emulator does slightly more than
the game does":**

- **Post-processing moved the ancient and boss nodes.** The game's `CenterGrid`,
  `SpreadAdjacentMapPoints` and `StraightenPaths` all take `Grid`, which is _only_ the
  path rows: `StandardActMap` holds `StartingMapPoint` / `BossMapPoint` separately, in
  the middle column, and the boss's row is one past the grid's last. So a centered map
  never drags them along — which is also why the save writes `start` and `boss` outside
  `points`. The emulator carried them as ordinary nodes, so a map with two empty left
  columns shifted start and boss to column 2 while the game kept them at 3.
- **Edge-breaking stopped after the first break.**
  `MapPathPruning.BreakAParentChildRelationshipInSegment` walks the whole segment and
  breaks _every_ qualifying parent→child link, setting a flag; the emulator returned on
  the first one. The re-scan that follows then sees a different graph, so the rest may
  never be broken at all — leaving an edge the game had pruned, which pinned a node to
  the only column both its children allowed.
- **The ancient counted as a parent in the prune guard.** `PruneSegment` skips a node
  when a parent has exactly one child _and is still in the grid_; the ancient is never
  in the grid, so the game ignores it there and the emulator did not.

**Map _edges_ are compared now, not just node positions** (native list 16, run API v9).
The same dots can be wired differently, the save records each point's `children`, and
connectivity is what constrains the post-processing passes — it was free ground truth we
were throwing away. It is what identified the second defect above.

**Seeds are canonicalized like the game does.** `SeedHelper.CanonicalizeSeed` uppercases
and folds `O`→`0`, `I`→`1` (its alphabet has neither letter), and
`StartRunLobby.BeginRunLocally` runs every chosen seed through it before hashing. The
emulator hashed the string as typed, so `"abcdef"` or any seed containing I or O derived
a different gen seed than the live run it was meant to reproduce — a silent, total
divergence. Fixed in `RunRngSet` and `sts2_gym.game_seed`; the sweep hit it within
seconds by generating a seed with an `I` in it.

**The second seed earned its keep — it caught three defects one sample could not:**

- **Act selection was wrong in mechanism and stream.** It was `NextBool()` on the
  _unnamed_ raw-seed stream. The game uses `rng.NextItem` over the unlocked acts for
  that index on a dedicated **`"act_selection"`** stream
  (`StartRunLobby.BeginRunLocally`). "ABCDEF" passed by luck — with a two-way roll a
  single sample cannot distinguish a correct model from a coin flip.
  ⚠️ **Not seed-pure**: the candidate list is whatever the _profile_ has unlocked, and
  the game force-selects an unlocked-but-undiscovered alt act instead of rolling. We
  model the mature-profile case. Same caveat as boss discovery.
- **`NibbitsNormal` was wrongly tagged `Nibbit`.** Only `NibbitsWeak` declares that tag;
  `NibbitsNormal` overrides nothing and inherits the empty default. The bogus tag made
  the no-repeat rule block the game's legitimate `NibbitsWeak -> NibbitsNormal` run,
  shifting the whole remaining sequence by one (2/15 -> 15/15 once fixed).
- **The map stream was keyed on act _identity_, not act _index_** (`state.Act - 1`).
  An Underdocks act 1 would have read `"act_2_map"` and desynced the entire map. The
  index for act 1 is always 0.
- Two more invented names corrected: `Nibbit` -> `NibbitsWeak`, `Nibbits` ->
  `NibbitsNormal` (old Python encounter strings still resolve as aliases).

**Run generation is now exact on both captured seeds.** The last residual — "AAB" row 1
holding a node at the wrong column — was **edge insertion order**. The game wires the
start node's children with `ForEachInRow`, which walks grid columns 0..6; we wired them
in _path-draw_ order (the order the 7 starts were rolled). That insertion order becomes
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
- `tests/fixtures/run_generation/UNS55LCMKP.json`, `HEADLESS1.json` — the same, for
  **Underdocks** act 1s. Keep at least one per act: act 1 is a coin flip and the two
  acts run down different branches. A test asserts the committed set covers both.
- `tests/fixtures/run_generation/4MW6NTLDWU.json`, `L4CEF9U55L.json` — the two sweep
  captures that caught the map defects (off-centre start/boss, and an unpruned edge).
  They exist to keep those fixed; delete them and nothing else in the suite notices.
- `tests/fixtures/act_selection/v0.107.1.json` — 88 (seed -> rolled act) pairs, 43 of
  them Underdocks, distilled from the profile's own run history (see below). Seeds and
  acts only, no account id or timestamps.
- `tests/fixtures/combat/*.json` — combat starts. Six captures from `combat_sweep.py
--save-fixtures` (both acts, both pools, and the three encounters that roll their own
  composition), each carrying a `capture` block with the seed, encounter, weak/normal
  context and TotalFloor needed to rebuild the emulator side offline. Plus the original
  `ABCDEF-corpse-slugs.json`, which predates that block and is checked the old way —
  its run's save is long gone, so it cannot be re-captured.
- `tests/python/test_live_fixtures.py` runs the **real comparison code** against them,
  so the full structure is checked rather than a hand-transcribed subset. Every fixture
  in the directory gets a test class built for it automatically — drop a capture in and
  it is checked, with no test to remember to write. `scripts/generate_capture_tests.py`
  does the same for the C# side.
- Every fixture is checked for two preconditions rather than trusting them: the **game
  version stamp** (all fixtures must agree, and `verify_run_generation.py` shouts when
  the installed game has moved on) and the **profile facts** — act selection and boss
  discovery read the profile, so a capture from a fresher account is not comparable.
  The boss check reads _the captured act's own_ `BossDiscoveryOrder`.

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
not a regression. A record is ground truth for _its own_ patch, and early runs also hit
`GetRandomList`'s force-select path (an unlocked-but-undiscovered alt act is taken
instead of rolled) rather than rolling at all. Only the installed build's rows are a
statement about the emulator as it stands.

The history records carry no rooms and no map, so this verifies **act selection only** —
the rest still needs a `current_run.save` capture per act.

## Combat (started — `scripts/combat_sweep.py`)

Same headless loop as the run-generation sweep, pointed at the other half of the
emulator. Per (seed, encounter) it embarks a fresh A8 run, jumps straight in with
`debug_start_encounter`, and compares four things: the **deck** (hand + draw pile in
order), **enemies** (count/HP), **intent** (each enemy's opening move) and **player** HP.

```bash
python scripts/combat_sweep.py --count 2                    # both pools, both acts
python scripts/combat_sweep.py --pool normal --count 2
python scripts/combat_sweep.py --act underdocks --count 4
```

**Normal-pool encounters do NOT need three easy fights behind them.** The
`completed_combat_rooms in [0,3)` rule only decides which variant the _map_ hands you;
`debug_start_encounter` looks the encounter up by class name and enters a `CombatRoom`
for it directly, so `NibbitsNormal` works on floor 1 of a fresh run. The emulator matches
with `completed_combat_rooms = -1`. Verified: `nibbits`, `punch-construct`, `sewer-clam`,
`fossil-stalker`, `mawler`, `vine-shambler` and `haunted-ship` all captured that way.
The real constraint is **stream freshness** — the direct combat env assumes every named
RNG stream is at CallCount 0, which is true of a run's _first_ combat whichever variant
it is. So: never answer Neow, never enter a room, one fresh run per capture.

**First run: 3/16. Now 32/32** across both pools and both acts. What it found:

- ✅ **The deck is exact** — the whole shuffled deck in order, every capture. Previously
  one committed sample; now the best-attested thing in combat.
- ✅ **Enemy HP, opening intents and rosters are exact** on every encounter swept so far
  (8 weak + 8 normal, both acts).
- ⚠️ **Every enemy damage value had been one ascension too high.** The game bakes
  ascension into monster data via `AscensionHelper.GetValueIfAscension(level, high, low)`,
  and the enum's ordinal IS the level: `ToughEnemies = 8`, `DeadlyEnemies = 9`. **At A8
  the Tough branch is live and the Deadly branch is not** — but every intent was
  transcribed as a bare literal taking the Deadly (A9) value. HP matched (Tough) while
  attacks were 1-2 points high (Deadly), on 13 of the first 16 captures.
  `Core/Ascension.cs` models the rule; sites read
  `Ascension.Value(Ascension.DeadlyEnemies, 9, 8)`, which diffs by eye against the
  property it came from, and `ModelledLevel` is the one place that says A8.
  **Fixed for the swept enemies only** — CorpseSlug, Nibbit, Seapunk, Toadpole,
  SludgeSpinner, ShrinkerBeetle, FuzzyWurmCrawler, the four slimes, SewerClam,
  FossilStalker, Mawler, VineShambler. ~134 intent literals and 96 monster classes carry
  a DeadlyEnemies value, so **the unswept ones are presumed wrong**; sweeping an
  encounter is what proves one.
  ⚠️ **Intents live in `EnemyAI.SelectIntent`, not `CombatFactory`.** The starting intent
  passed to `CreateEnemy` is overwritten by `ChooseIntents` right after the encounter is
  built, so fixing the CombatFactory literal alone changes nothing. That cost an hour.
- ⚠️ **The per-encounter RNG was missing, and so was the AI stream.** Encounter models
  roll their own composition from `EncounterModel.Rng`, seeded
  `(uint)((int)runState.Rng.Seed + runState.TotalFloor + GetDeterministicHashCode(Id.Entry))`.
  Three things ride on it, all now modelled in `Core/Run/EncounterRng.cs`:
  - `SlimesWeak.GenerateMonsters` draws **three** times (small, the forced second small,
    then the medium). The emulator drew twice and inferred the second small "for free",
    which read the medium off the wrong draw and produced the wrong roster.
  - `SlimesNormal.GenerateMonsters` is a single `NextBool` for which small slime leads.
  - `CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves` rolls `NextInt(3)` once and
    deals consecutive starting moves. The old hardcoded `(2, 0)` is that sequence for a
    roll of 2 — right one time in three.
    And separately: `state.AiRng` was never wired in the direct combat env, so intent rolls
    fell back to the combat RNG. Invisible for the many enemies whose opening move is
    deterministic; wrong for every enemy that opens on a random branch (LeafSlimeS,
    SludgeSpinner, Exoskeleton). It is now `GameRng(seed, "monster_ai")`, and
    `CombatFactory` no longer stomps a caller-provided one.

**`TotalFloor` at a Neow jump is 1** — the ancient is the single map point in the run's
history. Measured, not assumed: floors 0 and 2 give the wrong slime roster on seeds where
1 reproduces the live one exactly. `Sts2CombatEnv(total_floor=)` passes it (native API
**v15**, `Sts2_ResetEncounterAtFloor`); leave it None and encounters that roll their
composition silently fall back to the combat rng.

### Fights, not just openings (`--turns`)

An opening-state check proves an enemy's _first_ move, which for a three-move enemy is a
third of it — and which move it opens on is itself a roll. `combat_sweep.py --turns N`
ends N turns with no cards played, comparing intents every turn, and reports **coverage**:
how many of each enemy's declared moves the fight actually reached.

```bash
python scripts/combat_sweep.py --seeds <SEED> --encounters corpse-slugs --turns 6
python scripts/enemy_moves.py CorpseSlug        # the denominator, read from decompiled/
```

`scripts/enemy_moves.py` parses `new MoveState(...)` out of the decompiled monster
classes and dedupes by intent expression — two MoveStates can be the same move reached
from different entry points (FuzzyWurmCrawler's FIRST_ACID_GOOP / ACID_GOOP), so counting
MoveStates would make 100% coverage unreachable. Coverage counts distinct
**(type, magnitude)** pairs, not types: WhipSlap and Glomp are both "Attack".

**Three defects turns caught that openings could not:**

- **Multi-hit attacks executed at their A9 per-hit damage** while announcing the correct
  A8 total. The per-hit number is written out _separately_ from the intent, in
  `ExecuteIntent`'s attack branch (`for (i < 3) DealAttackDamage(enemy, state, 4)`), so
  fixing the intent table left the damage wrong. Toadpole announced 9 and dealt 12. Fixed
  for Toadpole, Byrdonis, PhrogParasite, SkulkingColony.
- **Strength gains were A9 too** (Nibbit's Hiss gave 3, A8 is 2), which compounds every
  turn after the buff.
- **Seapunk's BubbleBurp** block/Strength were both wrong-tier.

### Ascension is an input, not a constant (A8 **and** A10)

Enemy data is ascension-dependent — `GetValueIfAscension(level, high, low)` — so the same
enemy hits for different amounts at different levels, and a suite captured at one level
only ever exercises one branch of every pair. **`CombatState.AscensionLevel` carries the
run's level** (native API **v16**, `Sts2CombatEnv(ascension=)`), captures record it, and
tests read it back, so A8 and A10 fixtures coexist in one process.

```bash
python scripts/combat_sweep.py --seeds <SEED> --ascension 10 --turns 6 --save-fixtures
```

What actually differs between A8 and A10, and nothing else does:

- **`DeadlyEnemies` (9)** flips nearly every monster damage and buff amount to its high
  value. This is the whole practical difference in combat.
- **`DoubleBoss` (10)** adds a second boss — but only to the _last_ act
  (`i == Acts.Count - 1` in `RunManager.GenerateRooms`), which act-1 generation never
  reaches. Verified in passing: an A10 capture's deck, enemy roster and HP all match an
  A8-modelled emulator, because HP comes from `ToughEnemies` (live at both).
- Player HP stays 64/80: the HP-affecting levels (TightBelt 4, AscendersBane 5) are both
  below 8.

Committed A10 captures for corpse-slugs and toadpoles, both with turn traces, both 3/3
coverage. Mutation-checked the way that matters here: corrupting only the **high** side
of `Ascension.Value(DeadlyEnemies, 9, 8)` fails the three `_a10_` tests and leaves every
`_a8_` test green.

✅ **Act 1 swept end to end: 41 encounters, 28 ALL MATCH, and 4 more correct on behaviour.**
The four — shrinker-beetle, shrinker-and-fuzzy, bygone-effigy, terror-eel — report
`turns:ok` and fail only `coverage`, which is not an emulator defect: the sweep plays no
cards, so the live fight ends before a declared move ever appears. Reaching those needs a
capture that fights back. **That leaves 9 genuinely wrong**, listed below.

Closed in this pass, each verified against the running game: inklets, cubex-construct,
fogmog, gremlin-merc, ruby-raiders, fossil-stalker, byrdonis, skulking-colony,
phantasmal-gardeners, ceremonial-beast, vantom, terror-eel's behaviour, and the Kin.

✅ **Act 1 is done: a full 41-encounter sweep reports NO behavioural failure.** 32 are
ALL MATCH and the other 9 fail only `coverage` — a property of a capture that plays no
cards, not an emulator defect (shrinker-beetle, shrinker-and-fuzzy, slithering-strangler,
bygone-effigy, two-tailed-rats, terror-eel, lagavulin-matriarch, waterfall-giant, and
fogmog's own coverage). Reaching those needs a capture that fights back; three seeds of
two-tailed-rats all end at turn 5 with the player dead and 3 of 4 moves seen.

Five defects closed this pass, four of them general rather than per-monster:

- **The mid-combat reshuffle sorted by the wrong key.** `CardPileCmd.Shuffle` is a
  `StableShuffle`: merge discard + draw, **sort by ModelId**, then Fisher-Yates. ModelId
  compares the slugified class name as an ordinal string, and the emulator sorted by its
  own numeric ids — right pile counts, wrong card on top, from the first reshuffle of
  every fight. `CardDef.Entry` now carries the slug, written by `extract_data.py`.
- **Statuses with `HasTurnEndInHandEffect` were half-modelled.** Infection (3) and Wither
  (3) burned nobody; Toxic damaged the player for *playing* it, when it has no OnPlay at
  all and paying 1 to exhaust it is how the damage is dodged.
- **`CardPilePosition.Random` rolls on `Rng.Shuffle`**, not the combat stream — Soul
  Fysh's Beckon was landing in the right pile on the wrong turn.
- **Weak, Frail and Vulnerable tick AFTER the enemy's turn**, not the player's: every one
  of them ticks in `AfterSideTurnEnd(side == Enemy)`. Ticking first cost the last turn of
  every such debuff — an enemy attacking into the player's final point of Vulnerable was
  hitting for 8 where the game hits for 12. A debuff applied to the PLAYER also skips one
  tick (`PowerCmd` sets `SkipNextDurationTick` for player-side debuffs); enemies get no
  such grace.
- **Only the Wriggler sets `StartStunned`.** The emulator stunned every summon, which cost
  each one an extra turn: the Gas Bomb went off late, a summoned rat stood through its
  first attack, and Fogmog's eye dealt its three Dazed a turn behind. The enemy phase
  already iterates a snapshot of the roster, so a newcomer misses the phase that made it
  without any stun at all. Summons also roll HP the way the game does now — Niche stream,
  excluding the MaxHp of the creatures already on that side — and a rat called for backup
  arrives at the FRONT of the roster, because the rats hold Slots[2..4] and CallForBackup
  takes the last free slot.

The sweep now compares **the hand, in order, every turn**, which is what caught the last
three of those; `test_every_turn_hand_matches` pins it offline against four committed
captures. All four mutations tried against it were caught.

### Closing the coverage-only encounters

The eight `coverage:FAIL` encounters are closed too, and only one of the four causes was
an emulator defect:

- **The coverage instrument counted moves no capture could reach.** A live intent type
  the harness does not map returns None and is dropped from the count, so `Sleep`,
  `Summon`, `Heal` and `DebuffStrong` each cost a monster a declared move permanently —
  that alone was Lagavulin Matriarch, Bygone Effigy, the Two-Tailed Rats and the Shrinker
  Beetle. `live_enemy_intent` now covers the game's whole IntentType enum and the sweep
  reports anything unmapped.
- **Some declared states are unreachable from the machine's initial state**, entered only
  by `CreatureCmd.Stun(owner, ..., stateId)` from a power when the monster is damaged past
  a threshold: Terror Eel's STUN/TERROR, Waterfall Giant's ABOUT_TO_BLOW/EXPLODE,
  Ceremonial Beast's second phase. `enemy_moves.py` walks the machine and drops what it
  cannot reach — and counts everything when it cannot parse one. It was also swallowing
  the declaration after any MoveState with an object initializer, which *understated*
  denominators.
- **`combat_sweep.py --play`** plays the first card the live game says is playable. A
  capture that fights back survives long enough to walk a boss's ring, which is what
  closed the Giant and the Matriarch. Two harness bugs surfaced doing it, both the same
  shape as the round-counter one: a posted action has to be waited for, or the live state
  read back is the state before it.
- **Constrict and Disintegration are blockable damage**, not HP loss — invisible until a
  capture holds block. That was the Slithering Strangler.

One emulator rule was wrong and is now right: the one-tick grace on a player debuff
belongs to the POWER, so it applies to a debuff newly created on the player and not to
one added to a stack already there. The rats screech nearly every turn and live's Frail
sits at 1; the emulator's was climbing, costing a point of block a turn.

### Reaching what only happens when the player is winning

Both of those are closed now, by stacking the deck: `combat_sweep.py --add-card BLUDGEON`
puts a card on top of BOTH hands before turn one — live through the mod's
`debug_add_card`, the emulator through a new `Sts2_DebugAddCardToHand` (native API v18).
Four Bludgeons kill a Phrog Parasite by turn five. The hand rather than the deck, because
`debug_add_card` needs combat in progress and adds at the top of a pile: hand-stacking
places the same card in the same slot on both sides with no shuffle to agree about.

Three defects fell out, none of which any passive capture could have reached:

- **The four Wrigglers do not act in step.** `INIT_MOVE` is a conditional branch on the
  creature's SLOT — wriggler1 and wriggler3 open on NASTY_BITE, wriggler2 and wriggler4
  on WRIGGLE — so half the pack always bites while the other half buys Strength. The
  emulator spawned them all on the same move. Note the parity is inverted at spawn:
  SPAWNED_MOVE burns an index before the branch is read.
- **WRIGGLE adds an Infection, not a Dazed** — the same status the parasite deals three
  of, and one that burns for 3 in hand at end of turn.
- **Terror Eel's second phase was not modelled at all.** `ShriekPower(75 at A8)` watches
  for an unblocked hit that leaves the eel at or below its threshold, then stuns it for a
  turn and queues TERROR_MOVE, which lands **Vulnerable 99** on the player before the eel
  returns to CRASH. Verified in lockstep: at 74 HP both sides stun, both then show the
  debuff, and both come back to CRASH announcing 24 (16 x 1.5).

Mid-combat spawns also rolled HP off the combat rng in `CombatEngine`'s own copy of
`CreateEnemy`; they now use the Niche stream with the roster exclusion, as `EnemyAI`'s
already did.

Two harness gaps closed alongside: the play policy waits for a posted play to register
(the earlier "live game never played the card"), and card-adding refuses to exceed the
game's ten-card hand instead of silently dropping the overflow.

Watch for **star costs** when picking a card to stack: Devastate is 1 energy for 30
damage but costs 4 stars, so live reports `can_play: false` and never plays it.

Both fights are committed as fixtures and checked with no game running:
`QS2GYXRKWN-phrog-parasite-a8-play.json` and `QS2GYXRKWN-terror-eel-a8-play.json`. A
`--play` capture is saved under its own `-play` name rather than replacing the passive
capture of the same fight, because the two prove different things — the passive one walks
the enemy's move table, the playing one reaches what only happens when the player is
winning. The fixture records the stacked cards and every action each turn took, and
`FightChecks` puts the cards back in the same slots and walks the action list. Four
mutations were tried against them — the Wrigglers' alternation, Wriggle's Infection, the
eel's TERROR move and its Shriek threshold — and all four were caught.

**The previous standing, before this pass: 30 ALL MATCH, 5 more correct on behaviour,
6 wrong.** Newly closed: the Kin, Vantom, jaxfruit-and-flyconid,
and Slithering Strangler (now `turns:ok`, coverage-only). The five behaviour-correct ones
are shrinker-beetle, shrinker-and-fuzzy, bygone-effigy, terror-eel and
slithering-strangler — all `turns:ok`, all failing only `coverage`, which is a property of
a capture that plays no cards.

All six of those are now closed. What they turned out to be, since none was what the
symptom suggested:

| encounter | what it actually was |
| --- | --- |
| living-fog | the bomb was stunned; the game never stuns a summon but the Wriggler |
| waterfall-giant | its ring never returned to STOMP, SIPHON gained Steam Eruption instead of healing, STOMP dropped its Weak, and PRESSURE_GUN's climb was unmodelled |
| lagavulin-matriarch | she walked the four-move ring while asleep, DISEMBOWEL was one hit of 20 instead of two of 10 (so her own SOUL_SIPHON Strength landed once, not twice), SLASH2 used its A9 damage, and SOUL_SIPHON did nothing at all |
| soul-fysh | its Beckon was placed off the combat stream instead of Rng.Shuffle |
| phrog-parasite | Infection dealt no damage at end of turn, and the reshuffle put it in hand on the wrong turn |
| slime-and-flyconid | the player's Vulnerable expired one enemy phase early |
| two-tailed-rats | rats all summoned at once, the newcomer went to the back of the roster with a duplicate HP and a stun, and a full pack still tried to summon |

The earlier list, before that pass:

| encounter | what is wrong |
| --- | --- |
| waterfall-giant | six of seven turns match; its moves stack SteamEruptionPower(3) and PRESSURE_GUN's damage is a lambda climbing by PressureGunIncrease each use — neither is modelled, so late announcements read low |
| lagavulin-matriarch, soul-fysh | boss growth of the same kind; soul-fysh announces every intent correctly and only its damage dealt diverges, by a constant 6 from turn three |
| phrog-parasite | a phase off from turn six, despite a machine that strictly alternates — something costs it a turn mid-fight |
| slithering-strangler, living-fog, two-tailed-rats, slime-and-flyconid, jaxfruit-and-flyconid | one branch roll differs mid-fight; rosters, openings and rules all match |

The earlier figure was **26 ALL MATCH** before the Kin and Vantom closed:
`combat_sweep.py --turns 6` now covers **all 42** encounters both act-1 acts declare —
checked against `GenerateAllEncounters()` in the decompiled act models (Overgrowth 22,
Underdocks 20), not against the sweep's own list. CorpseSlugsNormal was the last gap and
was never an emulator one: Corpse Slugs is a single encounter whose roster (2 slugs weak,
3 normal) comes from `completed_combat_rooms`, so the normal variant only needed a name
to be swept under. It matched on both seeds first time. Elites and
bosses had never been checked at all — they were reachable the whole time, the sweep list
simply stopped at the normal pool.

Passing: the sixteen originally-covered encounters bar the ones below, plus cultists,
cultist-and-seapunk, gremlin-merc, inklets, cubex-construct, fogmog, ruby-raiders,
two-tailed-rats' roster and openings, fossil-stalker, byrdonis, skulking-colony,
phantasmal-gardeners, bygone-effigy and ceremonial-beast.

Still failing, in the order I would take them:

| encounter | what is wrong |
| --- | --- |
| terror-eel | phase: its stun/terror opening consumes a turn the emulator does not |
| phrog-parasite | phase by one from turn six |
| slithering-strangler, living-fog, two-tailed-rats | one branch roll differs mid-fight |
| kin, vantom, lagavulin-matriarch, soul-fysh, waterfall-giant | the five bosses whose literals were converted by the automated pass and never verified — expect mis-attributions like Phantasmal Gardener's |

`bygone-effigy` and `terror-eel` also report `coverage:FAIL`, which is not an emulator
defect: the live fight ends before their third move ever appears, because the sweep plays
no cards. Reaching those moves needs a capture that fights back.

**On sweep cost:** a full act-1 sweep is ~20 minutes because the direct combat env assumes
every RNG stream is at CallCount 0, so each encounter needs its own fresh run. That is
inherent, not a hang. The working loop is one to three encounters at a time (~1-2 min); run
the full sweep as a tally, and do NOT pipe it through `tail`, which hides progress until it
finishes.

The previous figure was **15/16** on the smaller list: `combat_sweep.py --turns 6`
over all sixteen known encounters. Fixed against the live readout in this pass: Seapunk's
SPINNING_KICK (pre-multiplied 2x4, so its four hits could not each take the Strength Bubble
Burp grants — announced 9 where the game said 12), Sludge Spinner's RAGE (announced as a
Buff; the game calls it an Attack, and moving it to the attack branch meant carrying its
Strength with it) and its OIL_SPRAY (a Debuff whose number is still damage and still grows
with Strength — hence `Intent.CarriesDamage`), Mawler's move selection (a RandomBranchState
with CannotRepeat and a once-per-combat ROAR, modelled as `MoveIndex % 3`), and Fossil
Stalker's (an `AddBranch(state, 2)` maxRepeats roll, modelled as a hand-written opening
sequence). **Still failing: fossil-stalker**, whose damage now matches but whose move
*choice* diverges from turn three — the emulator's `AiRng` is a `CountingRandom` seeded
from `GameRng(seed, "monster_ai").RawSeed` rather than that stream itself, so a weighted
branch roll cannot track the game draw for draw. **shrinker-beetle** reports
`coverage:FAIL` rather than a mismatch: six turns of ending the turn never reach its third
move.

**The sweep now covers 29 of the 42 act-1 encounters, up from 16.** The other thirteen were
reachable the whole time — `debug_start_encounter` names the encounter model directly — and
every one was unobserved. Sweeping them found that **9 of 13 disagree with the live game**,
so the act-1 picture is: 16/16 of the originally-covered set (bar fossil-stalker and
shrinker-beetle's coverage warning), and a fresh backlog underneath. Passing on first
contact: **cultists** (verifying this pass's Dark Strike and Ritual fixes),
**cultist-and-seapunk**, **shrinker-and-fuzzy**. Now fixed: **gremlin-merc** — see below.
**Where the nine stand: 4 fully closed (inklets, fossil-stalker, cubex-construct, fogmog),
plus gremlin-merc earlier; 4 partly; 1 untouched.** The blocking question turned out to be
eligibility and draw COUNT, never stream alignment — see the Monster Move Machines section
of AGENTS.md, which is where the rules now live. What closed each of the four:

- **inklets** — `CannotRepeat` is scored against the last logged move, and their branch is
  only ever entered from JAB, so nothing is ever excluded and the roll is always over two.
  Excluding an older move made it a roll over ONE on half the turns: a different number
  from the same stream.
- **fossil-stalker** — its "first move" test read `MoveIndex`, but the encounter builds it
  with `moveIndex: 1`, so the test never fired and the machine rolled at combat setup: one
  draw the game never makes. Under that sat a special case firing on whichever turn
  `MoveIndex == 2`, dealing a two-hit Lash at A9 damage whatever the machine had chosen —
  which also doubled its Suck Strength, since Suck triggers per hit.
- **cubex-construct** — `CHARGE_UP` happens once; `EXPEL` returns to the first blast.
- **fogmog** — `ILLUSION → SWIPE → weighted branch`, not a flat three-cycle.

Still open, all with the structure now right and one detail wrong:
`two-tailed-rats` (roster size, openings and once-per-rat summoning all match; one weighted
roll mid-fight differs), `slithering-strangler` and both Flyconid encounters (rosters and
openings match; late-fight rolls differ), and `ruby-raiders`, which is untouched and is a
different problem — two of three raiders have the wrong HP, which is roster data.

The earlier state, for reference: **2 fully closed, 5 partly, 2 untouched.**
`cubex-construct` and `fogmog` are ALL MATCH. `slithering-strangler` and `slime-and-flyconid`
have correct rosters now (the first was building three enemies where the game builds two),
`inklets` has correct opening intents, and `gremlin-merc` was closed earlier. What remains
on all of them is one thing: **which move a RandomBranchState rolls mid-fight**. Fogmog's
branch lands where the live game's does, so the AI stream is not globally misaligned — but
encounters with several rolling enemies (three Inklets, three rats) desync, which points at
draw ORDER or COUNT across enemies rather than the stream itself. That is the single
question to answer next; it likely closes inklets, two-tailed-rats, fossil-stalker and the
tail of the Flyconid pair together. `ruby-raiders` is separate and still untouched: two of
three raiders have the wrong HP, which is a roster/data problem, not a branch one.

The original evidence, for reference:

| encounter | what the live game says |
| --- | --- |
| inklets | the middle Inklet opens on WHIRLWIND; the emulator assigns openings differently |
| cubex-construct | REPEATER_BLAST announces as an Attack and grows 9, 11, 13, 15 — it buffs itself |
| slime-and-flyconid | enemy 0 HP 35 live vs 28 emu: the encounter picks a random medium slime and the emulator picks a different one |
| jaxfruit-and-flyconid | Flyconid opens on FRAIL_SPORES (Attack 8), not VULNERABLE_SPORES; Jaxfruit's ENERGY_ORB grows 5, 10, 13 |
| ruby-raiders | two of three raiders have the wrong HP, and all three the wrong opening intent |
| fogmog | its summon move announces as an Attack, and Headbutt grows 9, 10, 11 rather than jumping 15, 16 |
| slithering-strangler | **roster is wrong: 3 enemies emu vs 2 live** |
| two-tailed-rats | opening intents are assigned to the wrong rats, and the summons diverge |
| fossil-stalker | damage matches; the move CHOICE diverges from turn three |

Two themes account for most of it: a move that attacks AND does something else is announced
with the wrong primary type (the Grasping Vines class, now seen five more times), and a
monster that buffs itself has an announcement that should grow and does not.

**A correction worth reading before trusting this pass's other conversions.** Gremlin Merc
was made WORSE by the blind A9→A8 sweep: its GimmeDamage, DoubleSmashDamage and HeheDamage
all key off **ToughEnemies**, which is live at A8, so the high branch was already right and
converting them to DeadlyEnemies made the Merc hit for 14 where the game hits for 16. The
live sweep caught it. A per-enemy audit — each `Ascension.Value` level against that same
monster's own declaration, not against any monster declaring the same number pair — found
exactly these two and no others, so the remaining ten conversions stand. Run that audit
after any future sweep of this kind; matching a `(high, low)` pair against every monster
masks the error, because `(8, 7)` is declared at both levels somewhere.

The earlier number was **11/16 ALL MATCH**, up from a baseline where Nibbit, Seapunk and
SludgeSpinner all failed on the Strength-display gap. Nibbit now passes, which is that fix
landing. Punch Construct, Vine Shambler and Haunted Ship — three of the encounters whose
damage this pass corrected — match the live game through six turns, so those A8 values are
ground truth now rather than inference. The sweep also found one defect no reading of the
source had: **Grasping Vines announces an Attack, not a Debuff** (emu `(Debuff, 8)` vs live
`(Attack, 8)`); its MoveState lists `SingleAttackIntent` first and `CardDebuffIntent`
second, and the emulator had taken the second as primary. Still failing: mawler, seapunk,
sludge-spinner, fossil-stalker on `turns`, and shrinker-beetle on `coverage` (its capture
never reached one of its moves).

**Operational note (bitten three times):** the FIRST encounter of a sweep run reports
spurious turn mismatches — after launching with a run in progress, and after a dylib
rebuild. Nibbit "regressed" in one full sweep as entry [1/16] and passed alone on a re-run
with nothing changed. Re-run before believing any single failure. The cause is the same
each time: the direct combat env assumes every RNG stream is at CallCount 0, which only
holds once the sweep has embarked its own run. The old note:

**Operational note:** the first sweep after launching with a run already in progress
reports spurious turn mismatches — the direct combat env assumes every RNG stream is at
CallCount 0, which only holds once the sweep has embarked its own run. Run it twice and
trust the second, or abandon the in-progress run first.

**FIXED (was known-open): the reported intent magnitude excluded Strength.** `Intent` now
carries `Hits` alongside per-hit `Magnitude`, exactly as this note prescribed, and
`Intent.AnnouncedDamage` builds the label the way `AttackIntent.GetTotalDamage` does —
modified per-hit damage, then multiplied. Execution reads the same two fields through one
generic loop, so a multi-hit attack lands per hit for block, Thorns and every per-instance
effect without a per-enemy special case. Enemies whose intent still carries a
pre-multiplied total keep `Hits = 1` and behave as before; converting one is part of
writing its encounter suite. The old note follows, since it explains why:

**Known-open (historical): the emulator's reported intent magnitude excludes Strength.** The live game
displays effective damage — a Nibbit at +2 Strength announces 14 for a 12-damage Butt,
and Seapunk's 2x4 SpinningKick shows 12 at +1 Strength. The emulator reports the base, so
any enemy that buffs itself diverges _in display_ from the turn it buffs, even when the
damage it deals is right. Fixing it properly means `Intent` carrying a **hit count**
instead of a pre-multiplied total, so display (`base + strength` per hit) and execution
(the loop) both derive from one place — which is also what would have prevented the
multi-hit bug above. Until then, `--turns` fails on Nibbit, Seapunk and SludgeSpinner for
this reason alone; toadpoles and corpse-slugs are clean through 6 turns with 3/3 coverage.

**Committed and pinned:** six combat captures now run the full comparison offline in
`tests/python/test_live_fixtures.py` — deck, roster/HP, opening intents and player HP,
one test class per capture, asserted separately so a failure says which generator moved.
Two coverage tests keep the set honest (both acts, both pools, and at least one
encounter whose composition is rolled from the per-encounter RNG). Both defect classes
were mutation-checked: deleting the `totalFloor` term from the encounter seed fails 5
tests, and putting one enemy's damage back to its A9 value fails 1, naming the enemy.

**Wanted next:** work outward from the opening state — turn 2+ intents, damage actually dealt, and the remaining ~120 unswept intent
literals. Every encounter in `LIVE_ENCOUNTER_BY_EMULATOR` is reachable in ~40s, so this
is a sweep-and-fix loop, not a research problem.

### Introspection & verification tooling (built)

- `scripts/compare_draw_pile.py` — emulator vs live ordered piles. `--live-json`
  re-diffs a saved capture offline; `--jump-encounter` avoids the lobby crash.
- `scripts/verify_run_generation.py` — the table above, straight from a save.
- `scripts/verify_act_selection.py` — act 1 vs the whole run history; `--fixture` re-runs
  it offline, `--all-builds` shows older patches for context.
- `scripts/combat_sweep.py` — the same idea for combat starts: deck, enemies, intents
  and player HP per (seed, encounter). See "Combat" above.
- `scripts/capture_sweep.py` — the batch version of all of the above: N seeds, embarked
  and compared unattended against a headless game. **This is the tool to reach for when
  touching generation code.** It embarks cleanly now that the embark race is fixed; it
  keeps a single retry as a backstop, announces it, and counts it in the summary, so a
  returning flake shows up as a number rather than being absorbed. Seeing a retry there
  means investigating, not raising the retry count.
- `scripts/start_real_game_run.py <SEED> --ascension 8` — embark a seeded custom run.
  Pass `--ascension 8`: the emulator models A8, and a capture at another level is not
  comparable (the elite budget differs, so encounters and map both diverge).
- Emulator: `Sts2_GetPile` -> `env.get_pile(...)`; run-generation lists 11-14 on
  `Sts2Run_GetStateList` (normal/elite/event sequences, `[act, boss, map_nodes]`, the
  map as (col,row,type) triples, and — new — its **edges** as (col,row,childCol,childRow)
  quadruples). Native API **v16**, run API **v9**.
- Live: our STS2MCP fork emits `draw_pile_ordered` / `discard_pile_ordered` /
  `hand_ordered` under `result["player"]`. The stock `draw_pile` is **sorted for
  display**, which is why ordered comparison was impossible before.
- `sts2_gym.game_seed("ABCDEF") -> 3334281563` — no more 500k brute-force seed search.
- **Encounter names were corrected** against the game's act pool: `ShrinkerAndFuzzy` ->
  `OvergrowthCrawlers`, `LargeSlimes` -> `SlimesNormal`, `SlimeAndFlyconid` ->
  `FlyconidNormal`, `JaxfruitAndFlyconid` -> `SnappingJaxfruitNormal`. The emulator had
  invented those four labels. Old Python encounter strings still resolve as aliases.

## Headless capture (the game runs without a window — verified)

**The game boots headless and is fully drivable, and the embark crash does not occur.**
This removes the one manual step in the whole pipeline.

```bash
GAMEDIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
MACOS="$GAMEDIR/SlayTheSpire2.app/Contents/MacOS"
echo -n "2868840" > "$MACOS/steam_appid.txt"     # one-time; see caveat below
cd "$MACOS" && ./"Slay the Spire 2" --headless &
# mod HTTP server is up in ~6s on :15526
```

- The binary is a custom Godot 4.5 build ("MegaDot", mono) and the release export
  template **keeps `--headless`** (`--display-driver headless --audio-driver Dummy`).
- Without `steam_appid.txt` it dies at Steamworks init — _"No appID found"_ — and then
  blocks forever on a confirmation popup nobody can click. The appid is **2868840**.
  ⚠️ That file is a Steamworks _development_ convenience; it lets the binary start
  without being launched by Steam. Harmless when launching through Steam normally, but
  delete it if that bothers you (headless then stops working).
- Costs ~148 MB RSS and a few % CPU. Boot to drivable is ~6s.
- **A scripted seeded embark that crashes in GUI mode completed cleanly headless**,
  which is further evidence the crash is presentation-layer (`NRunMusicController` is
  audio, and headless forces the Dummy audio driver).
- Proven end to end: a fresh seed `"HEADLESS1"` was embarked, captured and verified
  **ALL SECTIONS MATCH** first try — and it rolled Underdocks, a second independent
  check of that act.

**Why this does NOT mean running the game as the simulator.** `CiCoreRunner` in the
game's own DLL is a `Godot.Node`, so the logic is inseparable from the SceneTree:
`Cmd.Wait` uses `SceneTree.CreateTimer`, actions marshal through `ProcessFrame`,
combat runs on async `ActionExecutor` queues. Fine for capturing ground truth at
human speed; hopeless for the millions of rollouts MCTS needs, where the native sim
resets a combat in microseconds. **Real game for truth, native emulator for speed.**

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
the fixtures. That is not a rubber stamp: only the _game_ side moves, so an emulator
regression still fails the comparison.

**What it will not do is rewrite expectations from the emulator's own output.** Auto-updating an
assertion to whatever the code now produces turns a regression detector into a rubber
stamp — the failing DarkEmbrace test is precisely how the Exhaust-flag bug surfaced,
and a script that "fixed" it would have buried a defect affecting ~30 cards. Ground
truth also cannot be regenerated from the emulator by definition; it has to come from
the game.

### The modelled profile (why generation is seed-deterministic)

Two decisions read the **profile**, not the seed: Act 1 is rolled only among _unlocked_
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
uv run python -m unittest discover -s tests/python   # 119 pass
```
