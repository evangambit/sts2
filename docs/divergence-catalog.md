# Divergence catalogue

Every emulator/game divergence found by differential testing, with the metric that
exposed it, the cause, and the seed to reproduce it on. New entries go at the
bottom of their table; nothing is removed once fixed, because the *reason* a bug
survived as long as it did is usually more reusable than the fix.

Two companions: `tests/fixtures/run_trace/README.md` lists the captured runs and
their current status, and `docs/replay-verification.md` explains why fidelity is
measured as "% of real runs replayed exactly" rather than claimed as "no bugs".

## How to reproduce

```bash
uv run python scripts/replay_full_run_trace.py tests/fixtures/run_trace/<SEED>-a8.json
```

The seeds below are all ascension 8 on **v0.107.1** (build 23811903). "Step N" is
an index into that trace. A few entries were found by the per-encounter sweep
instead:

```bash
uv run python scripts/combat_sweep.py --seeds <SEED> --encounters <name> --ascension 8 --play
```

Seeded capture requires custom mode — standard mode refuses a chosen seed — and a
run's FIRST act is seed-dependent, so roughly half of all seeds open in Underdocks
rather than Overgrowth.

---

## Open

Five, all found by one batch of six captures taken after the set had gone green. A sixth
(an enemy holding twice its block) is closed below as E36-E37.

| # | Metric | Seed | What is known |
| --- | --- | --- | --- |
| O3 | `player.block` step 122: 3 live, 5 here | `25TS4F5T37` | A Defend the game taxed for Frail and the emulator did not, so the emulator is a stack short somewhere earlier. Frail is not a compared field, so the miscount is invisible until it changes a number — widening the comparison to player status would say where it starts. This is the only thing left in a run that used to end 35 steps early (E33-E35). |
| O4 | `player.hp` step 75: 57 live, 61 here | `XTLVVPKFBF` | The same Fogmog/Eye encounter as O3, so start by asking whether it is the same cause. |
| O5 | `player.hp` step 75: 21 live, 20 here; `battle.enemies` 91: 65 live, 74 here | `L9R346P3YD` | A Skulking Colony elite and no illusion anywhere in the run, so this is its own cause. Nine HP of enemy damage unaccounted for by step 91. |
| O7 | `player.hp` step 126: 9 live, 8 here | `NXV45HW43K` | One point, after 126 clean steps, and nothing else wrong in 149. The narrowest lead in the set and probably the cheapest to chase. |
| O8 | `state_type` step 1: game `rewards`, emulator `event` | `J09SPL8Y3V` | **Diagnosed, not fixed. Neow's Bones is three defects at once.** `AfterObtained` shuffles the valid relic list on `PlayerRng.Rewards` and takes 2, then offers them as a `RewardsSet(...).WithCustomRewards(...).WithSkippingDisallowed()` — a screen the player answers, twice, before the curse is added. The emulator draws two `Rng.UpFront.NextItem` instead: the wrong stream, a method that can hand out the SAME relic twice where a shuffle-and-take cannot, and no screen at all. Its candidate list is wrong too — the game takes every Neow option's relic except Neow's Bones itself (28 of them, `IsAllowedAtNeow`-filtered), the emulator only `NeowPositiveOptions`. Same shape as E18 (Lost Coffer). |

All sixteen committed traces are ground truth either way; `tests/fixtures/run_trace/README.md`
tracks which replay clean. Eleven do.

The two that stood here last are E22 to E26 below; E27 and E28 came out of reading the
code they touched, not out of a capture. What closed them is worth knowing
before opening the next one: **O2's stated cause was right and its stated location was
wrong.** The shuffle stream really was at a different position — but the fight it
surfaced in had nothing to do with it, and the draws were spent in a fight three floors
earlier that had already been won.

### How to measure a stream position on the live side

