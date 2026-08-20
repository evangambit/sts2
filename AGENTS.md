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
- State that belongs to one COPY of a card rides on `CardInstance`, not on the player.
  Rampage's damage growth is per-copy, so two Rampages in a deck grow separately —
  `CardInstance.BonusDamage` carries it. `CardEffects.Apply` takes the instance by value
  and cannot hand a mutation back, so a card that changes the copy being played sets
  `CombatState.PlayedCardBonusDamage` and `CombatEngine.PlayCard` folds it in wherever the
  card lands. Adding another per-copy property means extending that channel, not reaching
  into the pile afterwards.
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
  that reopens until its picks are spent (Purity), and a choice among freshly generated
  cards (Discovery), whose options ride on the selection because they exist in no pile
  until one is picked. A card that only offers part of a pile
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

## Relic Tests

- Relic effects live in `src\Sts2Emulator\Core\Effects\RelicEffects.cs`, one `public const int`
  per relic (the id from `Generated\Relics.g.cs`) and one hook method per timing:
  `ApplyBeforeOpeningHand`, `ApplyCombatStart`, `ApplyStartOfPlayerTurn`, `ApplyAfterCardPlayed`,
  `ApplyAfterUnblockedDamageReceived`, `ApplyAfterPlayerHpChanged`, `ApplyEndOfPlayerTurn`.
  Match the hook to the override the decompiled relic actually declares; `AfterRoomEntered` on a
  `CombatRoom` is combat start, and `BeforeSideTurnStart`/`AfterSideTurnStart`/`AfterPlayerTurnStart`
  all land on `ApplyStartOfPlayerTurn` here.
- A relic's only per-relic state is `RelicInstance.Counter`. Use `CountTowards` for "every Nth"
  relics (Happy Flower, Pendulum, Shuriken, Nunchaku) and `FiresOncePerCombat` for "the first time
  each combat" relics (Centennial Puzzle, Permafrost). A relic whose count is per turn is listed in
  `PerTurnCounters`, which the turn-start hook clears; leaving it out is how a per-turn relic
  silently becomes per-combat, so the test that plays across a turn boundary is the one that matters.
- `ValueProp.Unpowered` on a `BlockVar`/`DamageVar` means Strength and Dexterity do **not** apply —
  call `GainUnpoweredBlock`/`DealUnpoweredDamageToAll`, and write the test that stacks the buff and
  asserts the number did not move. Four relics were wrong this way before anyone checked.
- Tests are grouped by timing, not one file per relic: `Relics\CombatStartRelicTests.cs`,
  `TurnTimingRelicTests.cs`, `CardPlayRelicTests.cs`, `DamageReceivedRelicTests.cs`. Relics are far
  fewer than cards and read best side by side.
- A relic hook that has no chokepoint to hang off needs the chokepoint built first, not an
  approximation written down: gold gain, potion acquisition, deck additions, enemy spawns
  and the energy a play actually cost all became single functions because a relic needed
  them. Routing every call site is a mechanical edit; leaving one out is the bug, so grep
  for the raw operation afterwards (`\.Deck\.Add(`, `Enemies.Add(`) and expect zero hits
  outside the chokepoint itself.
- **The default encounter eats debuffs.** Both enemies in encounter 1 hold Artifact, so
  `Fight.WithRelics(...)` plus "assert every unprotected enemy is Weak" asserts over an empty list
  and passes whatever the code does. Use `Fight.Encounter(3, ...)` — three enemies, none protected —
  for anything that applies a debuff. Two tests were vacuous this way.

## Combat Tests

- Every encounter gets its own file: `src\Sts2Emulator.Tests\Combats\<Encounter>Tests.cs` holding
  `public class <Encounter>Tests`, named for the `ActOneEncounter` member. `CombatCoverageTests` is
  the guard, fed by `python scripts/generate_combat_coverage.py`, and `Pending` is a burn-down list
  exactly as `CardCoverageTests.Pending` is.
- Build the fight with `Fight.Encounter(ActOneEncounter.Toadpoles, ascension)` and watch it with
  `EnemyDefIds`, `Intents` and `EndTurn()`. An encounter test plays no cards: ending turns walks the
  enemy through its move table and puts its damage under test at the same time.
- What an encounter owes, in the order these have actually been wrong: the roster, HP at a known
  ascension, the opening intents, and the move cycle run long enough to repeat. Read all four off
  `decompiled\MegaCrit.Sts2.Core.Models.Encounters` (roster) and `...Models.Monsters` (HP, moves) —
  `GenerateMonsters` is the roster, not `AllPossibleMonsters`, which lists one of each kind.
- **The move machine is a graph, not a cycle.** `FollowUpState` wiring is what decides the order, and
  an opening move often never comes round again — Haunted Ship's HAUNT is entered once and its two
  attacks then point at each other. Transcribing it as `MoveIndex % 3` re-haunts every third turn,
  which is what the emulator did until an encounter test walked five turns.
