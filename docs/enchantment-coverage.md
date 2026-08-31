# Enchantment coverage — what the emulator is missing

Enchantments are the per-card modifier orthogonal to upgrading: a card's identity is
`(DefId, Upgraded, Enchantment, EnchantAmount)`. The emulator models **13 of the game's
22**, and **4 of the 14 relics that grant one** (four more *read* enchantments rather
than granting them, and need the system to exist before they mean anything). This file is the gap, with the source to
port from.

**Why it is not act-2 work.** The implemented four granting relics all happen to be
`Ancient` rarity, which makes the mechanic look like something the act-2 ancients hand
out. It is not. Act 1 reaches enchantments by two ordinary routes:

- **Events.** Self-Help Book (Sharp/Nimble/Swift at **2**), Stone of All Time (Vigorous)
  and Symbiote (Corrupted) are in **both** act-1 event pools; Sapphire Seed (Sown) and
  Wood Carvings (Slither) are in Underdocks'. See `RunMapGenerator.cs:340`.
- **Shops.** Five `Shop`-rarity relics grant one and a sixth pays off having one,
  and there are three shops per act.

So an act-1 deck can carry enchantments from roughly floor 5 onward, and a deck-level
model trained on a distribution that structurally excludes them is biased in a way that
does not announce itself.

## What is already implemented

`Core/Card.cs` → `enum Enchantment`: `Sharp`, `Nimble`, `Swift`, `Steady`, `Spiral`,
`Sown`, `Corrupted`, `Slither`, `Vigorous`, `Imbued`, `Clone`, `Goopy`,
`TezcatarasEmber`, `Inky`, `Adroit`, `Momentum`, `RoyallyApproved`, `PerfectFit`,
`SoulsPower` (values 1–19; `None` is 0).

Granting relics: Pael's Claw (Goopy), Nutritious Soup (Tezcatara's Ember), Electric
Shrymp (Imbued), Pael's Growth (Clone at 4) — all in
`Core/Run/RunNonCombatEffects.cs:300`.

**Append to the enum, never insert.** The numeric value is what the observation carries,
so inserting renumbers every enchantment above it and silently invalidates any trained
policy and every committed fixture. Next free value is **20**.

## The three missing enchantments

Source: `decompiled/MegaCrit.Sts2.Core.Models.Enchantments/<Name>.cs`.

| Enchantment | Restricted to | Effect |
| --- | --- | --- |
| **Glam** | any | Card plays `Times` (=1) extra times. **Once per combat** — `AfterCardPlayed` sets `UsedThisCombat` and flips its own `Status` to `Disabled`. |
| **Instinct** | Attack | `EnchantDamageMultiplicative` returns **2×**, powered attacks only. |
| **SlumberingEssence** | any | `BeforeFlush`: if the card is in **hand**, `EnergyCost.AddUntilPlayed(-1)`. Stacks per flush while it sits in hand. |

Souls Power was the last of these to land, with Grave of the Forgotten. It is worth
noting on its own: it is the only enchantment whose `CanEnchant` reads a KEYWORD rather
than a card type, and the only one whose `OnEnchant` takes something away rather than
adding it. In the emulator that makes `CardInstanceExtensions.IsExhaust` its home, not the
per-play hooks the others use.

Two notes for whoever ports these:

- `IsPoweredAttack()` gates Inky, Instinct and Momentum. The unpowered branch returns the
  identity (`0m` additive, `1m` multiplicative), not the effect.
- Instinct is multiplicative and `Hook.ModifyDamage` carries a `decimal` with no rounding
  step, so the truncation rule the captures established applies (`6 × 1.75 = 10`).
  A `2×` never exposes it, but the same code path will carry the next one that does.

## The missing relics — ten granters, four readers

### `Shop` rarity — act 1 reachable, and the priority

| Relic | Id | Grants |
| --- | ---: | --- |
| **Gnarled Hammer** | 103 | Sharp at **3**, on pickup, onto chosen cards (`CardsVar`). |
| **Royal Stamp** | 224 | RoyallyApproved, on pickup, over every deck card passing `CanEnchant`. |
| **Wing Charm** | 292 | Swift at **1** — not on pickup: `TryModifyCardRewardOptionsLate` enchants a **card reward option**, so it changes what the reward screen offers. |
| **Punch Dagger** | 210 | Momentum at **5**, on pickup, one chosen card. |
| **Kifuda** | 125 | Adroit at **3**, on pickup, onto chosen cards (`CardsVar(3)`). |
| **Mystic Lighter** | 160 | Grants nothing — `ModifyDamageAdditive` gives **+9 unpowered damage** to any card that *has* an enchantment. Belongs with the readers below, but it is Shop rarity and act-1 reachable, so it is listed here. |

Four of these five granters need a card-select screen on pickup, which the run layer
already has (`BeginDeckSelection(state, DeckSelection.Enchant, …)` — see Electric Shrymp
and Pael's Growth for the shape). Wing Charm is the odd one: it needs a hook on the
**card reward generator**, not on pickup.

### `Event` rarity

| Relic | Id | Grants |
| --- | ---: | --- |
| **Fresnel Lens** | 92 | Nimble. |

### `Ancient` rarity

| Relic | Grants |
| --- | --- |
| **Glitter**, **Silken Tress** | Glam |
| **Tri-Boomerang** | Instinct |
| **Beautiful Bracelet** | Swift at **3**, one chosen deck card |

### Relics that *read* enchantments rather than grant them

A separate category, and each needs the enchantment system to exist before it means
anything:

- **Mystic Lighter** (Shop) — +9 unpowered damage on any enchanted card.
- **Claws** (Ancient) — copies a card, carrying its enchantment across via
  `MutableClone()` when `CanEnchant` allows.
- **Archaic Tooth** (Ancient) — same clone-the-enchantment shape, on starter cards.
- **Whispering Earring** (Ancient) — orders itself `Late` specifically so it fires after
  Imbued.

## Suggested order

1. **The five Shop granters plus their enchantments** (Sharp is already implemented;
   RoyallyApproved, Momentum, Adroit are not). This is what act-1 shops actually sell,
   and it is what makes an act-1 deck distribution honest.
2. **Mystic Lighter**, once any enchantment exists for it to read.
3. Fresnel Lens, then the Ancient set.

Wing Charm is worth doing last of the Shop five: it is the only one needing a new hook
point in `RunRewardGenerator` rather than reusing the deck-selection path.