The game exposes no call counts, but a capture pins them anyway. A combat's opening pile
is `hand_ordered + draw_pile_ordered`, its input is the deck in run order, and the shuffle
is a plain `UnstableShuffle` — so the position the game was at is the one `k` for which
`shuffle(deck, stream[k:])` reproduces the capture. Sixteen cards make a false match
vanishingly unlikely, so a search over `k` reads the answer straight off a fixture, with
no game running. The emulator's side is `Sts2Run_GetShuffleRngCallCount`, which already
existed. Comparing the two per combat named the fight in one pass, where reading the fight
that failed could not have.


## Fixed — engine

| # | Metric | Seed / where | Cause |
| --- | --- | --- | --- |
| E1 | Endless Conveyor upgraded a Defend where the game upgraded a Strike | event captures (`ABCDEF`) | **`EventRng` built a fresh stream per call**, so every draw inside an event came off position 0. The game gives an event one `base.Rng` for its lifetime: the conveyor rolls its dish in `CalculateVars`, then Observe the Chef's pick reads the SECOND value. |
| E2 | (same fix) tribute cost, bridge card, Ranwid's relic and the trader's shelf changed between reads | event captures | Those values were **accessors that re-rolled on every call** — idempotent only while every draw restarted. The game computes each once as the event is generated. Ranwid also never spent his potion draw, so the relic came off the wrong slot. Consequence: **reading the action mask advanced the run's randomness**, so an agent perturbed the outcome it was deciding about. `EventRngStabilityTests` pins that invariant. |
| E3 | `state_type` step 1: game `event`, emulator `map` | `4KJ7X2MQND` | **Neow's blessings had no Proceed page.** Every one ends in `AncientEventModel.Done()`, a `SetEventFinished`. Only Kaleidoscope was given one, because its reward screen made the gap visible; the rest jumped to the map and swallowed a real action. |
| E4 | `battle.enemies` step 105: enemy at 29 HP live, 24 here | `Y75EFT6EDV` | **A multi-hit attack retargeted.** `AttackCommand` re-filters its targets each hit and BREAKS when none are alive; for a single-target attack that list is just the one chosen, so a target that dies partway through eats the remaining hits. `DealDamageMultiHit` re-resolved the target per hit and fell back to the first living enemy. Affects every multi-hit single-target attack. |
| E5 | `event.event_id` step 59: game `SUNKEN_TREASURY`, emulator `WAR_HISTORIAN_REPY` | `4KJ7X2MQND` | **`IsEventAllowed` ends in `_ => true`**, so an event whose rule nobody transcribed is silently ALLOWED. War Historian Repy's rule is `return false` — never drawn from the sequence at all. Reading all 35 overrides also found: Endless Conveyor wanted 40 gold (game: 120), Waterlogged Scriptorium's `or Deck.Count > 0` clause made a 55-gold test always pass, Unrest Site was unconditional (game: hp ≤ 70% of max), and Whispering Hollow, Trash Heap, Spiraling Whirlpool, Punch Off and Slippery Bridge had no case at all. |
| E6 | `battle.enemies` step 68: gardener block 7 live, 0 here | `4KJ7X2MQND` | **Skittish unmodelled.** Every Phantasmal Gardener carries it: the first card each turn to land UNBLOCKED damage gives it `SkittishAmount` block (7 at A8, 6 below). The power persists — spent for the turn, cleared when the player's turn ends. |
| E7 | Soak crash: "Cannot rewind an RNG stream" | soak, boosted runs | `RollDish` fast-forwarded a fresh stream to grabs-minus-the-fifths, a workaround from when every `EventRng` call built its own. With a persistent stream that became a REWIND as soon as anything else drew from the event. **The C# suite could not catch this** — it walks the belt down one path and never mixes a grab with another draw. |
| E8 | `player.hp` step 77: 61 live, 58 here (via block 8 vs 5 at 74, deck at 63) | `DPUJR117FL` | **Fishing Rod was a constant with no effect.** It upgrades a card every third MONSTER room — elites, bosses and event-started fights return early and do not even advance its counter — picking off `Rng.Niche`. Entering an event room now also marks the room type, so a fight-shaped event cannot be counted as an ordinary combat. |
| E9 | (same fix) the event sequence could run out | — | `RoomSet.NextEvent` is `events[visited % count]`: a **ring**, not a list. `EnsureNextEventIsValid` also skips events the run has already SEEN, not just disallowed ones. Neither was modelled. |
| E10 | `battle.enemies` intent step 18: 14 live, 11 here | `4KJ7X2MQND` | **CorpseSlug's WHIP_SLAP was folded into a single 6** rather than `MultiAttackIntent(3, 2)`. Identical while the slug has no Strength — and Ravenous is how it gets some. The game adds Strength to EACH hit. |
| E11 | `battle.enemies` intent step 18: 14 live, 16 here | `4KJ7X2MQND` | **Ravenous applied 5**, the A9 branch of `GetValueIfAscension(DeadlyEnemies, 5, 4)`, as a bare literal. At A8 it is 4. Exactly the bug class `Ascension.cs` was written to prevent. |
| E12 | `battle.enemies` intent step 24: game announces Attack 8, emulator announces no attack | `4KJ7X2MQND` | **Intent order.** Every `MoveState` declares its attack FIRST and the debuff/buff second, and the readout follows. Sludge Spinner's OIL_SPRAY and Living Fog's ADVANCED_GAS were modelled as a "Debuff carrying damage", so an agent reading intent types saw a debuff where the game shows an attack. Living Fog's `Smoggy` was also never applied. |
| E13 | `event.event_id` step 115: game `AROMA_OF_CHAOS`, emulator `SUNKEN_STATUE` | `DPUJR117FL` | **Neow's room consumes an event slot.** `MapPointType.Ancient` maps to `RoomType.Event`, and `RunManager` marks every room it enters — so `eventsVisited` is already 1 before the first real event. `NextEvent` is `events[visited % count]`, so the first event a run meets is `events[1]`. We started the cursor at 0. Confirmed by replaying into the live game and reading `events_visited` off the save. |
| E14 | `player.deck` step 118: game took Stone Armor, emulator took Expect a Fight | `DPUJR117FL` | **An event transforms off its own stream.** `CardCmd.TransformToRandom` takes the rng to roll with, and every EVENT passes its own `base.Rng`. Only New Leaf — which does not use the deck-selection path — uses `Rng.Niche`. We rolled all of them off Niche. |
| E15 | `player.hp` step 230: 24 live, 0 here (the run ends) | `DPUJR117FL` | **Fairy in a Bottle unmodelled.** An Automatic potion whose `ShouldDie` returns false for its owner; `AfterPreventingDeath` heals `Max(MaxHp × 0.3, 1)`. Death now resolves through one `PlayerIsDead` helper instead of four scattered `PlayerHp <= 0` checks. |
| E16 | sweep turn 4: player hp 53 live, 45 here | `QS2GYXRKWN`, `--encounters living-fog --play` | **The Living Fog's bomb takes a slot in FRONT of it.** A live capture lists `[Gas Bomb, Living Fog]`; we appended, so the fog came first and the same target index named a different creature on each side. The HP consequence is **not confirmed** — `combat_sweep --play` derives each action from the emulator's own state, so changing the roster order walks the two sides out of step. |
| E17 | `state_type` step 73: game `event`, emulator `rewards` | `41TJ3T2Y0Q` | **Brain Leech's reward belongs to its event.** Once the reward screen empties the run returns to the event's finished page, like Neow's. The check for that sat BELOW the reward-screen return, which exits first, so it never ran. |
| E18 | `state_type` step 1: game `rewards`, emulator `card_reward` | `CF32ERF3DH` | **Lost Coffer** is a `RewardsCmd.OfferCustom` of TWO rewards — a three-card CardReward and a PotionReward — so both sit on a SCREEN to be claimed. We handed the card reward straight over. The potion is guaranteed, not rolled, so it no longer spends 0.1 of the odds. |
| E19 | `battle.enemies` intent step 68: game announces a Stun, emulator announces Attack 12 | `CF32ERF3DH` | **Ravenous's stun did not reach the readout.** When an ally dies the survivor takes Strength and skips its next move; the intent changes immediately. We applied both buffs and left the old intent up, telling an agent to expect 12 damage from a creature about to sit the turn out. |
| E20 | `battle.enemies` step 69: game has 2 enemies, emulator has 1 | `QD1DQCJU2K` | **Seapunk's normal variant.** `SeapunkWeak` is one Seapunk; `SeapunkNormal` is a Calcified Cultist AND a Seapunk. Two encounters in the game, one entry here — like `CorpseSlugs` — so the variant must come off the weak flag, and did not. |
| E21 | `battle.enemies` step 111: split gremlins 15/18 live, 12/17 here | `WK1DEGZD8P` | **Reinforcements rolled off the wrong stream.** `CombatState.CreateCreature` calls `SetUniqueMonsterHpValue` for EVERY enemy it makes, spawned or not: HP comes off `Rng.Niche` and must differ from the other enemies on that side. The Gremlin Merc's two reinforcements used the combat rng with no uniqueness. |
| E22 | `state_type` step 23: game `monster`, emulator `map` — the run stalls on the map (was **O1**) | `KFMKQQA7MS` | **Winged Boots' free travel was unmodelled.** `MapTravel.GetTravelablePointsFrom` answers with `GetPointsInRow(row + 1)` — the WHOLE next row — while `Hook.ShouldAllowFreeTravel` holds, and `WingedBoots.ShouldAllowFreeTravel` is `!IsUsedUp`. `AfterRoomEntered` spends a charge only when the node just entered was not a child of the one left behind, so a run can hold the relic for a whole act without moving its counter. The map was right all along: all 77 edges matched. Two details that are not guesses: the boss is NOT in the game's `Grid`, so `GetPointsInRow` never returns it and `NMapScreen` makes it travelable outright from the last grid row instead; and the counter belongs on the RELIC (`[SavedProperty] TimesUsed`) — `RunState.WingedBootsTimesUsed` existed, was read by nothing, and is gone. |
| E23 | `player.hand` step 125: a fight's opening hand differs (was **O2**) | `WK1DEGZD8P` | **A combat the enemy phase ended kept playing.** `CombatManager.ExecuteEnemyTurn` awaits `CheckWinCondition` after EVERY enemy and returns the moment `IsInProgress` goes false; the emulator checked only at the very end of `EndTurn`, so it first ran a whole player turn that never happens — drew a hand, and reshuffled a 13-card discard pile to find one. `Rng.Shuffle` is a RUN-level stream, so those twelve draws left it ahead of the game's for the rest of the run and every later fight was dealt from the wrong position. The fight that exposed it was three floors later; the fight that caused it had ended, at floor 12, when the last Fat Gremlin took its Heist gold and fled on the enemy turn. Player death is the same rule and was the same bug: a fight that killed the player kept taking turns, which is how a seventh-turn relic fired in a fight that ended on the fourth. |
| E24 | `player.hp` step 132: 61 live, 58 here | `WK1DEGZD8P` | **Suck fired per hit.** `SuckPower.AfterAttack` runs once for the whole `AttackCommand` and grants `Amount x` the number of hits that dealt unblocked damage — so the Strength it hands over cannot reach the later hits of the attack that earned it. Applying it inside each hit gave a Fossil Stalker's two-hit Lash a second swing three points higher than the game's. Every multi-hit enemy attack now goes through one `DealAttack(enemy, state, damage, hits)` that counts the landed hits and triggers Suck after. |
| E25 | `player.block` step 137: 3 live, 5 here | `WK1DEGZD8P` | **Fossil Stalker's TACKLE never applied its Frail.** TACKLE_MOVE is `SingleAttackIntent` plus `DebuffIntent`, so the readout calls it an Attack — but the emulator resolved it in the DEBUFF branch, which its Attack intent never reaches. The Grasping Vines class again (E12), and the tell is the same: a move that attacks AND does something else resolves where its PRIMARY intent says. Every Defend after it blocked five where the game blocked three. |
| E26 | `battle.enemies` step 161: game `[22, 19]`, emulator `[19, 22]` | `WK1DEGZD8P` | **A summoned rat always joined the front.** `TwoTailedRat.CallForBackup` takes `Slots.LastOrDefault(s => no creature holds s)`, and the rats start in `Slots[2..4]` of five — so with the pack intact the last free slot is "second" and the newcomer really does lead. Once a rat has died its slot is free again: with "fifth" empty the newcomer joins the BACK. Hardcoding the front swapped the two survivors, and every target index after it named the other creature. `EnemyState.Slot` now carries the encounter slot, and the roster is ordered by it. The old guard (refuse when the roster already holds five rats) counted the DEAD, which the game's slot search does not. |
| E27 | Flame Barrier retaliates against nothing that has a special case | source read, no capture | **The retaliation was in the attack branch's generic tail**, past eighteen `break`s and past the multi-hit path's own. So it answered only single-hit attacks by monsters with no special case: zero against any multi-hit intent, and zero against a Snapping Jaxfruit, a Sludge Spinner, a Vine Shambler or a Fogmog either. `FlameBarrierPower.AfterDamageReceived` is a hook on the DAMAGE, so it belongs where Thorns already was — inside the per-hit helper. Three details came off `CreatureCmd`, which is the only place the hook is raised: it fires **per DamageResult**, it fires **whether or not block absorbed the hit** (the neighbouring `AfterCurrentHpChanged` is guarded on `UnblockedDamage > 0` and this one is pointedly not), and it is **skipped when the blow killed its target** (`!WasTargetKilled \|\| !IsDead`) — so a player who dies to the hit does not retaliate, and one a relic revives does. |
| E28 | Punch Construct's FAST_PUNCH applied no Frail | source read, no capture | **A rider behind the multi-hit break.** FAST_PUNCH is `MultiAttackIntent(FastPunchDamage, FastPunchRepeat)` plus a `DebuffIntent`, and the emulator's intent declares `Hits: 2` — so it took the multi-hit path, which `break`s before every per-monster rider. The construct's signature move dealt the right damage and left the player undebuffed, which reads downstream as blocking too much on the turn after. The break is gone: the generic path takes `Math.Max(1, Hits)` and the riders belong to the attack as a whole. Audited rather than assumed — of the 24 riders past it, this was the only one whose move was multi-hit; KinPriest, Lagavulin Matriarch, Vantom and Fossil Stalker all have theirs on single-hit moves. |
| E29 | `player.gold` step 27: 114 live, 108 here — the first combat reward of the run | `1UL0BRX8WC` | **Phial Holster's potions rolled off the Rewards stream.** `PhialHolster.AfterObtained` is `GainMaxPotionCount(1)` and then `CreateRandomPotionsOutOfCombat(2, Rng.CombatPotionGeneration)`; the emulator drew both from `PlayerRng.Rewards`, which is the stream every card reward, shop and transformation in the run also reads. Two draws the game never makes there put everything downstream at the wrong position. Its `+1` potion slot was unmodelled too, so the owner could carry two where the game gives three — `max_potion_slots` in the capture reads 3 for this run and 2 for the other nine, which is also what settles the BASE: `Player.initialMaxPotionSlotCount` is 3, but at A8 every capture says 2, so the decompiled constant is the un-ascended one. |
| E30 | (same seed) gold still 106 after E29 | `1UL0BRX8WC` | **A four-draw fudge on top.** `AdvanceRewardRngForNeowRelic` burned 4 Rewards calls for Phial Holster "to keep the stream aligned" — the same shape as the 18 Kaleidoscope used to burn. With the potions moved to their real stream the fudge was all that was left, and it put the first combat's gold at stream position 5 where the game had it at 1. Removed. **The other two rows stay and are a standing debt**: Hefty Tablet and Lead Paperweight really do draw from Rewards (`CardFactory.CreateForReward`) while the emulator rolls their cards off `Rng.UpFront`, so their counts stand in for real draws — and neither has a capture to check against. |
| E31 | `player.energy` step 97: 2 live, 1 here | `1UL0BRX8WC` | **The opening hand skipped Slither's cost roll.** `Slither.AfterCardDrawn` re-rolls its card's cost off `Rng.CombatEnergyCosts` on every draw INTO HAND, and the opening hand is a draw — but `CombatFactory` deals it with a bare `Hand.Add(DrawPile[0])`, so an enchanted card in it kept its printed cost. A Wood Carvings run enchanted a Bash on floor 8 and opened the floor-9 fight paying 1 for it; the emulator paid 2. Worth knowing why it hid: the replay compares the deck as `(id, upgraded)` pairs, which an enchantment does not change, so an unapplied enchantment is invisible there and surfaces as energy three steps later. |
| E32 | (same fix) `combat_energy_costs` restarted every fight | `1UL0BRX8WC` | **The one named stream that was neither fast-forwarded nor written back.** Every other stream is built by advancing a fresh `CountingRandom` to the run stream's `CallCount` and then advanced back at combat end; `CombatEnergyCosts` did neither, so each combat re-read it from position 0 and two Slither draws in two fights returned the same value where the game returns consecutive ones. Found by inspection while chasing E31 rather than by the capture — the capture's only Slither draw is at position 0, where both models agree. |
| E33 | `state_type` step 90: game `monster`, emulator `rewards` — the emulator wins a fight the game is still fighting | `25TS4F5T37` | **A Fogmog's Eye With Teeth cannot be killed, and the emulator killed it.** `IllusionPower` is three rules the emulator had none of: `ShouldCreatureBeRemovedFromCombatAfterDeath` is false, so the eye stays in the roster; `AfterDeath` forces a REVIVE_MOVE with `MustPerformOnceBeforeTransitioning`, so its next turn is spent healing to full rather than acting; and `ShouldPowerBeRemovedOnDeath` keeps its buffs — IllusionPower included — through the death, so it does it again, forever. What made the damage compound is target resolution: with the eye dead in the emulator and alive in the game, every blow the live run spent on the illusion resolved against the emulator's first LIVING enemy, which was the Fogmog. It died floors early. |
| E34 | (same fix) a fight could end on the wrong body count | `25TS4F5T37` | **The emulator had no notion of a secondary enemy.** `Creature.IsPrimaryEnemy` says it outright — "a secondary enemy will automatically die unless there's also a living primary enemy" — and carrying `MinionPower` or `IllusionPower` is what makes one. Every all-dead check counted the whole roster instead, which is wrong in both directions at once: a Fogmog's eye revives forever, so a fight it outlived could never be won; and a Gas Bomb left standing after its Living Fog died would hold a finished fight open. |
| E35 | (same fix) ILLUSION re-summoned and the re-summon ate the revive | `25TS4F5T37` | **A guard standing in for the missing mechanic.** The emulator's ILLUSION branch ran only when no eye was alive, sweeping away any dead one first — which is a fair approximation of "the eye comes back" and becomes actively wrong once the eye really does. It deleted an eye in the middle of reviving and inserted a fresh one at the front of the roster, moving the creature a target index names. `ILLUSION_MOVE` is the move machine's INITIAL state with nothing leading back to it: it fires once per combat and never again. The same shape as E30 — a compensation that outlived what it compensated for. |
| E36 | `battle.enemies` step 78: block 8 live, 16 here | `SAM9XS24LM` | **A Sewer Clam gained its Plating block twice.** `PRESSURIZE_MOVE` is `PowerCmd.Apply<StrengthPower>(4)` and nothing else — the block the emulator also gave it on that turn was invented. The clam's block comes from `PlatingPower` alone, which grants it to every owner at the end of its side's turn, so the buff turn paid out twice. |
| E37 | (same seed) block 9 live, 8 here on the first turn, then 8 live and 9 here | `SAM9XS24LM` | **Plating decayed a turn early, and in the wrong order.** `PlatingPower` decrements on `AfterSideTurnStart` and grants its block on `BeforeSideTurnEndEarly`, so the block a turn ends with is the ALREADY decremented amount — and `AfterSideTurnStart` skips the decrement entirely for enemies on round one. The emulator granted first and decremented after, which is a point of block ahead of the game for the whole fight. Watch the counter: `CombatState.Turn` counts from ZERO, so the first enemy phase is Turn 0 and a `> 1` guard puts the decay a turn late — which is how the first attempt at this fix traded one off-by-one for another. |




