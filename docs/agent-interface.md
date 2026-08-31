# The Agent Interface — Design

How the emulator presents itself to a learning agent, and the rules that keep a
bit-exact simulator from turning into an oracle. This covers determinism,
hidden information, state cloning for search, and what the observation owes the
network. It is a companion to [replay-verification.md](replay-verification.md),
which covers the opposite obligation: reproducing the game exactly.

## The tension

The emulator has two jobs that pull in opposite directions.

- **Verification** wants exact reproduction. Every RNG stream is seeded from the
  run seed and advances in lockstep with the game, which is what lets a captured
  act replay step-for-step and turn any divergence into a localized bug.
- **Training and inference** want an agent that cannot see the future. The same
  determinism that makes verification possible makes the whole run derivable
  from one seed.

The resolution: **there is one engine and it stays deterministic.** Randomness is
never baked into the simulation. It enters at exactly two boundaries, both
explicit, and verification calls neither.

1. `reset(seed)` — training samples the run seed from its own RNG.
2. `clone(resample_hidden=True)` — search forks a state and resamples everything
   the agent has not been shown.

## Two leak surfaces

Determinism can leak to the agent in two different places, and they need
different fixes.

### The observation

What the agent is handed each step. Combat exposes `DrawPile.Count`,
`DiscardPile.Count` and `ExhaustPile.Count` — sizes, never order.

This surface was described here as clean while it was not. Alongside the map's
node types the run block carried a second array of the same width holding the
**encounter behind each choice**, read straight off
`NormalEncounterSequence[NormalEncountersVisited]` — so a policy standing on the
map was told which fight the next monster node held before it picked one. That is
the whole of the decision a monster row asks, and the game never shows it: you
learn what is in a room by walking into it. The block is gone
(`MapChoiceObsOffset` with it), the run observation is seven integers narrower,
and `Sts2Run_ObsLayout` no longer publishes an offset by that name.
`State.MapChoices` is untouched and still resolves the encounter when a node is
actually entered — it was only ever the observation that had no business with it.

Worth recording how it survived: the map's node types and its encounters were
written by the same two-line loop, and a leak that sits beside something
legitimate reads as part of it. **The node types are correct to expose** — the
game's map draws them as icons — and their neighbour was never questioned. What
found it was a person playing the CLI and noticing the screen had named the
monsters before the room was entered, which is the argument for having a reader
that shows a state to somebody rather than only to a network.

The act's BOSS is the one thing on the map that is named, because the game names
it from the moment the act opens. It is not in the observation either; screens
that want it read it through `Sts2RunEnv.map_graph()`, and whether a policy
should be handed it is a separate question from what a map screen draws.

The run layer used to carry `Deck.Count` and nothing else, which made card
rewards, shops, rest upgrades and transforms unlearnable — the agent chose
between three cards without being told what the other twenty in its deck were.
It now carries the deck card by card and the relics relic by relic. What it does
**not** carry is any pile's order: the deck list is composition, and the piles
stay counts.

The deck block is in `State.Deck` order, deliberately, because a card-select
screen's action `i` indexes the same list — sorting it into a canonical multiset
would read more tidily and leave the agent unable to say which card it meant.
Nothing leaks by keeping that order: a deck is inspectable in full in-game. The
draw pile is the opposite case, and dumping it as an ordered list would close
one gap by opening a worse one.

### Search

The larger surface, and the less obvious one. A faithful clone hands a tree
search more than the draw order: because every run-level stream derives from the
run seed and its call count, a search that rolls forward can read the exact card
rewards a fight will drop, the shop's stock, and the composition of an encounter
it has not entered yet. That is an oracle over the entire future, not a peek at
the shuffle.

So the clone API is not a memcpy. It takes a `resample_hidden` flag that
re-seeds every stream whose output the agent has not yet observed, and reshuffles
the unknown region of the draw pile. A search node then explores _a_ plausible
world rather than _the_ world.

## What the player actually knows

The model of hidden information, which both the observation and the resampling
have to agree on.

The player knows:

- The **composition** of every pile — draw, discard, exhaust. In-game these are
  all inspectable.
- The **identity and position of cards deliberately placed**: a card put on top
  of the draw pile is known to be on top, and stays known until it is drawn or
  the pile is shuffled. Same for the bottom.
- Nothing else about draw-pile order.

Everything else about the future — the next shuffle, reward rolls, shop stock,
encounter composition — is unknown, and stays unknown.

