# AGENTS.md

## General

- Read the README.md to get an overview for what this project is.
- Whenever you edit a Markdown file, format it afterward with: `bunx --bun prettier --write foo.md`
- Whenever you edit a Python file, format it afterward with: `uv run black --target-version py314 foo.py`
- To validate code changes, run the combined lint/test script: `bash lint-and-test.sh`
- For `Sts2RunEnv` run-level reward logic, prefer decompiled source under `decompiled\` (for example `PotionRewardOdds`, `PotionFactory`, and merchant entry classes) over inferred deterministic shortcuts.
- Full-run replay diagnostics should report available boundary diffs before stopping on unsupported trace actions, and unsupported action errors should include the reference step, state type, and floor.
- Full-run replay may coalesce live reward/event substeps as no-ops when `Sts2RunEnv` has already applied those rewards or advanced to map, so retained traces continue to the next meaningful mismatch.

## Code Organization

- Native card effects that cause player HP loss should use `CardEffects.LoseHp` so Rupture and Inferno hooks stay consistent with other card-effect self-damage.
- Native card effects that exhaust another card from hand should select from `state.Hand` after the played card has already been removed, then call `CardEffects.ExhaustCard` so exhaust hooks stay consistent.
- Native card effects that upgrade hand cards should replace `CardInstance` values in `state.Hand` with upgraded copies; the played card has already been removed before `CardEffects.Apply` runs.
- Native card effects that conditionally draw multiple cards should draw one card at a time and respect the 10-card hand cap so the newly drawn card controls whether drawing continues.
- Native card powers that modify attack play count should live in `CombatEngine.PlayCard`, apply one extra `CardEffects.Apply` for affected Attack cards, decrement their counter per Attack, and expire at end of player turn.
- Native card powers that auto-play cards at the start of the player play phase should run in `CombatEngine` after the normal turn-start draw, bypass energy spending, and still route played Attack cards through normal attack play hooks and discard/exhaust cleanup.
- Native cards with dynamic per-turn costs should compute those costs in `CombatEngine.EffectiveCost` so play validation and energy spending stay aligned.
- Native generated cards that are free only for the current turn should use `CardInstance.FreeThisTurn`; clear it when cards leave hand for discard or exhaust piles.
- Native cards that return themselves before the next turn's draw should queue the played `CardInstance` from `CombatEngine` play lifecycle hooks, then move the matching card from discard/draw/exhaust to hand before normal draw.
- Native X-cost cards should spend current `state.Energy` inside `CardEffects.Apply` after the played card's printed cost has been handled; generated X-cost cards currently encode cost as 0.
- Native card effects that retain the remaining hand should apply a player `BuffId` and let `CombatEngine.EndTurn` skip normal discard for non-ethereal cards, then decrement/remove the retain counter at player side turn end.
- Native card effects that reapply or scale an enemy debuff after dealing damage should keep the pre-damage target reference, verify the target survived, and reuse the relevant debuff hooks.
- Native card effects with multiple actions should use explicit card cases when decompiled effect order matters; do not rely on fallback damage/block ordering.
- Native card effects that move cards from discard to hand should operate after the played card has left hand, clear `FreeThisTurn`, and respect the 10-card hand cap.
- Native card effects that splash based on the first hit should use the effective first-hit HP-loss plus overkill amount, then apply splash as unpowered damage unless decompiled value props say otherwise.
- Native cards that care whether the player lost HP this turn should use `CombatState.PlayerHpLostThisTurn`, reset it at the start of each player turn, and increment it from relevant unblocked player HP-loss paths.
- Native cards that care whether any card exhausted this turn should use `CombatState.CardsExhaustedThisTurn`, increment it only through `CardEffects.ExhaustCard`, and reset it at the start of each player turn.
- Native card effects that trigger when the card itself exhausts should put the hook in `CardEffects.ExhaustCard` so it works for normal self-exhaust and secondary exhaust effects.
- Native card effects that repeat block gain should call `CardEffects.GainBlock` once per decompiled gain so block hooks trigger per gain.
- Native cards that grant next-turn block should store a `BuffId.BlockNextTurn` amount, resolve it after the next player-turn block clear in `CombatEngine`, and grant it as unpowered block.
- Native cards that apply temporary enemy Strength loss should consume Artifact before applying paired `Strength` and `TemporaryStrength` buffs, then restore the enemy Strength in `EnemyAI.ExecuteIntent` at that enemy's turn end.
- Native card powers that modify a played card's destination pile should make that decision in `CombatEngine` after effects resolve but before adding the card to discard.
- Native card powers with extra dynamic variables can be represented with companion `BuffId` entries when `BuffState` needs to track both the visible counter and hidden per-power state.
- Never hand-maintain a list of which cards do a thing. `EffectiveCost` carried three id
  lists of "cards that get cheaper when upgraded" covering 18 of the 56 cards that
  actually do, so Corruption, Tank, Barricade and the rest silently kept their unupgraded
  cost. `extract_data.py` reads `EnergyCost.UpgradeBy` off each card into
  `CardDef.UpgradeCost` instead — the same treatment the Innate flags get. If a property
  is stated on the card, extract it; a list in the engine goes stale without failing.
- A card's numbers live in `Cards.g.cs` and its behaviour lives in `CardEffects.Apply`.
  When they disagree the data is usually right: Flash of Steel's case hardcoded 3/6 while
  the extracted `CardDef` had the game's 5(+3) all along. Prefer `Dmg(def, upgraded)` and
  `Blk(def, upgraded)` over literals so the two cannot drift.

## Card Selection

- Cards whose effect asks the player to pick a card (Headbutt's discard retrieval, upgraded
  True Grit's exhaust) pause mid-play by setting `CombatState.PendingSelection`. While it
  is open, `CombatEngine.ValidActions` offers **only** the candidate indices and
  `CombatEngine.Step` interprets an action as the answer, so an agent resolves it as an
  ordinary step and cannot play, end the turn or use a potion first.
- The observation carries the open selection (kind, candidate count and up to
  `MaxSelectionCandidates` card ids), because a policy that cannot see what it is choosing
  between cannot learn the choice. That is what took the native API to v17 and the run API
  to v10.
- Do not restate `CombatObservation.ObsSize` anywhere. `RunConstants.CombatObsSize` used to
  carry its own copy of the number, and the run observation silently reserved the old width
  for the combat block the moment it grew.
- A card played from inside another card (Havoc) or from the auto-play queue (Hellraiser,
  Stampede, Mayhem) cannot hand a selection back to the caller mid-drain, so those resolve
  automatically — `CardEffects.PlayNestedCard` and the `AutoPlay` wrappers set
  `CombatState.AutoPlaying` for exactly that, saving and restoring it rather than clearing.
  Adding another "play a card from within an effect" path means routing it through
  `PlayNestedCard`, or the engine will strand a selection inside a queue nobody can answer.
- The kinds in `CardSelectionKind` cover the shapes seen so far: a card out of the discard
  pile (Headbutt), a card exhausted from hand (True Grit, Brand), exhaust-then-draw
  (Burning Pact), a card fetched out of the draw pile (Secret Technique, Secret Weapon,
  Seeker Strike), a card put back on top of it (Thinking Ahead), and a repeated exhaust
  that reopens until its picks are spent (Purity). A card that only offers part of a pile
  passes explicit candidate indices, so the filter lives in `Candidates` rather than in
  the resolution.
- Tests make the choice explicitly with `Fight.Choose(candidate)`; there is deliberately no
  "pick something sensible" helper, since the emulator picking on the player's behalf is
  the behaviour this replaced.

## Card Tests

- Every card gets its own file: `src\Sts2Emulator.Tests\Cards\<Class>\<CardName>Tests.cs` holding
  `public class <CardName>Tests`, where `<Class>` is the card's id class in `CardIds.g.cs`
  (`IC` -> `Ironclad`, `SI` -> `Silent`, `CL` -> `Colorless`, `AN` -> `Ancient`, `ST` -> `StatusCurse`).
  Test methods omit the card name, because the class already carries it: `UpgradedHitsTwice`, not
  `Cleave_UpgradedHitsTwice`.
- Build the combat with `Fight` (`Tests\Support\Fight.cs`) rather than hand-rolling setup:
  `Fight.Hand(Card(IC.MoltenFist)).Energy(1).Enemy(hp: 100)`. Anything the builder does not cover is
  set directly on `Fight.State` — do not add a builder method that one card would call.
  `MoltenFistTests` is the worked example.
- Each card needs at least three tests: the unupgraded effect, the upgrade delta, and one interaction
  with whatever hook it touches (exhaust hooks, the 10-card hand cap, target death, its scaling
  counter). Cards with a conditional or scaling term also need the case where the condition is unmet;
  `SpiteTests` is the model.
- Expected values come from `decompiled\` or a live capture, never from running the emulator, and the
  decompiled class goes in a comment above the test. Deriving expectations from our own output is a
  rubber stamp — the same rule `scripts\generate_capture_tests.py` documents.
- After adding a `case` to `CardEffects.Apply`, run `python scripts/generate_card_coverage.py` and
  either write the tests or add the card to `CardCoverageTests.Pending`. The build fails otherwise.
  `Pending` is a burn-down list: shrink it, and expect to justify any growth.
- `python scripts/generate_card_coverage.py --print-untested` lists what is still unverified.
- A card counts as implemented when it has dedicated logic, which comes in **two** shapes:
  a constant case in `CardEffects.Apply` (`case IC.MoltenFist:`) or a name case in one of
  the per-character routines (`ApplyDefectCard`, `ApplyNecrobinderCard`, `ApplyRegentCard`,
  `ApplyMiscGeneratedCard`, all switching on `def.Name`). Counting only the first shape is
  how Defect, Necrobinder and Regent — 263 implemented cards — stayed invisible to the
  guard, which reported 235 implemented when the real number is 552. Name cases are
  filtered against `data/id_map.json` so that `case "ReanimatePower"` is not mistaken for
  an untested card.
- Cards that reach the engine through the generic damage-and-block path are still
  invisible: Strike, Defend and Giant Rock have no case of either shape. An empty
  `Pending` means "every card with dedicated effect code is tested", not "every card is
  tested". Widening the rule to "has damage or block in the data" is wrong — it matches
  146 cards, most of which do more than the data describes.

### Ground truth from the running game

- `decompiled\` is the shipped logic but not the game executing it, so it is weakest
  exactly where cards are hardest: effect ordering, rounding, splash and overkill, what a
  power sees when a target dies mid-effect. For those, capture the real thing:

  ```
  python scripts/capture_card.py --card MoltenFist            # game running, any OS
  python scripts/generate_card_capture_tests.py               # -> Cards/CardCaptures.g.cs
  ```

- `capture_card.py` stages the card with `debug_add_card`, guarantees it is affordable
  with `debug_set_energy`, plays it, and commits the before/after under
  `tests\fixtures\cards`. The fixture is self-contained — it records the state the card
  was played into, so the generated test rebuilds that exact situation instead of
  reproducing a whole run.
- The capture refuses to write a fixture when the card was unplayable or the state did
  not move, and generation refuses fixtures it cannot rebuild faithfully (an unmapped
  power, a relic in play, a card missing from `data\id_map.json`). Both failures are
  loud on purpose: a capture that silently drops the interesting half is worse than none.
- A capture rebuilds the _situation_, not the game's RNG state, so it still cannot pin
  _which_ enemy a random-target effect hit (Juggernaut, Volley, Sword Boomerang, an
  auto-played Attack). The emulator now models the `CombatTargets` stream those draw
  from, but a fixture does not carry that stream's call count, so asserting a per-enemy
  result from one sample gives a test that is wrong half the time. Capture such cards for
  everything else they prove — amounts, powers, piles — and assert the target choice with
  a property test instead (`JuggernautTests.TargetVariesWithTheTargetStream`).
- Randomness is per-stream and the streams are not interchangeable. Picking WHICH enemy
  to hit is `combat_targets` (`CombatState.TargetRng`, via `CardEffects.RandomLivingEnemy`);
  picking WHICH existing card to exhaust or transform is `combat_card_selection`
  (`CombatState.CardSelectionRng`, via `CardEffects.CardSelectionRng`); rolling up a NEW
  card is `combat_card_generation`; and Stampede's pick is `shuffle`, despite also choosing
  a card from hand. Check the decompiled effect for which `Rng.*` it reads rather than
  assuming from what the effect looks like.
- Random target choice belongs to `CardEffects.RandomLivingEnemy`, which reads
  `CombatState.TargetRng`. Adding a new random-target effect means calling that rather
  than indexing with the combat RNG: the game draws every target from
  `Rng.CombatTargets`, so a stray `rng.Next` desynchronises the stream for everything
  after it — the failure `AiRng` was introduced to fix.
- Never hand-edit `Cards\CardCaptures.g.cs`, and never copy emulator output into a
  fixture. Re-capturing re-reads ground truth; deriving it from our own output is the
  rubber stamp `scripts\generate_capture_tests.py` warns about.

## STS2MCP

- This project uses a fork of STS2MCP, checked out beside this repo as `..\STS2MCP`
  (`D:\Repositories\STS2MCP` on the original Windows box,
  `~/Projects/STSS/STS2MCP` on the macOS one).
- Sometimes, we might need to update the mod in order to add/fix API functionality. If we make updates to the mod, we need to:
  - Recompile the mod. It builds anywhere `dotnet` does — the csproj resolves the game
    assemblies per-platform (`data_sts2_windows_x86_64`, `data_sts2_macos_arm64`,
    `data_sts2_linuxbsd_x86_64`), so pass the install directory and let it pick:
    `dotnet build STS2_MCP.csproj -c Release -p:STS2GameDir="<install dir>"`.
    `build.ps1` is a PowerShell convenience wrapper around exactly that, not a requirement.
  - Close the running Slay the Spire 2 instance, if it is running.
  - Copy the DLL into the game's mods directory, which differs by platform:
    - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods`
    - macOS: `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods`
