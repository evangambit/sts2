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

What the agent is handed each step. This surface is currently clean: combat
exposes `DrawPile.Count`, `DiscardPile.Count` and `ExhaustPile.Count` — sizes,
never order.

The risk is in closing the known gap. The run layer's observation carries
`Deck.Count` but not the deck's contents, which makes card rewards, shops, rest
upgrades and transforms unlearnable. Closing that must mean **composition, not
order**: the multiset of cards in a pile, plus the parts of the order the player
legitimately knows (below). Dumping `DrawPile` as an ordered list would close one
gap by opening a worse one.

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

- **Persistent identity** — `DefId`, `Upgraded`, and the enchantments `Sharp`,
  `Nimble`, `Swift`. This is what a deck-level representation needs; it is what a
  card-reward or shop decision is about.
- **Combat-local mutation** — `BonusDamage` (Rampage grows per copy),
  `CostForCombat`, `FreeThisTurn`, `Retain`. Only the in-combat representation
  needs these.

Enchantment magnitudes are not always 2: Self-Help Book grants 2, other sources
vary, so they are small integers rather than flags.

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