The Crystal Sphere is the same rule on a grid. Its board is fifteen items buried
under 11×11 of fog, and the game names an item — footprint and all — the moment
any one of its cells clears. So an item with a cell showing is known, an item
with none showing is not, and a resampled clone moves exactly the second kind
(`CrystalSphereGame.ResampleUnseenItems`). Without that, a search could read
where the relic is and divine straight onto it.

### Known-order tracking

`DrawPile[0]` is the top: draws take from index 0, `Insert(0, …)` top-decks,
`Add(…)` bottoms. The pile is a single ordered list with no notion of what the
player has seen, so the state carries two counters:

- `KnownTopCount` — the first N cards of `DrawPile` are known, in order.
- `KnownBottomCount` — the last M cards are known, in order.

Their maintenance rules:

| Event                        | Effect                                            |
| ---------------------------- | ------------------------------------------------- |
| Any shuffle of the draw pile | both reset to 0                                   |
| Card placed on top           | `KnownTopCount++`                                 |
| Card placed on bottom        | `KnownBottomCount++`                              |
| Card drawn from the top      | `KnownTopCount--` (floored at 0)                  |
| Card removed from the middle | the counter on that side shrinks if it covered it |
| Pile empties and refills     | both reset to 0                                   |

The two regions never overlap: their sum is clamped to the pile size.

### Resampling the unknown

`clone(resample_hidden=True)` reshuffles **only** the region between the known
prefix and the known suffix, and re-seeds the streams that have not yet paid out.

The reshuffle uses a plain uniform shuffle — deliberately **not** the game's
`StableShuffle`, which sorts by `ModelId.Entry` before Fisher–Yates to reproduce
the game's exact order. Reproducing the game's order is the opposite of what a
determinization wants; the point is to sample a plausible world, not the real
one.

## What the observation owes the network

### Card identity

Card ids are nominal, not ordinal — card 473 is not "one more" than card 472 —
so they belong in columns an encoder can embed, not in an undifferentiated float
vector.

`CardInstance` splits in two, and the split matters for how much each consumer
needs to carry:

- **Persistent identity** — `DefId`, `Upgraded`, and the card's one
  `Enchantment` with the `EnchantAmount` it was applied at. This is what a
  deck-level representation needs; it is what a card-reward or shop decision is
  about, and it is what the run observation's deck block carries.
- **Combat-local mutation** — `BonusDamage` (Rampage grows per copy),
  `CostForCombat`, `FreeThisTurn`, `Retain`. Only the in-combat representation
  needs these, and the deck block leaves them out.

Enchantment magnitudes are not always 2: Self-Help Book grants 2, other sources
vary, so they are small integers rather than flags. The combat observation's
hand slots still carry only `DefId` and `Upgraded`, so an enchanted card in hand
is indistinguishable from a plain one mid-fight — a known gap, and a smaller one
than the deck was.

Relics carry their `Counter` and whether the run has spent them: a Silver
Crucible with three charges is a different relic from one with none, and a
used-up relic stays in the list doing nothing.

### The shop

The merchant's board is a block of its own, one slot per thing on sale, indexed
by the action that buys it: seven cards, three relics, three potions, then the
card-removal service at 13. Each slot carries what is on it and what it costs.

Before this, three of the seven cards were in the observation and none of the
prices — so an agent could buy shop slot 5 without ever being shown what was on
it, and could not tell a 50-gold card from a 300-gold one on any slot, which is
the whole decision a shop is. A sale needs no flag of its own: the game halves
the slot's price rather than marking it, so the discount is already in the
number.

### Sizes and truncation

The deck block is 64 slots and the relic block 32, both well past what a full
four-act run reaches; the shop block is exactly the merchant's 14 actions. A run that overran either would have its later entries
unseen — but `Deck.Count` and `Relics.Count` still report the real sizes, so the
truncation is visible rather than silent. `Sts2Run_ObsLayout` reports where the
blocks sit so a consumer does not hard-code offsets that move when a block
grows.

### Action width

The run's action mask is 256 wide. It was 32, which was enough while the widest
screen was a shop and silently wrong past that: the mask setter drops anything
that does not fit, so a deck grown past 32 cards had its later cards
unselectable at a card-select screen without a word. The Crystal Sphere forced
the issue — its board is 121 cells and each may be divined with either tool, so
242 actions — and a mask that cannot express a screen is a worse bug than a wide
one.