- If you need to make a trace from an in-game replay, you can use the `start_replay` API that we added to STS2MCP.
- The root STS2MCP URL (`http://localhost:15526/`) is only a health check. Use `http://localhost:15526/api/v1/singleplayer` for singleplayer state and actions, including replay control commands such as `start_replay`, `get_replay_status`, and `cancel_replay`.
- Debug actions the differential harnesses rely on: `debug_start_encounter`,
  `debug_force_play_phase`, `debug_add_card`, `debug_set_energy`, `return_to_main_menu`.
  `debug_add_card` takes the card's entry id or its C# class name (`MOLTEN_FIST` or
  `MoltenFist`), so callers do not need the id map.

## Platforms

- **Nothing in this repo is Windows-only.** The C# suite, the native library, the mod,
  the live differential harness and the game itself all run on macOS and Windows; the
  scripts talk to the mod over HTTP and take `--base-url`, so the game does not even have
  to be on the same machine. Do not tell the user a task needs the Windows box —
  check first.
- `lint-and-test.sh` carries Windows/WSL fallback paths for `uv` and `dotnet`; those are
  fallbacks for one contributor's setup, not a statement about what is supported.
- On the macOS box `dotnet` is installed at `~/.dotnet/dotnet` and is missing from a
  non-interactive `PATH`, so `which dotnet` finds nothing while the toolchain works fine.
  Use the absolute path: `~/.dotnet/dotnet test src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj`.
- `lint-and-test.sh` publishes the native library with `-r win-x64`; on macOS build it
  with `scripts/build.sh`, which produces `out/Sts2Emulator.dylib` (the loader in
  `src/sts2_gym/native.py` picks `.dll`/`.so`/`.dylib` per platform).

## Slay the Spire 2 Launch Instructions

- **Always launch Slay the Spire 2 through Steam**, not by starting the executable
  directly. Otherwise, the game will fail to initialize with the following error: Steam
  failed to initialize. Make sure you run the game from Steam.
- Launch: `Start-Process "steam://rungameid/2868840"` (Windows) or
  `open "steam://rungameid/2868840"` (macOS).
- After launching through Steam, verify STS2MCP with
  `Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:15526/"` (Windows) or
  `curl -s http://localhost:15526/` (macOS). The root URL is only a health check.