- **Ascension is an input to HP as well as to damage.** `MinInitialHp` is usually
  `GetValueIfAscension(ToughEnemies, high, low)`; `EnemyDef.HpBand(ascension)` picks the branch, and
  the extractor keeps both. Taking only the first is how enemy HP stayed ascension-blind while every
  damage number was ascension-aware.
- **Block cannot tell a multi-hit attack from a single one** — it absorbs the same total either way.
  Assert the hit count with Thorns (one retaliation per hit) or a per-instance effect; a block-based
  assertion passes whether the attack lands once or three times, which a mutation check will catch.

## Waiting on a Long Sweep

- A full act-1 `combat_sweep.py` run is ~20 minutes: the direct combat env assumes every
  RNG stream is at `CallCount 0`, so each encounter embarks its own fresh run. That is
  inherent. Work in batches of one to three encounters (~1-2 min) and save the full sweep
  for a tally.
- **Do not pipe a sweep through `tail`** — nothing prints until it finishes, so a working
  run is indistinguishable from a hung one.
- **Poll the process, not the output** — but anchor the pattern so the waiter does not
  match ITSELF. `until ! pgrep -f combat_sweep` never exits: the waiting shell's own
  command line contains "combat_sweep", so pgrep always finds it. Use a pattern that only
  the real process has, e.g. `until ! pgrep -f "python.*combat_sweep"; do sleep 60; done`,
  and confirm with `ps -eo pid,command | grep "[c]ombat_sweep"` — bracketing the first
  character is what keeps grep out of its own results.
  An `until grep -q "<expected line>"` loop waits forever when the sweep dies early or its
  output never takes the expected shape, and it leaves an orphaned shell behind — three of
  those accumulated in one session, the oldest spinning for ten hours.
- **A live turn that did not advance looks exactly like an emulator running a turn ahead.**
  `wait_for_combat_ready` only asks for a combat state with a full hand, which is already
  true the instant `end_turn` is posted — so it could return before the game acted, and
  every later row compared the emulator's turn N against the live turn N-1. Two encounters
  were investigated as damage bugs on the strength of that. `drive_turns` now waits for
  `battle.round` to increase; the tell, if it ever comes back, is two consecutive live rows
  with an identical hand.
- Before reporting a sweep result as final, check nothing of yours is still running:
  `ps -eo pid,etime,command | grep -E "[u]ntil|[c]ombat_sweep"`.

## Monster Move Machines

The decompiled `MonsterMoveStateMachine` answers most "the emulator picks a different move"
bugs, and the rules are not guessable — read them there rather than inferring from a fight.

- **Only a `RandomBranchState` draws.** `MoveState.GetNextState` returns its `FollowUpState`
  id and consumes nothing. `FindNextMoveState` loops until it lands on a move, so a
  transition costs exactly one draw per branch traversed, and zero when a move leads
  straight to another move.
- **The first move never draws.** `FindNextMoveState` returns early while
  `!_performedFirstMove`, so `initialState` is used as-is. An `initialState` can be
  conditional — Toadpole and Nibbit branch on `IsFront`, Inklet on `_middleInklet`.
- **`CannotRepeat` scores against the LAST LOGGED MOVE, not any older one**, and the log
  holds moves only (a branch has `ShouldAppearInLogs => false`). So a branch reached from a
  fixed move — Inklet's, always entered from JAB — never excludes anything, and its roll is
  always over the full set. Excluding an older move makes it a roll over a smaller set,
  which is a DIFFERENT draw from the same stream: this is what made three Inklets desync
  from the live game while the damage was already right.
- **A cooldown is a different rule from `CannotRepeat`**: `AddBranch(state, 2)` is the
  *maxRepeats* overload (barred after coming up twice running), while
  `AddBranch(state, 3, MoveRepeatType.CannotRepeat)` is a *cooldown* of 3. `EnemyState`
  carries `MoveHistory` because one `LastMove` cannot answer either question.
- The roll itself is `NextFloat(total weight)` then a walk over the branches in the order
  they were added — `EnemyAI.PickBranch`. `Next(n)` is a different number from the same
  stream, so it desyncs everything after it.
- `CombatManager` rolls every enemy's next move at the START OF THE PLAYER'S TURN, iterating
  the roster in order, which is what `EnemyAI.ChooseIntents` mirrors. A creature added
  mid-combat also rolls as it is added (`AfterCreatureAdded`), so a summon costs a draw when
  its machine starts on a branch — check this before assuming a summoning encounter has an
  intent bug.

- **Eligibility can depend on the rest of the roster.** Two-Tailed Rat's `CanSummon()`
  returns false when any OTHER rat's `NextMove` is already `CALL_FOR_BACKUP_MOVE`, so the
  first rat to pick backup takes the option off the table for the rest of the pass. That is
  why `SelectIntent` is handed the roster: a monster reads the moves chosen before its own.
- **A monster that sits out turns must not walk its ring while it does.** Lagavulin
  Matriarch's `SLEEP_MOVE` loops on itself while `AsleepPower` lasts and the branch only
  then sends her to `SLASH`. Advancing `MoveIndex` on the sleeping turns starts the ring
  two moves in and every later move is wrong by two.
