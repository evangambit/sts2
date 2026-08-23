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

| # | Metric | Seed / where | Cause | Blocked on |
| --- | --- | --- | --- | --- |
| O1 | `state_type` step 23: game `monster`, emulator `map` — the run stalls on the map | `KFMKQQA7MS` | **Winged Boots' free travel is unmodelled.** While `TimesUsed < 3` the relic lets the player move to ANY node on the next row, not only a child; moving to a non-child spends a charge. The map itself is right — all 77 edges match the capture — and `RunState.WingedBootsTimesUsed` already exists. Nothing reads it. | `RunConstants.MapChoices` is 4 and a full row is 7, so the choice arrays cannot hold the options. Widening it moves the run observation layout and the API version, and `sts2_gym/run_constants.py` **restates** the 4 rather than reading it from the emulator. |
| O2 | `player.hand` step 125: a fight's opening hand differs | `WK1DEGZD8P` | Unknown. Ruled out: the deck matches in contents **and order**, and both sides shuffle the same way — `CardPile.RandomizeOrderInternal` is an `UnstableShuffle` over the pile as it stands, not a `StableShuffle`, so deck order is the input and it agrees. What is left is the run-level `Rng.Shuffle` **position**: an earlier combat drew from it a different number of times. | Needs the shuffle call count instrumented across the whole run. Inspecting the fight where it surfaces will not find it. |

---

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