---

## Fixed — harness

These produced no wrong emulator behaviour but hid or misattributed real bugs.
They are catalogued because each one cost more than the engine bug it concealed.

| # | Gap | Consequence |
| --- | --- | --- |
| H1 | The replay compared `state_type` but never WHICH event | Two runs sitting in different events both read `"event"`, so an Underdocks divergence stayed invisible for 60 steps and surfaced as a gold mismatch. `event.event_id` is now a default compared field. |
| H2 | Target ids were treated as stable | The game **renumbers** entity ids as enemies die — four gardeners are `_0.._3`, and once the first dies the survivors become `_0.._2`. The map was built once on entering a fight, so a replay quietly attacked the wrong enemy for the rest of it. Values are now ordinals among living enemies, resolved against the emulator's own list. |
| H3 | Hands were compared by card id alone | An upgrade does not change a card's id, so Defend and Defend+ compared equal. A lost upgrade surfaced three steps later as HP — which reads like a damage bug, and **I misdiagnosed it as one**, blaming an enemy whose damage was correct. With `(id, upgraded)` the same bug reports at step 63 instead of 77, naming the card. |
| H4 | The snapshot carried far more than it compared | Block, energy, max_hp, relics and potions were summarised and thrown away, and the deck was not summarised at all. Two engine bugs (E10, E11) surfaced on the first run of the widened comparison. |
| H5 | `validate_real_game_trace.live_enemy_intent` special-cased two monsters | It returned Debuff when the capture plainly said `Attack '8'` — the expected value **bent to agree with the emulator**, which is the one thing a live fixture must never do. Removed, and the fixture re-captured rather than hand-edited. |
| H6 | `combat_sweep --play` derives actions from the emulator's state | Any change to the emulator's roster order walks the two sides out of step and the capture fails outright. Needs the H2 treatment: resolve targets by what they name. Currently blocks confirming E16. |
| H7 | The target map spelled a hyphenated enemy differently from the game | `build_target_map` slugified the display name by folding only WHITESPACE, so "Two-Tailed Rat" came out `TWO-TAILED_RAT` where the game's entity id says `TWO_TAILED_RAT`. The lookup missed — and `translate_target` does not fail on a miss, it falls back to the entity id's numeric SUFFIX, which is the position among LIVING creatures and stops matching the emulator's index the moment anything dies. That is precisely the trust H2 removed, restored by a hyphen. The map now keys on the capture's own `entity_id` as well, which needs no transcription at all. |
| H8 | The tracer treated an ACCEPTED action as a done action | The retry loop only ever caught an action the game REFUSED. The other failure strands the capture: `proceed` from a shop came back `ok`, the map screen really did open with the right options on it, the travel vote registered in the game's own log — and no room ever loaded. The state settles on `unknown`, every later action is refused, and the capture ends holding a run that is still alive. It is a race, not a rule (the committed traces all leave their shops fine), which is why it needed handling rather than avoiding. `recover_stranded_run` now nudges with a `proceed` and re-posts until the run has actually moved, recording nothing until it takes. The first capture taken with it needed six attempts at exactly that step and would otherwise have died on floor 5. |
| H9 | The abandon crash wedged the game, and the harness blamed the clock | `SaveManager.DeleteCurrentRun` deletes `current_run.save.backup` unconditionally and `CloudSaveStore.DeleteFile` THROWS when it is absent. The exception escapes `NAbandonRunConfirmPopup.OnYesButtonPressed` half way through: the run is gone from disk but the main menu never finishes coming back, reporting `menu_screen: main` with no enabled buttons, forever. Every capture after that dies on "Timed out waiting for menu screen 'main'" — a message that had already sent one investigation after the lobby instead of the state it names (see HANDOFF's embark notes). The workaround was known and written down as a manual step, which is exactly how it stayed a gotcha; `ensure_run_save_backup()` now does it before every abandon. `wait_for_menu` also went from 10s to 60s, because an abandon that saves and then preloads 126 assets does not reliably fit in ten. |


---

## Patterns worth remembering

**Compare contents, not containers.** H1, H2, H3 and H4 are the same mistake four
times: comparing `state_type` without which event, enemy slots without which enemy,
card ids without which version, a summary without the fields it carried. Each let a
real divergence sit silently until it surfaced somewhere unrelated.

**A check that passes can still be scoped wrong.** E20 slipped past the encounter
sequence comparison because `encounter_names()` normalises the `WEAK`/`NORMAL`
suffix away *on purpose* — for most encounters the split really is one roster. E21
slipped past the deck comparison because it is a multiset.

**Anything left to a default is a silent allow.** E5 is the archetype:
`IsEventAllowed` ends in `_ => true`, so an untranscribed rule does not fail, it
quietly permits. `generate_event_gating_coverage.py` now makes an untranscribed
rule a build failure.

**Neow is not a thing.** All 28 of its options are `RelicOption<T>` — obtain the
relic, then `Done()`. E3, E17 and E18 all presented as "Neow bugs" and were all
relic-effect bugs. `MassiveScroll` is in the game's `PositiveOptions` but its
`IsAllowed` is `Players.Count > 1`, so leaving it out of a solo run is correct.

**The soak and the suite catch different things.** E7 was a crash the 1666-test C#
suite could not reach, because it needs two draws from one event in one run.

**The stream is a run-level object, so the fight that pays is not the fight that
spends.** E23 is the archetype: twelve extra draws in a floor-12 fight surfaced as a
wrong opening hand on floor 14, and every read of floor 14 was a read of the wrong fight.
Anything that shares state across combats — `Rng.Shuffle`, `Rng.Niche`, `monster_ai` —
has to be measured where it is spent, not where it hurts.

**A turn the game never takes is not free.** The same entry: the emulator "just" played
out one extra player turn after a fight was already over. It drew cards, so it reshuffled,
so it moved a run-level stream. Any code path that continues past a terminal state is a
divergence in waiting, whatever it looks like it costs.

**A green fixture set measures the fixtures, not the emulator.** The nine traces went
clean, and the tenth — taken for no reason except that it would hold a relic none of them
held — diverged on its first combat reward and produced four defects. Three of those had
nothing to do with the relic; they sat on paths no earlier capture happened to walk. When
the set goes green the honest reading is "these runs are exhausted", and the next capture
is worth more than any amount of re-reading them.

**A fudge factor outlives the thing it was compensating for.** E30's four burned Rewards
calls were put there to paper over E29's wrong stream. Fixing the real defect left the
fudge behind, still wrong, now in the opposite direction — and the catalogue already had
the same story for Kaleidoscope's eighteen. Any "advance the stream by N to stay aligned"
is a debt with a name attached; write down which defect it is standing in for, so that
whoever fixes that defect knows to delete it.

**An early `break` is a scope decision nobody wrote down.** E27 and E28 are the same
line: `if (Hits > 1) { ...; break; }` silently decided that a multi-hit attack skips
everything below it, which included one monster's own debuff and the whole of Flame
Barrier. The branch that dealt the damage should not get to decide which effects the
damage has — anything that answers a hit belongs in the helper that lands the hit, next
to Thorns. When adding a `break` to a long `if` chain, the question is not "does my case
work" but "what did I just exclude".

**A rule read off one observation is a rule fitted to one state.** E26's "a summoned rat
goes to the FRONT" was true, and true for the reason given — it just happened to be the
answer only while all three starters were alive. `Slots.LastOrDefault` is the rule; the
front was one of its outputs. When a note explains a placement by naming slots, model the
slots.