The sphere's tool is folded into the action (0..120 big, 121..241 small) rather
than set by an action of its own. Switching tools costs the game nothing, and a
free action is a cycle an agent can ride forever.

**The sphere's board is deliberately not in the observation.** The phase is
modelled and the mask is correct, so an agent can play it and will divine
blind — which is a decision, not an oversight: divining well is worth little
over a crude heuristic, and the decision that actually matters, which card to
take from what the fog gave up, happens afterwards on the reward screen where
the agent can see. Backlogged rather than built.

### Action identity

An action's _type_ — play card, end turn, use potion, choose reward — is
currently only recoverable by knowing the hand size at that instant, because
`end_turn` is `hand_count` and potions follow it. Whether the policy head wants
fixed slots or an embedding over action types is a downstream modelling choice,
but either way the engine should emit the type alongside the index, so neither
head has to reconstruct it.

## Consequences for verification

None of the above weakens the differential harness, by construction:

- The engine stays deterministic, so a captured act still replays step-for-step.
- Resampling is opt-in and off by default, so nothing in the test path touches it.
- Known-order tracking is bookkeeping beside the pile, not a change to it; the
  order the game produces is untouched.


---

## What the interface actually measures (`scripts/agent_probe.py`)

The sections above are design. This is what the interface *does* when something walks
it, and the numbers are here so later emulator work has a target rather than a feeling.
Re-run after any change to the observation, the action encoding or the step path:

    uv run python scripts/agent_probe.py

Findings worth carrying, from the first run:

**The simulator is not the bottleneck people assume, and it is also not as fast as a
single microbenchmark suggests.** Timing `run_step` with a fixed action mostly measures
REJECTED actions — the mask says no, it returns in ~2us, and you conclude the simulator
runs at 500k/s. Timing a loop that resets folds whole-run generation into the average
instead. The honest figure is a per-phase mixture: **~72us/step overall, about 14,000
steps/s on one env**, with combat around 54us median and act entry carrying a long tail
because it generates a map. PLAN.md's AlphaZero target is 1e5–1e6 transitions/s/core, so
the step path is **roughly an order of magnitude short** and that is where the work is.

**Clone is the cheap half of search**, ~59,000/s including hidden-state resampling — so
a tree search's ceiling is set by the step, not by forking. The handle pool caps
CONCURRENT runs at 256, which is fine for clone-simulate-destroy and not fine for a
design that holds a handle per tree node.

**Card ids reach the network as raw integers.** Nothing in the observation says slot N is
categorical, so a network reading it as a magnitude learns that card 473 is *more* than
card 472. That is an embedding on the agent side, but it is the observation's shape that
forces the issue, and it is worth stating here rather than rediscovering it in a training
run.

**About a fifth of the observation is ever non-zero** under random play. Some of that is
genuinely dead width and some is screens a random policy never reaches — those are worth
telling apart before a network is sized around 630 inputs.

**A dozen action indices are legal in more than one phase.** The mask keeps that safe, but
it means one output neuron means different things on different screens, so the phase has
to be prominent in the observation for the network to disambiguate.


## What the search path measures (`scripts/mcts_probe.py`)

A throwaway determinized MCTS — shallow tree, floor-count value, nothing trained —
walked hard enough to answer what the design above could not. Re-run after any change to
the clone API or the step path:

    uv run python scripts/mcts_probe.py

**The clone contract holds.** All three invariants a tree search depends on pass:
stepping a clone leaves the parent untouched; two un-resampled clones given the same
actions end in the same state; and 24 determinizations produce 20 distinct futures, so
`resample_hidden` genuinely re-seeds rather than quietly copying. That is worth having
checked, because a clone that silently shared state with its parent would look like a
search bug for a long time.

**The handle pool takes 255 concurrent clones.** Clone-simulate-destroy never approaches
it. A design that holds a handle per tree node is two orders of magnitude past it, so the
tree has to be Python-side and the world replayed into a clone per simulation — which is
what this probe does.

**A simulation costs ~714us, not one step.** It is clone + descend + roll out, about 16
steps deep, so **~1,400 simulations/s** and ~22,000 steps/s inside the search. The step
path is the ceiling: clone is ~17us of that and the remaining ~700us is simulation. This
is the concrete reason to work on step cost rather than on forking.