- **`StartStunned` is opt-in.** Wriggler sets it; Gas Bomb does not, so a bomb goes off in
  the same enemy phase that summoned it. Adding a stun the game never asked for delays a
  summon by exactly one turn, which reads as a damage bug.

## Card Piles and Shuffling

The shuffle is a bigger source of "the damage is wrong on turn five" than any monster.

- **The combat-start shuffle and the mid-combat reshuffle are different operations.**
  `CardPile.RandomizeOrderInternal` is a plain `UnstableShuffle` over the deck's own order.
  `CardPileCmd.Shuffle` is a `StableShuffle`: it merges the discard pile and whatever is
  left of the draw pile (discard first), **sorts**, and only then Fisher-Yates.
- **The sort key is the model's string id, not any number.** `CardModel.CompareTo` falls
  through to `ModelId.CompareTo`, which is an ordinal comparison of `Category` then `Entry`
  — the slugified class name. `CardDef.Entry` carries it, written by `extract_data.py`.
  Sorting by our own numeric ids gives the right pile *counts* and the wrong card on top,
  which surfaces several turns later as a status card burning the player early.
- **`CardPilePosition.Random` rolls on `Rng.Shuffle`**, as `Shuffle.NextInt(count + 1)` —
  not the combat stream. Soul Fysh's Beckon is placed this way.
- **Statuses with `HasTurnEndInHandEffect` burn their holder for the card's own damage
  value** — Burn 2, Infection 3, Wither 3, Toxic 5 — and Beckon loses HP instead, ignoring
  block. Toxic has no OnPlay at all: paying 1 to exhaust it is how the damage is dodged.
- The per-turn hands the sweep records are what puts all of this under test; see
  `test_every_turn_hand_matches`.

## Buff and Debuff Duration

- **Weak, Frail and Vulnerable tick after the ENEMY side's turn**, once a round — all
  three do it in `AfterSideTurnEnd(side == CombatSide.Enemy)`. Ticking anywhere earlier
  loses the last turn of every one of them: an enemy swinging into the player's final
  point of Vulnerable still hits for 1.5x, and an enemy the player made Weak still swings
  weakened.
- **A debuff NEWLY created on a player-side creature skips one tick**, because
  `PowerCmd.Apply` sets `SkipNextDurationTick` on the model it creates. Adding to a stack
  the player already holds does NOT — the existing power's flag is untouched, so it ticks
  as usual. Live at A8 the Two-Tailed Rats screech nearly every turn and the player's
  Frail reads 1, 1, 0, 1: a point applied, a point ticked. Treating any increase as a
  skip makes it climb, which is a point of block a turn. Enemies get no grace at all.
- **Turn-end self-damage goes through block.** Constrict and Disintegration both use
  `CreatureCmd.Damage`, not an HP loss, and so does every `HasTurnEndInHandEffect` status
  bar Beckon. A capture that plays no cards holds no block and cannot tell the difference.

## Coverage, and What It Cannot See

Coverage is `distinct live readouts seen / declared moves`, and BOTH sides of that
fraction have been wrong in ways that read as an emulator defect:

- **A live intent type the harness does not map returns None, which is dropped from the
  count** — so the move can never be seen, however long the capture runs. `Sleep`,
  `Summon`, `Heal` and `DebuffStrong` each cost a monster a declared move permanently.
  `live_enemy_intent` now covers the game's whole `MonsterMoves.Intents.IntentType`, and
  the sweep reports any type that turns up unmapped instead of silently dropping it.
- **Some declared states are unreachable from the machine's initial state.** Terror Eel's
  STUN_MOVE is entered by `ShriekPower` when an unblocked hit drops it to a threshold,
  Waterfall Giant's ABOUT_TO_BLOW by `TriggerAboutToBlowState`, Ceremonial Beast's by
  `PlowPower` — always `CreatureCmd.Stun(owner, ..., stateId)`. `enemy_moves.py` walks
  the FollowUpState/AddBranch graph from the initial state and drops what it cannot
  reach; it counts everything when it cannot parse the machine, because a parser that
  quietly found no edges would drop every move and turn coverage green.
- A capture that passes every turn dies too early to reach the far end of a move table.
  `--play` plays the first card the LIVE game says is playable, which is what closed the
  Waterfall Giant and Lagavulin Matriarch.

## Driving the Live Game

Both harness bugs found here have the same shape — **posting an action and reading the
state straight back reads it before the game has acted**:

- After `end_turn`, wait for `battle.round` to increase (`wait_for_next_round`). Without
  it a live turn that had not yet taken made the emulator look a turn ahead, and two
  encounters were investigated as damage bugs on that basis.
- After `play_card`, wait for the hand to shrink (`wait_for_card_to_leave_hand`). Without
  it every later action indexes into a hand the game has moved on from, and the two sides
  drift apart inside one turn.
- **The emulator keeps a dead enemy in the roster at 0 HP** so an agent's observation has
  stable slots; the game removes the creature. Compare only living enemies, or every
  fight where something dies looks like an emulator inventing an attacker.

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
