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

# C# unit tests (currently 206 pass)
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

- **Emulator is patch-current & fully working on macOS**: builds, 206 C# + 25 Python
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

1. **Opening-hand exact match** — one residual left (enemy HP already exact):
   - ~~Port the game's turn-1 draw-pile reorder~~ — **DONE**, see
     `CombatFactory.ApplyTurnOneDrawPileReorder` (8 tests). Note the reorder is a no-op
     for the *starter* deck (no Innate cards, and `ShouldStartAtBottomOfDrawPile` is the
     **`Imbued` enchantment** — not Ascender's Bane, as previously written here — and
     enchantments aren't modelled, so `StartsAtBottomOfDrawPile()` is hardcoded false).
     So this fixed a real divergence for later decks but **cannot** be the cause of any
     turn-1 starter-deck mismatch. Two ordering subtleties are ported and pinned by test:
     innate cards end up **reversed** (the game inserts each at index 0), and the game's
     `Except` runs on *reference* identity — do not reimplement it with LINQ on
     `CardInstance`, which is a value type and would dedup equal cards.
   - ~~Residual shuffle-state factor~~ — **ANSWERED, and it was not shuffle state.**
     The introspection tooling was built and run against the live game: deck composition
     matches exactly, the shuffle *algorithm* matches (both are the same descending
     Fisher-Yates), the master deck order matches the save's `deck` array, and the save
     shows every combat stream at CallCount **0** — so the fresh-stream assumption was
     right and there is no offset to find. **The divergence is the RNG algorithm itself**
     (see above). **The MegaRandom port is DONE and the opening hand now matches
     exactly.** Item 1 is closed.
   - ⚠️ **Follow-on from the RNG port — `RunMapGenerator` needs re-verification.**
     It contains empirically-tuned fudges (a bare `for i < 202: upFront.NextDouble()`,
     then 57/60 `NextInt` calls) that were tuned to *reproduce CallCounts under the old,
     wrong RNG*. They are now meaningless as calibration and were never verified against
     the live game. Map generation also does not use the game's `act_N_map` stream
     (`RunRngSet.ActMapRng`) — it seeds off the bare run seed. Under the corrected RNG
     the act for seed `"0"` flipped Overgrowth→Underdocks and the Neow options changed,
     which is expected, but none of it is validated. **Verify map/act/Neow generation
     against a live run next.**

   **Introspection tooling (built, live half not yet exercised):**
   - Emulator: `Sts2_GetPile` (native API **v12**) → `env.get_pile("draw"|"hand"|
     "discard"|"exhaust")`, returning `(card_def_id, upgraded)` in true order,
     index 0 = top. The obs vector only ever carried pile *counts*.
   - Live: our STS2MCP fork now emits `draw_pile_ordered` / `discard_pile_ordered` /
     `hand_ordered` under `result["player"]` (raw entry ids, true order). **The existing
     `draw_pile` field is sorted by rarity/id for in-game display** — that's why an
     ordered comparison was impossible before; both fields are kept.
   - `scripts/compare_draw_pile.py` joins them and prints a side-by-side diff, and
     distinguishes *same cards wrong order* (shuffle/reorder bug) from *different cards*
     (deck construction bug). `--live-json` re-diffs a saved capture with no game running;
     `--save-live-json` captures one. Verified both ways against fixtures; **the live
     capture path has not been run against the real game yet.**
   - `sts2_gym.game_seed("ABCDEF") -> 3334281563` ports the string→gen-seed hash to
     Python, so harnesses no longer need `find_matching_seed`'s 500k brute-force search.
2. **Harden the debug/menu mod actions** with settled-state guards (fix the crash-on-churn).
3. **Implement `start_replay`** in the STS2MCP fork → clean deterministic full-run capture
   (design in docs/replay-verification.md §1).
4. **Full-run replay harness** → the primary fidelity metric (docs/replay-verification.md).
5. Then the **AlphaZero layer**: MCTS over the sim (C#) → value/policy net (Python) →
   self-play (PLAN.md §2, §5).

## One-shot smoke test (verify the whole stack works)

```bash
cd ~/Projects/STSS/emulator
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$PATH"
dotnet test src/Sts2Emulator.Tests/        # 206 pass
bash scripts/build.sh osx-arm64            # → out/Sts2Emulator.dylib
uv run python -m unittest discover -s tests/python   # 25 pass
```