**The path works end to end, and it scales with search.** Against random play on the same
seeds, MCTS reaches deeper floors:

| simulations per move | random | search | lift |
| --- | --- | --- | --- |
| 40 | 3.5 mean | 5.2 mean | +1.7 floors |
| 120 | 3.4 mean | 8.0 mean | +4.6 floors |

That is not a claim about the agent — the value function is "how far did the rollout
get". It is a claim about the plumbing: the clone, the mask, the value and the backup are
all connected, and more search buys more floors, which is what a working search does.
Cost at 120 sims/move is about 6s of wall clock per run.


## Where a step's time actually goes (`StepCostProbe`)

    dotnet test src/Sts2Emulator.Tests --filter StepCostProbe -c Release
    cat /tmp/sts2-step-cost.txt

The probe is skipped by default and lives in the test assembly rather than in `scripts/`
because the number that matters is `GC.GetAllocatedBytesForCurrentThread`, which only C#
can see. **A step that allocates kilobytes is paying a GC bill no algorithmic tidying
will refund**, and that is the shape of the problem here.

| scenario | time | alloc |
| --- | ---: | ---: |
| `WriteObservation` | 1.3 us | 0 KB |
| end turn, baseline | 79.5 us | 14.6 KB |
| end turn, **combat already over** | **2.7 us** | **1.0 KB** |
| end turn, one enemy instead of two | 75.7 us | 13.1 KB |
| end turn, draw pile stocked (no reshuffle) | 75.5 us | 14.6 KB |

Reading down the table rules out three suspects and leaves one:

- **Not the observation.** 1.3us against a ~72us step; 1.3% of it.
- **Not the enemy phase.** One enemy costs *more* than two. Whatever this is does not
  scale with the number of creatures acting.
- **Not the reshuffle.** Stocking the draw pile saves ~4us and allocates identically.
- **It is the START OF THE NEXT PLAYER TURN.** "Combat already over" is the only row that
  falls off a cliff — 2.7us and 1.0KB — and the only thing it skips is the next turn's
  setup. That path costs roughly **77us and 13.6KB per step**.

Two further pieces of scale, from `scripts/agent_probe.py`: combat is about **61% of a
run's total step time**, and within a combat, ending the turn costs about **12x playing a
card** (125us vs 10us). So the next-player-turn path is the single hottest thing in the
emulator, and it is a path that allocates.

Worth recording how this was found, because two plausible readings were wrong first. The
enemy phase looked like the answer until enemy count was varied; the reshuffle looked
like the answer until allocation was compared. **The measurement that settled it was
turning the next turn off**, not looking harder at what was on.


### What the allocation fix bought, and what it did not

Two allocations found by the table above, both in the enemy phase:

**`BuffSystem.Get` allocated on every call.** It read
`buffs.FindIndex(b => b.Id == id)`, and that lambda CAPTURES `id` — so a display class
and a delegate on every one of its 242 call sites, several of them per point of damage.
A hand-written loop over what is always a handful of entries costs less than the closure
did.

**`ShuffleDiscardIntoDraw` sorted by string through LINQ.** The pile is canonicalised by
card `Entry` before being permuted, so the same cards in a different pile order shuffle
alike — but `OrderBy(...).ThenBy(...).ToList()` builds a buffer, a key array and a
comparer chain each reshuffle, keyed on strings. It now sorts in place against a
precomputed int rank, with the original position in the low bits of the key so the sort
stays STABLE — two cards can match on Entry and Upgraded while differing in an
enchantment the key does not see, and an unstable sort would shuffle them differently.

| | before | after |
| --- | ---: | ---: |
| `ExecuteIntent`, attacking enemy | 3,080 B | **352 B** |
| `ExecuteIntent`, buff or unknown intent | ~570 B | **0 B** |
| end turn, whole step | 14.6 KB | **4.1 KB** |

**Wall-clock throughput did not measurably move** — 12,500 steps/s against 13,900 before,
which is inside the run-to-run spread. So the garbage was not what the time was going on
at this scale, and the honest reading is that this buys headroom for sustained parallel
training rather than a faster step today. A combat step is still ~56us median while
allocating 4KB, which means the remaining cost is compute and **has not been located yet**.
That is the next thread, and the table at the top of this section is how to pull it.

The 32 committed run traces are what makes this kind of change safe: any drift in shuffle
order or buff resolution breaks them loudly, and they stayed clean throughout.
