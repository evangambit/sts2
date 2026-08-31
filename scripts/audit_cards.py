#!/usr/bin/env python3
"""Track which cards have been READ against the current decompiled source.

`CardCoverageTests.Pending` answers "does this card have a test". That is a weaker claim
than it looks, and this session is the evidence: **every card whose divergence was found
in E158-E185 had already passed that bar**, or was explicitly deferred with a note. A test
written from a wrong reading passes forever, and a test written from a RIGHT reading of an
old source passes forever too, because the emulator and the test drift together while the
game moves underneath both.

So this audit records a different fact: that a human compared this card's decompiled source
to the emulator's arm, and WHICH VERSION of the source they compared. It is the mechanism
`audit_enemy_moves.py` already uses for monster move machines -- a digest plus a note --
applied to the thing that turned out to need it more.

Three states, and the middle one is the point:

  read     in READ, and the source still digests the same. Nothing to do.
  STALE    in READ, and the source has CHANGED since. Exit code 1: a card someone
           verified against a version of the game that no longer exists is worse than an
           unread one, because the note says it was checked.
  unread   not in READ. A worklist, not a failure -- there are hundreds, and the count
           going down is the progress bar.

The number worth watching is neither of those. It is `tested but unread`: cards with a
test suite and no reading behind them. That is exactly the state Leg Sweep, Predator,
Shadow Step and Shadowmeld were in.

    uv run python scripts/audit_cards.py
    uv run python scripts/audit_cards.py --unread          # the worklist, in full
    uv run python scripts/audit_cards.py --digests Zap Turbo
    uv run python scripts/audit_cards.py --card Shadowmeld
"""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CARDS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Cards"
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated" / "Cards.g.cs"
COVERAGE = REPO / "src" / "Sts2Emulator.Tests" / "Cards" / "CardCoverageTests.cs"
IMPLEMENTED = REPO / "src" / "Sts2Emulator.Tests" / "Cards" / "ImplementedCards.g.cs"

BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
LINE_COMMENT = re.compile(r"^\s*(///|//).*$", re.MULTILINE)

# Lines that carry no rules. A card's digest has to survive MegaCrit re-recording a sound
# effect or renaming a shader, or the notes go stale for reasons nobody needs to read
# about -- and a digest that cries wolf gets ignored, which is the failure this whole file
# exists to prevent.
#
# Kept deliberately narrow: each pattern matches a line that is ONLY the cosmetic call.
# `.WithHitCount(...)` and `.Targeting(...)` sit in the same fluent chains and are rules,
# so a loose "drop anything mentioning Vfx" would eat them the moment someone reflows a
# chain onto one line.
COSMETIC = re.compile(
    r"""^\s*(
          (await\s+)?(CreatureCmd\.TriggerAnim|VfxCmd\.\w+|SfxCmd\.Play|Cmd\.(CustomScaled)?Wait)\(.*\);
        | \.With(Hit|Attacker)(Fx|Vfx|Sfx|Anim)\(.*\)
        | (protected\s+override\s+)?(IEnumerable<IHoverTip>|string)\s+(Extra)?(HoverTips|PassiveSfx|EvokeSfx|ChannelSfx)\s*=>.*
        | public\s+override\s+Color\s+\w+\s*=>.*
        | protected\s+override\s+IEnumerable<string>\s+ExtraRunAssetPaths\s*=>.*
        | NCombatRoom\.Instance.*;
        | (NGame|SaveManager|NDebugAudioManager)\..*;
        # Relic-side UI, for the sibling audit that shares this stripper. No card source
        # contains any of these, so adding them moved no card digest -- checked before the
        # patch rather than after, because a stripper change silently re-flags every note.
        | Flash\(\);
        | public\s+override\s+(bool\s+(ShowCounter|HasUponPickupEffect)|int\s+DisplayAmount)\s*=>.*
        )\s*$""",
    re.VERBOSE,
)


def behavioural_source(text: str) -> str:
    """Reduce a card to what a reader actually reads: the class, minus cosmetics."""
    text = BLOCK_COMMENT.sub("", text)
    text = LINE_COMMENT.sub("", text)
    start = text.find("public sealed class")
    if start < 0:
        start = text.find("public class")
    if start >= 0:
        text = text[start:]
    kept = [line for line in text.splitlines() if not COSMETIC.match(line)]
    return " ".join(" ".join(kept).split())


def card_digest(text: str) -> str:
    return hashlib.sha256(behavioural_source(text).encode()).hexdigest()[:12]


# Cards whose decompiled source has been READ against the emulator's arm, with the digest
# of the source that was read and a one-line note on what the reading turned on.
#
# Seeded ONLY with cards read in the session that built this file. Ironclad's 92 were
# written against `decompiled/` with the source cited per file, which is a good basis and
# is NOT the same claim -- nobody can now say which version of the source that was, and
# guessing would put exactly the false confidence here that the file exists to remove.
# They are unread until someone re-reads them, and that is the honest starting point.
READ: dict[str, tuple[str, str]] = {
    "Cascade": (
        "c1b2abb765be",
        "X-cost Rare: auto-play X cards (X+1 upgraded) off the TOP of the draw pile, `forceExhaust: false`. Correct -- and it is one of the raw-id `case 546:` arms the coverage generator could not see until E403.",
    ),
    "Disintegration": (
        "e2ceda2a1892",
        "Status, PowerVar<DisintegrationPower>(6). Applied by the Knowledge Demon's `OnChosen`, not by playing the card, and the emulator does that at the demon's screen; the card itself is inert. Correct.",
    ),
    "FranticEscape": (
        "6e00d958ff98",
        "1-cost Status: raises the Sandpit on the enemy HOLDING it -- `Enemies.FirstOrDefault(HasPower<SandpitPower>())` -- and adds 1 to its own cost for the combat. The emulator targeted the FIRST enemy, so with the sandpit anywhere but the front it raised the wrong creature's power and the escape never progressed.",
    ),
    "LanternKey": (
        "f3126c61e4be",
        "Quest, Unplayable. At `CurrentActIndex == 2` it makes every `?` room an Event and forces the next event to be War Historian Repy. Act 3 only, so inert in Act 1 -- correct, and the emulator's empty arm says so.",
    ),
    "MindRot": (
        "8a9cf16c9656",
        "Status, PowerVar<MindRotPower>(1), applied by the Knowledge Demon's OnChosen rather than by play; correct.",
    ),
    "Sloth": (
        "b35da3dfdbda",
        "Status, PowerVar<SlothPower>(3), applied by the Knowledge Demon's OnChosen rather than by play; correct.",
    ),
    "Soul": (
        "caa378999af5",
        "0-cost Token, Exhaust: `CardsVar(2)` upgrading by 1 -- draw TWO, or three. It was drawing one.",
    ),
    "SpoilsMap": (
        "e47cb1055657",
        "Quest, Unplayable, GoldVar(600). Its `ModifyGeneratedMap` replaces act TWO's map with a SpoilsActMap; act 1 is untouched, so the empty arm is correct.",
    ),
    "SporeMind": (
        "47f6999118b7",
        "1-cost Curse with Exhaust and NO OnPlay AT ALL -- the whole card is paying one energy to be rid of it, Debris's shape as a curse. The emulator applied `NoBlock`, which is invented and one of the harshest debuffs in the game.",
    ),
    "SweepingGaze": (
        "72d747d157ec",
        "0-cost Token, Ethereal + Exhaust, OstyAttack-tagged: OstyDamageVar(10) upgrading by 5, one hit at a random opponent, and nothing when the pet is gone; correct.",
    ),
    "WasteAway": (
        "c7cec1b4168c",
        "Status, PowerVar<WasteAwayPower>(1), applied by the Knowledge Demon's OnChosen rather than by play; correct.",
    ),
    "Wish": (
        "e64c267ed8a5",
        "0-cost Ancient, Exhaust: a TUTOR -- `CardSelectCmd.FromCombatPile(PileType.Draw)` takes one chosen card from the draw pile into hand, and the upgrade adds RETAIN. The emulator paid GOLD, which is not something this card does and not something a combat card generally does.",
    ),
    "Debris": (
        "d2165304ef91",
        "1-cost Status, Exhaust, and OnPlay returns immediately -- a card you pay one energy to be rid of. The empty arm is correct.",
    ),
    "Enlightenment": (
        "3b02252de2a0",
        "0-cost, Exhaust: sets every card in HAND to cost ONE, `reduceOnly` -- `SetThisCombat` upgraded, `SetThisTurnOrUntilPlayed` otherwise. The emulator made the whole hand FREE and ignored the upgrade: a strictly better card, for the wrong duration. Needed a CostThisTurn field, since a free-for-the-turn flag cannot express 'costs 1 this turn'.",
    ),
    "Enthralled": (
        "7d981a0045f3",
        "2-cost Curse, Eternal: `ShouldPlay` is false for every card in hand except itself, so you must play it before anything else. Already modelled in `IsBlockedByEnthralled`; correct.",
    ),
    "FeedingFrenzy": (
        "1257ad6c5a3d",
        "0-cost, `FeedingFrenzyPower : TemporaryStrengthPower` at PowerVar<StrengthPower>(5) upgrading by 2 -- five Strength or seven, TAKEN BACK at end of turn. It sat in a six-card stack giving permanent Strength 1 or 2: wrong number and wrong duration, in opposite directions.",
    ),
    "HelloWorld": (
        "8819f3b0db56",
        "1-cost Power, `HelloWorldPower(1)` -- ALWAYS one, the upgrade adds INNATE. The power adds that many distinct COMMON cards from the character's own pool to hand each turn. The emulator applied `InfiniteBlades`, which makes SHIVS, at `upgraded ? 2 : 1`: wrong power and wrong amount.",
    ),
    "Luminesce": (
        "783cdf7764b4",
        "0-cost Token, Exhaust + Retain: EnergyVar(2) upgrading by 1. It shared `upgraded ? 2 : 1` with Supercritical and Wisp; only Luminesce actually ran that body, and only Luminesce was wrong.",
    ),
    "Outmaneuver": (
        "77cb94a34117",
        "1-cost, EnergyVar(2) upgrading to 3 as `EnergyNextTurnPower`; correct.",
    ),
    "Supercritical": (
        "f347dfeb9219",
        "0-cost Rare, Exhaust: EnergyVar(4) upgrading by 2. Handled correctly in ApplyDefectCard -- its label in the shared energy stack was DEAD, and removing it is why this reads correct rather than as a quarter of its value.",
    ),
    "ToricToughness": (
        "d200c4178a30",
        "2-cost, 5 block AND `ToricToughnessPower(2)`: for the next two turns, the same block again when block clears. The power half was missing, which is most of the card. Its Amount is the TURN COUNT, so the block lives in a BlockVar set by SetBlock -- and SetBlock records what was GAINED, so Dexterity rides the repeats.",
    ),
    "Wisp": (
        "4efd9f2a0a9c",
        "0-cost, Exhaust, EnergyVar(1) -- and the upgrade adds RETAIN rather than energy, so the number does not move. Handled correctly in ApplyNecrobinderCard; its label in the shared energy stack was dead.",
    ),
    "BrightestFlame": (
        "ccc0a50e6e54",
        "0-cost, EnergyVar(2) upgrading to 3, CardsVar(2) which does NOT upgrade, and LoseMaxHp(1) -- the price the card is built around, missing entirely. Its draw was being upgraded too; only Fuel's is. LoseMaxHp deals the excess as Unblockable damage rather than clamping, so at full health it costs a point of current HP with the cap.",
    ),
    "DualWield": (
        "ea0e2f16a87d",
        "1-cost, CardSelectCmd.FromHand filtered to ATTACK or POWER, then CardsVar(1) clones of the chosen card -- one, or two upgraded. The emulator duplicated the FIRST card in hand whatever it was, two or three times: no choice, no filter, wrong count in both directions.",
    ),
    "Entrench": (
        "ab3fb3872d35",
        "2-cost, doubles the block you have -- `GainBlock(Block, Unpowered | Move)`. UNPOWERED, so Dexterity does not ride the doubling; it was going through the powered path and paying Dexterity twice on a card whose whole point is the block already there. Upgrade cuts the cost.",
    ),
    "Fuel": (
        "1b80d720a343",
        "0-cost Token, Exhaust: EnergyVar(1) flat and CardsVar(1) upgrading to 2. It was stacked with Brightest Flame and given ITS numbers -- two or three of each.",
    ),
    "Relax": (
        "103e642ce9a7",
        "3-cost Ancient, Exhaust: 15 block, then DrawCardsNextTurnPower(2) and EnergyNextTurnPower(2), all three upgrading; correct -- the draw and energy are NEXT turn, which the emulator already had.",
    ),
    "RipAndTear": (
        "9efd92702939",
        "1-cost, 7 damage x2 at RANDOM opponents, re-rolled per hit; correct.",
    ),
    "Stack": (
        "5075ce1856b0",
        "1-cost, block equal to the DISCARD pile's size, `CalculationBase(0)` upgrading to 3; correct, and `ValueProp.Move` means Dexterity does apply -- unlike Entrench above.",
    ),
    "Apotheosis": (
        "e6b681bf7ffe",
        "2-cost Skill, Exhaust + Innate, upgrade cuts the cost. Upgrades every card in `AllCards` EXCEPT itself -- and AllCards is Hand, Draw, Discard, EXHAUST and Play. The exhaust pile was missing, which matters because cards come back from it (Howl From Beyond, Bombardment, Secret Technique) and one that comes back upgraded is a different card.",
    ),
    "Apparition": (
        "27f9d7dbb6c3",
        "1-cost, Intangible 1, Ethereal + Exhaust, and the upgrade REMOVES Ethereal rather than changing a number; correct, and the upgrade is carried by the extracted EtherealRemovedWhenUpgraded flag.",
    ),
    "Caltrops": (
        "060ba6b62cb6",
        "1-cost Power, Thorns 3 upgrading to 5; correct.",
    ),
    "Distraction": (
        "24b13c54e506",
        "1-cost, ONE random SKILL from the character's own pool, free THIS TURN, into HAND; upgrade cuts the cost. It shared a body with three other cards that added one card of ANY type and passed `upgraded` as the free-this-turn flag -- so the filter was ignored and an unupgraded Distraction gave a card that was not free.",
    ),
    "Metamorphosis": (
        "d834ed4ee9b2",
        "2-cost, THREE random ATTACKS (five upgraded, `CardsVar(3)` upgrading by 2) into the DRAW PILE at random positions, free for the whole COMBAT. The shared body gave one card of any type, into hand, free only when upgraded -- four things wrong.",
    ),
    "ByrdSwoop": (
        "2983c17b5bef",
        "0-cost, 14 damage, +4 upgraded. What the Byrdonis Egg becomes. Plain attack; correct in the shared damage body.",
    ),
    "ByrdonisEgg": (
        "51697303a29f",
        "Quest, Unplayable, cost -1. Its whole effect is TryModifyRestSiteOptions adding HATCH -- the only rest option a CARD puts on the screen. Written from scratch with Byrdpip (E397).",
    ),
    "Clash": (
        "b250b15e04f9",
        "0-cost, 14 damage, and `IsPlayable` is EVERY card in hand being an Attack -- itself included, and it is one. It was stacked into a plain-damage body, so any hand could play it and the whole deckbuilding constraint was gone. Split out.",
    ),
    "Exterminate": (
        "2c31a7e2a006",
        "1-cost, 3 damage x4 to ALL enemies, upgrading the damage rather than the count; correct.",
    ),
    "Maul": (
        "b91ae8805165",
        "1-cost, FIVE damage TWICE, and every play raises the damage of EVERY Maul the player owns by Increase(1, upgrading to 2). It was stacked into a plain-damage body: one hit and no growth, so half the damage and none of the card. Note the growth reaches all copies, where Rampage's reaches only its own -- two readings of 'this card gets stronger'.",
    ),
    "MinionDiveBomb": (
        "d40c7686790c",
        "0-cost, 13 damage, Exhaust, Minion-tagged. Plain attack; correct.",
    ),
    "MinionSacrifice": (
        "03350d3732b2",
        "0-cost, 8 block, Exhaust, Minion-tagged -- the only Minion card that gains block, which is what makes Vitruvian Minion's block half reachable; correct.",
    ),
    "MinionStrike": (
        "25d35e1adcfe",
        "0-cost, 6 damage AND draw 1, Exhaust, tagged Strike AND Minion; correct.",
    ),
    "Peck": (
        "5f48047fa29e",
        "1-cost, 2 damage x3, upgrading the COUNT to 4 rather than the damage; correct.",
    ),
    "Rebound": (
        "5013369937c6",
        "1-cost, 9 damage AND ReboundPower, which sends the next card bound for the discard to the TOP of the draw pile and is spent doing it. The power was missing -- the card was in a plain-damage stack. It has no guard against its own source, so Rebound recycles ITSELF, and AfterSideTurnEnd removes it so an unspent one does not carry over.",
    ),
    "Squash": (
        "4302881e7a22",
        "1-cost, 10 damage and Vulnerable 2, both upgrading; correct.",
    ),
    "Clumsy": (
        "49abf07a482c",
        "Unplayable + Ethereal, and nothing else -- it exhausts itself at end of turn, so it costs one draw and leaves. Keywords only; both extracted correctly.",
    ),
    "CurseOfTheBell": (
        "ade12ad267fa",
        "Unplayable + Eternal, nothing else. Calling Bell's price, and Eternal means the removal screens will not offer it. Keywords only.",
    ),
    "Folly": (
        "392cfc186633",
        "Unplayable + Eternal + Innate + Ethereal -- all four. Innate puts it in the opening hand and Ethereal exhausts it there, so it costs one card of the opening hand every fight and cannot be removed. Keywords only.",
    ),
    "Greed": (
        "94c02475545e",
        "Unplayable + Eternal, nothing else. Keywords only.",
    ),
    "Guilty": (
        "99cc2a3fb771",
        "Unplayable, and `AfterCombatEnd` counts the combats it sits through in the DECK -- at FIVE it removes itself. The only card in the game that leaves on its own. Per COPY, because the count is [SavedProperty] on the card model. Written from scratch; the event that hands it out was modelled and the card did nothing.",
    ),
    "Injury": (
        "2d3104bf3233",
        "Unplayable, nothing else. Keywords only.",
    ),
    "Normality": (
        "2568d9f7cfcf",
        "Unplayable, and `ShouldPlay` is false once THREE cards have been played this turn while it is in HAND -- so drawing it late can stop a turn dead and shuffling it away costs nothing. Written from scratch, onto the same door as Velvet Choker's limit.",
    ),
    "PoorSleep": (
        "f8167cf4271c",
        "Unplayable + Retain -- the one curse that does NOT leave the hand at end of turn, so it clogs the same five cards all fight. Keywords only.",
    ),
    "Soot": (
        "499c9162b49d",
        "Unplayable, `CanBeGeneratedInCombat => false`. Biiig Hug's gift and nothing more. Keywords only.",
    ),
    "Void": (
        "8e443f147881",
        "Unplayable + Ethereal, and `AfterCardDrawn` on ITSELF loses 1 energy -- the energy it takes on the way past is the whole card. Written from scratch.",
    ),
    "Wound": (
        "bed29a739fb0",
        "Unplayable, nothing else. Keywords only.",
    ),
    "Writhe": (
        "77e1caeda0da",
        "Unplayable + Innate -- it is in the opening hand every fight, and unlike Folly it stays there. Keywords only.",
    ),
    "BadLuck": (
        "02137afba50d",
        "Unplayable, Eternal. At turn end in HAND, 13 Unblockable+Unpowered -- block does not save you, and Eternal means it cannot be removed. Written from scratch; the turn-end list named four cards and this was not one.",
    ),
    "Beckon": (
        "a85cd64c3795",
        "Costs 1, Status. At turn end in hand, 6 Unblockable+Unpowered. Already modelled, beside the four-card list rather than in it; folded into the one chokepoint now.",
    ),
    "Burn": (
        "01accf98c5e6",
        "Unplayable. At turn end in hand, DamageVar(2) blockable and unpowered; correct.",
    ),
    "Debt": (
        "21904208b16f",
        "Unplayable. At turn end in hand, `Min(GoldVar(10), Owner.Gold)` gold -- it cannot put the run into debt, which is the joke. Written from scratch, and the only turn-end card that touches gold rather than HP.",
    ),
    "Decay": (
        "9fa293c51e14",
        "Unplayable. At turn end in hand, DamageVar(2) blockable and unpowered -- Burn's twin as a Curse. Written from scratch.",
    ),
    "Doubt": (
        "b13d11376e21",
        "Unplayable. At turn end in hand, Weak 1, with SkipNextDurationTick set when the player did not already have Weak -- so a fresh stack survives the tick moments later instead of expiring in the same breath. Written from scratch.",
    ),
    "Infection": (
        "413589cde4e3",
        "Unplayable Status. At turn end in hand, DamageVar(3) blockable and unpowered; correct.",
    ),
    "Regret": (
        "44d983355712",
        "Unplayable. At turn end in hand, damage equal to the SIZE OF THE HAND -- snapshotted in BeforeSideTurnEnd and spent in the turn-end hook, so it is the hand as the sequence began. Unblockable and Unpowered. Written from scratch.",
    ),
    "Shame": (
        "0239c6477001",
        "Unplayable. At turn end in hand, Frail 1, with the same fresh-stack grace as Doubt. Written from scratch.",
    ),
    "Toxic": (
        "e87272a9dd7c",
        "Costs 1, Exhaust. At turn end in hand, DamageVar(5) blockable and unpowered; correct.",
    ),
    "Wither": (
        "845a28054f82",
        "Unplayable Status. At turn end in hand, DamageVar(3) blockable and unpowered. Its `FakeUpgrade` grows the damage by 3 a time and nothing in a solo run calls it; correct.",
    ),
    "Acrobatics": ("bcc58ace2ee0", "Draw 3/4 then discard a CHOSEN card; correct."),
    "BouncingFlask": (
        "766f4097ab21",
        "Poison 3 to a RANDOM enemy per bounce, 3/4 bounces; correct.",
    ),
    "BulletTime": (
        "4e180a25fb08",
        "Hand free for the turn EXCEPT X-cost cards, plus NoDraw 1; the emulator freed the X cards too, turning them into dead draws.",
    ),
    "Burst": ("25dceef8846d", "BurstPower for 1/2 SKILL replays; correct."),
    "DaggerThrow": ("07eab37057c7", "9/12, draw 1, discard a CHOSEN card; correct."),
    "Dash": ("81b36f100587", "Block 10/13 FIRST, then damage 10/13; correct."),
    "Finisher": (
        "33a02522c3dc",
        "6/8 once per Attack FINISHED this turn, no minimum hit and it does not count itself; correct.",
    ),
    "Flanking": (
        "77c43ef93b15",
        "MultiplayerOnly: FlankingPower 2 on the target, doubling what ALLIES deal to it. The emulator gave the player 2 energy next turn -- a real, observable buff on an unreachable card.",
    ),
    "Flechettes": (
        "9a59e9c1eb58",
        "5/7 once per Skill in hand, no minimum hit; correct.",
    ),
    "GrandFinale": (
        "c0bc1dd9afe1",
        "60/75 to all, playable only with an EMPTY draw pile -- a mask rule, and it is in the mask; correct.",
    ),
    "HandTrick": (
        "d56344a53d5f",
        "Block 7/10 and a CHOSEN non-Sly Skill in hand becomes Sly for the turn; correct.",
    ),
    "Haze": (
        "0030df952ecf",
        "Three cost, Sly, Poison 4/6 to every hittable enemy; correct.",
    ),
    "HiddenDaggers": (
        "4ee86e9d45b9",
        "Discard 2 CHOSEN cards then make 2 Shivs, upgraded if it was; correct.",
    ),
    "KnifeTrap": (
        "fb56b49e840c",
        "Replays every Shiv in the EXHAUST pile at its own target, upgrading them if upgraded; correct.",
    ),
    "Malaise": (
        "38efe14304fe",
        "X (+1 upgraded) permanent Strength loss and Weak on the target; correct.",
    ),
    "MasterPlanner": (
        "44ec3393272c",
        "Every Skill the owner plays gains the Sly keyword for the combat; correct.",
    ),
    "Mirage": (
        "75e85e77a1e9",
        "Block equal to all the Poison on living enemies, upgrade is a cost cut; correct.",
    ),
    "Murder": (
        "d8953cad6a0f",
        "1 + 1 per card drawn this COMBAT, upgrade is a cost cut; correct.",
    ),
    "NoxiousFumes": ("7eb0bdebb700", "Poison 2/3 to every enemy each turn; correct."),
    "Outbreak": (
        "35184319329d",
        "OutbreakPower 11/15, paid on every third Poison applied; correct.",
    ),
    "PhantomBlades": (
        "9107f85b9994",
        "Shivs Retain, and the FIRST Shiv of the turn hits for 9/12 more; correct.",
    ),
    "Pinpoint": (
        "25c89e153264",
        "15/19, and one cheaper per SKILL played this turn through two hooks; the emulator had a comment saying so and no code.",
    ),
    "PreciseCut": (
        "98c2cb739f75",
        "13/16 less 2 per OTHER card in hand -- the played card is already out of hand; correct.",
    ),
    "Prepared": (
        "67cdee4edc61",
        "Draw 1/2 then discard that many CHOSEN cards; correct.",
    ),
    "SerpentForm": (
        "b29e6203454e",
        "4/6 to a random enemy on every card played, snapshot before and paid after; correct.",
    ),
    "Sneaky": (
        "60210937566b",
        "MultiplayerOnly: block when ANOTHER player plays an Attack. The emulator stacked Afterimage, which is block per card the OWNER plays -- a power that does pay out solo.",
    ),
    "Speedster": (
        "c3e3d8755037",
        "2 damage to all on every non-hand draw; the upgrade adds INNATE, not damage. Correct.",
    ),
    "Survivor": ("8a181b1a3b78", "Block 8/11 and discard a CHOSEN card; correct."),
    "Tracking": (
        "2a9783f7891a",
        "TrackingPower 2, or +1 if already held; multiplies card attacks on a Weak target. Correct.",
    ),
    "WraithForm": (
        "5c7a42ec7896",
        "Intangible 2/3 and WraithFormPower 1, which takes Dexterity EVERY turn; correct.",
    ),
    "Bombardment": (
        "4812ad4c0628",
        "18/24 and Exhaust -- then it AUTO-PLAYS itself out of the exhaust pile every turn, free; the emulator had a plain attack.",
    ),
    "BundleOfJoy": (
        "aa58ce3623a7",
        "3/4 DISTINCT COLOURLESS cards to hand; took one CLASS card, and passed `upgraded` as the helper's freeThisTurn.",
    ),
    "CelestialMight": (
        "77af22877a73",
        "6 damage x 3 hits, upgrade buys a fourth hit; correct.",
    ),
    "CloakOfStars": ("130140808bb3", "One star, block 7/10; correct."),
    "Comet": (
        "3de607e4cc3d",
        "Five stars, 33/44 and Weak 3 / Vulnerable 3 on the target; correct.",
    ),
    "CrashLanding": (
        "8f58ee6bf3d1",
        "21/26 to ALL, then DEBRIS to fill the hand; hit one enemy and added two random Regent cards.",
    ),
    "DefendRegent": ("d40144965470", "Block 5/8; correct."),
    "Devastate": ("ece5da4b7c01", "Four stars, 30/40 at one target; correct."),
    "FallingStar": (
        "6267cbb69b6f",
        "Two stars, 8/12 and Weak 1 / Vulnerable 1 on the target; correct.",
    ),
    "GammaBlast": (
        "9173ab013a4e",
        "Three stars, 13/18 and Weak 2 / Vulnerable 2 on the target; correct.",
    ),
    "GatherLight": (
        "c418e5303e31",
        "Block 8/11 and one star, which does not upgrade; correct.",
    ),
    "Glitterstream": ("cd7d9fae505f", "Block 11/13 now and 5/7 next turn; correct."),
    "GuidingStar": ("d1448fe49231", "Two stars, 12/13 and draw 2/3; correct."),
    "Hegemony": ("42f780feac9d", "15/18 and 2/3 energy next turn; correct."),
    "HeirloomHammer": (
        "36a79abe237c",
        "20/25, then a CHOSEN COLOURLESS card in hand is CLONED; added one or two random Regent cards.",
    ),
    "KinglyKick": (
        "b833b122a5e5",
        "27/35, and this COPY gets one cheaper every time it is DRAWN; the draw hook was missing.",
    ),
    "KinglyPunch": (
        "26ddc0642133",
        "8/10 plus 4/6 for every time this COPY has been DRAWN; scaled by cards played this combat.",
    ),
    "KnockoutBlow": (
        "eec4c6801bc5",
        "30/38 and five stars only if the target died; correct.",
    ),
    "MakeItSo": (
        "284022a49f46",
        "6/9, and every THIRD Skill played in a turn pulls it back to hand from any pile; the hook was missing.",
    ),
    "ManifestAuthority": (
        "24a410c8d36c",
        "Block 7/8 and one distinct COLOURLESS card, upgraded if it was; added a random Regent card.",
    ),
    "MeteorShower": (
        "6a15861e75ba",
        "14/21 and Weak 2 / Vulnerable 2 to ALL; shared Comet's single-target body.",
    ),
    "Patter": ("8e9cf9adf8db", "Block 8/10 and Vigor 2/3; correct."),
    "Radiate": (
        "a125ab4a980a",
        "3/4 at ALL, once per star GAINED this turn and no floor; hit one enemy `max(1, stars held)` times.",
    ),
    "SevenStars": (
        "2a4f98261c55",
        "7 damage x 7 hits at ALL, upgrade buys energy; put all seven hits on one enemy.",
    ),
    "ShiningStrike": (
        "a2337cf4f55e",
        "8/11, two stars, back on TOP of the draw pile; correct -- the Exhaust guard falls out of the play path's ordering.",
    ),
    "SolarStrike": ("cdceabd16bd6", "9/10 and 1/2 stars; correct."),
    "StrikeRegent": ("de7665516307", "6/9 at one target; correct."),
    "Afterlife": (
        "3c50500e8694",
        "Summon 6/9 and nothing else; a dead duplicate arm drew cards.",
    ),
    "BansheesCry": (
        "6aeeaee4db7f",
        "33 to ALL, and 2 cheaper per Ethereal card played; had neither.",
    ),
    "Bodyguard": (
        "ccf12b1bb5f4",
        "Summon 5/7 and nothing else; a dead duplicate arm gained block.",
    ),
    "BoneShards": (
        "cd7e52f353df",
        "Osty hits ALL for 9/12, block, kill -- and the block is inside the missing-Osty guard.",
    ),
    "Bury": ("8a99f88b7959", "52/63 at one target; correct."),
    "DeathMarch": (
        "bd8a51d2308f",
        "8/9 + 4/6 per NON-hand-draw this turn; counted Automation's draws and never upgraded the base.",
    ),
    "Deathbringer": (
        "3a26bb59aab8",
        "Doom 21/26 and Weak 1 to ALL hittable enemies; both landed on one.",
    ),
    "DeathsDoor": (
        "00fc803d67fc",
        "1 block gain, 3 if the player applied Doom this turn; gated on half HP and did 3/4.",
    ),
    "DefendNecrobinder": (
        "de9b93de7490",
        "Block 5/8; correct, and split off Undeath's case so it cannot clone itself.",
    ),
    "Defy": ("cf363200adee", "Block 6/9 and Weak 1 on the target; correct."),
    "Delay": ("f61e6290febf", "Block 11/13 and 1/2 energy next turn; correct."),
    "DrainPower": (
        "1cc1b760863e",
        "10/12 damage, then 2/3 RANDOM upgradable discards upgraded; took the front of the pile.",
    ),
    "Dredge": (
        "2ba57ec9d4b9",
        "Up to 3 CHOSEN discards to hand, clamped by hand room; took the oldest three.",
    ),
    "EndOfDays": ("f01cfd9f6337", "Doom 29/37 to all then DoomKill; correct."),
    "Fear": ("8d9e1f059ded", "7/8 and Vulnerable 1/2 on the target; correct."),
    "Fetch": (
        "6ef8f0ecdb05",
        "Osty 3/6, and draws only on this COPY's first play of the turn; drew every time.",
    ),
    "Flatten": (
        "a64ea6f955a7",
        "Osty 12/16, and free for the turn once Osty has swung; had no cost hook.",
    ),
    "GlimpseBeyond": (
        "53de3cacd223",
        "3/4 Souls into the draw pile at random positions; correct -- it is handled in ApplyNecrobinderCard. It ALSO carried a label in the generated-approximation stack, which was dead (that dispatch runs later) and misleading: a reader of that stack would have credited it with adding one random class card to hand. Label removed.",
    ),
    "GraveWarden": (
        "86a184cfa05c",
        "Block 8/11 and one Soul to the draw pile; correct. `card_pair.py` hid the Soul inside a PreviewCardPileAdd line and the reading briefly deleted it.",
    ),
    "Graveblast": (
        "78fd5261799a",
        "4/6, then a CHOSEN discard to hand; took the oldest.",
    ),
    "HighFive": (
        "45f3d3270eb8",
        "Osty hits all for 11/13 plus Vulnerable 2/3, nothing without a pet; correct.",
    ),
    "Misery": (
        "e0fb8e27fcb2",
        "7/9, then the target's debuffs (snapshot BEFORE the hit) copied to every other enemy; had High Five's body.",
    ),
    "NegativePulse": ("30c534f09f57", "Block 5/6 and Doom 7/11 to all; correct."),
    "Parse": ("489bf24322fa", "Draw 3/4, Ethereal; correct."),
    "Poke": ("d470530d086d", "Osty 6/9 at one target; correct."),
    "Protector": (
        "fd9a24ffcad7",
        "10/15 + Osty's MAX hp, and NOTHING without a pet; the base paid out regardless.",
    ),
    "PullAggro": ("7bf8145061e8", "Summon 4/5 then block 7/9; correct."),
    "Rattle": (
        "f9441c26cd02",
        "Osty 7/9, hit 1 + Osty's swings this turn times; hit once always.",
    ),
    "Reanimate": ("cc56a66d12c1", "Summon 20/25, Exhaust; correct."),
    "Reap": ("f6bd7d8138d1", "27/33 at one target, Retain; correct."),
    "Reave": (
        "54c112231e7c",
        "9/11 and one Soul to the draw pile, upgraded if Reave was; correct.",
    ),
    "RightHandHand": (
        "83fa8c2f25e9",
        "Osty 4/6, and every copy in the DISCARD returns to hand after a play costing 2+; had only the attack.",
    ),
    "Sacrifice": (
        "fa9f788c7243",
        "Block = Osty MaxHp x2 then kill, gated on the pet being ALIVE; tested MaxHp, which outlives it.",
    ),
    "Scourge": ("882938e1eaa8", "Doom 13/16 on the target then draw 1/2; correct."),
    "Severance": (
        "44e148080484",
        "13/18 and a Soul each to draw, discard and hand; correct.",
    ),
    "SicEm": (
        "52de4429313a",
        "Osty 5/6 then SicEmPower 3/4 on the target, applied even with no pet; correct.",
    ),
    "Sow": (
        "bf13fd7189a2",
        "8/11 to ALL enemies, Retain; shared the single-target Strike body.",
    ),
    "Spur": ("29cfffc2dc8b", "Summon 3/5 then heal Osty 5/7; correct."),
    "Squeeze": (
        "1e4c424b500b",
        "25/30 + 5/6 per other OstyAttack card, and nothing without a pet; the guard was missing.",
    ),
    "StrikeNecrobinder": ("8ea264c0d967", "6/9 at one target; correct."),
    "TheScythe": (
        "34652cae3a94",
        "13, and this COPY gains 4/5 damage per play for good; scaled by cards exhausted this turn.",
    ),
    "TimesUp": (
        "68e6e9106d0b",
        "1 damage per point of DOOM on the target; scaled by cards played this combat, and the capture could not tell them apart.",
    ),
    "Undeath": (
        "74754adf044c",
        "Block 7/9 then a CLONE OF ITSELF to the discard; the emulator cloned a Soul, so the pile counts matched.",
    ),
    "Unleash": ("ac7ffe499a9e", "6/9 + Osty's CURRENT hp; correct."),
    "Wisp": (
        "4efd9f2a0a9c",
        "Gain 1 energy; upgrading buys Retain, not a second energy. Correct.",
    ),
    "Accelerant": (
        "a8ad0a653203",
        "AccelerantPower 1/2, which PoisonPower reads to re-trigger itself",
    ),
    "Accuracy": (
        "31148abdaa81",
        "AccuracyPower 4/6 -- Shiv damage",
    ),
    "AdaptiveStrike": (
        "5a7e572eee34",
        "a clone of itself with SetThisCombat(0) into the discard",
    ),
    "Aggression": (
        "02e647254c95",
        "AggressionPower 1; the upgrade adds the INNATE keyword, not an amount",
    ),
    "Alchemize": (
        "b56e1dbc7bcb",
        "a random potion on Rng.CombatPotionGeneration; the upgrade cuts the cost",
    ),
    "Alignment": (
        "a04029cf2105",
        "3 stars for 2/3 energy",
    ),
    "AllForOne": (
        "078fa2a6ae3b",
        "every 0-cost Attack/Skill/Power in the DISCARD returns to hand; the filter excludes Status and Curse",
    ),
    "Anger": (
        "1f0fb33a1d62",
        "CreateClone() into the discard -- the whole card, enchantment and all",
    ),
    "Anointed": (
        "d49185fba0a0",
        "as many RANDOM rares as the hand has room for, TakeRandom on CombatCardSelection; upgrade adds Retain",
    ),
    "Anticipate": (
        "ab402f9fcd56",
        "the var is a DexterityPower but the APPLY is AnticipatePower, a temporary one",
    ),
    "Armaments": (
        "c5f41cadb022",
        "FromHandForUpgrade ASKS; upgraded takes every upgradable card with no choice",
    ),
    "Arsenal": (
        "e95e87d4c740",
        "ArsenalPower 1: Strength for every card its owner GENERATES; upgrade adds Innate",
    ),
    "AscendersBane": (
        "3a372ec07f97",
        "Eternal, Unplayable and Ethereal; CanBeGeneratedByModifiers false",
    ),
    "AshenStrike": (
        "30f92abca715",
        "6 + 3/4 per card in the exhaust pile, through the ordinary attack command",
    ),
    "AstralPulse": (
        "7b73931aeb9a",
        "3 stars for 6/8 damage TWICE at every enemy",
    ),
    "Automation": (
        "71f4eb594f70",
        "AutomationPower at EnergyVar(1); the upgrade cuts the cost",
    ),
    "Backstab": (
        "ff373211f9f9",
        "11/15, Exhaust AND Innate keywords",
    ),
    "Barricade": (
        "3c6ea5ef5e01",
        "BarricadePower at 1; the upgrade cuts the cost",
    ),
    "Bash": (
        "5c78737df706",
        "8/10 damage then Vulnerable 2/3 on the target",
    ),
    "BattleTrance": (
        "28d66a384f64",
        "draw 3/4 then NoDrawPower -- the lockout is half the card",
    ),
    "BeaconOfHope": (
        "9c7eb023b564",
        "MultiplayerOnly; BeaconOfHopePower shares block with TEAMMATES, so nothing alone",
    ),
    "BeatDown": (
        "5110a4959ae2",
        "3/4 Attacks from the discard by StableShuffle on Rng.Shuffle, each targeted on CombatTargets",
    ),
    "BeatIntoShape": (
        "2746a617868c",
        "5/7 damage, then Forge 5/7 per PRIOR powered hit on that target this turn",
    ),
    "Begone": (
        "328917df044b",
        "a CHOSEN hand card becomes a MINION STRIKE, upgraded if Begone was",
    ),
    "BelieveInYou": (
        "7c0411593bd4",
        "MultiplayerOnly: 2/3 energy to an ALLY, which is the player alone in singleplayer",
    ),
    "BiasedCognition": (
        "d4f233f5adf2",
        "TWO vars: FocusPower(4m) +1, and BiasedCognitionPower(1m) which drains a Focus every turn",
    ),
    "BigBang": (
        "0d1d5317673f",
        "draw 1, a star, an energy and a Forge of 5; upgrade only adds Innate",
    ),
    "BlackHole": (
        "21c730e6ad53",
        "applies BlackHolePower 3/4; the POWER hits all enemies Unpowered on stars gained",
    ),
    "BlightStrike": (
        "173f7411a9a4",
        "8/10 damage, then Doom for the damage actually dealt -- blocked plus unblocked",
    ),
    "BloodWall": (
        "da48d5654770",
        "2 HP first, then 16/20 block",
    ),
    "Bloodletting": (
        "8c6b8815f1ef",
        "3 HP unblockable+unpowered, then 2/3 energy; the upgrade is on the ENERGY",
    ),
    "Bludgeon": (
        "186334ba9bbd",
        "32 damage upgrading by 10",
    ),
    "Blur": (
        "31bae5b9e5c0",
        "5/8 block and BlurPower 1 flat; the upgrade raises the BLOCK",
    ),
    "BodySlam": (
        "f9c4feb15911",
        "CalculatedDamage multiplied by the owner's Block; the upgrade cuts the cost",
    ),
    "Bolas": (
        "b8546461a2d8",
        "3/? damage, and BeforeHandDraw returns it to hand next turn like Thrumming Hatchet",
    ),
    "BorrowedTime": (
        "3a18d83e44d6",
        "4/6 energy now, and every card played for the REST OF THIS TURN costs one more",
    ),
    "Brand": (
        "7e119d6e535f",
        "1 HP, exhaust a CHOSEN card, then Strength 1/2",
    ),
    "Break": (
        "d9af852c0606",
        "20/30 damage then Vulnerable 5/7",
    ),
    "Breakthrough": (
        "6cd7b572ed36",
        "1 HP FIRST, then 9/13 to all enemies",
    ),
    "Buffer": (
        "6d70e0ecc365",
        "PowerVar<BufferPower>(1m) +1; each charge zeroes one instance of HP loss",
    ),
    "Bully": (
        "56019526be10",
        "4 + 2/3 per stack of the TARGET's Vulnerable; the upgrade raises the multiplier",
    ),
    "Bulwark": (
        "e59dd8fd9f23",
        "12/15 block and a Forge of 10/13",
    ),
    "BurningPact": (
        "680b497e0b0c",
        "exhaust a CHOSEN card, THEN draw 2/3 -- the draw must not seed its own candidates",
    ),
    "Calamity": (
        "3542b791859e",
        "CalamityPower 1; the upgrade cuts the cost",
    ),
    "Calcify": (
        "26fd6e6dc455",
        "CalcifyPower 4/6 added to OSTYs attacks only -- not the players",
    ),
    "CallOfTheVoid": (
        "c6daf526d7c9",
        "CallOfTheVoidPower 1: a pool card into HAND every turn, granted ETHEREAL; upgrade adds Innate",
    ),
    "CaptureSpirit": (
        "3cc52a711e51",
        "3/4 Unblockable Unpowered damage to the ENEMY, and 3/4 Souls",
    ),
    "Catastrophe": (
        "d68da1dfdebf",
        "2/3 cards auto-played, each a StableShuffle pick off Rng.Shuffle, preferring playable",
    ),
    "Charge": (
        "b4f994c9ccfb",
        "two CHOSEN draw-pile cards become MINION DIVE BOMBS, in place",
    ),
    "ChildOfTheStars": (
        "538c59ae0155",
        "ChildOfTheStarsPower 2/3: that much Unpowered block PER STAR spent on a card cost",
    ),
    "Cinder": (
        "029fc97a5586",
        "18/24 then exhaust a random hand card on Rng.CombatCardSelection",
    ),
    "Cleanse": (
        "f4a61f813c6e",
        "summon 3/5, then EXHAUST a card CHOSEN from the draw pile",
    ),
    "CollisionCourse": (
        "fd47d73b50c3",
        "11/15 damage and a DEBRIS into hand",
    ),
    "Colossus": (
        "1808bffc3474",
        "5/8 block and ColossusPower 1",
    ),
    "Conflagration": (
        "a20ff9f7cabf",
        "2 damage x 4/5 hits to all; the upgrade raises the REPEAT, not the damage",
    ),
    "Conqueror": (
        "dfdf89a0f3f5",
        "Forge 3/5 and ConquerorPower on the target -- a Sovereign Blade hit lands DOUBLE",
    ),
    "ConsumingShadow": (
        "ab55da889c51",
        "RepeatVar(2) +1 Dark orbs, and a power that evokes the LAST orb at each side turn end",
    ),
    "Convergence": (
        "51fcda76ae54",
        "RetainHand 1, an energy next turn, and a STAR next turn of 1/2",
    ),
    "Coolant": (
        "5b1ac7befd20",
        "AfterSideTurnStart blocks distinct-orb-types times Amount, unpowered",
    ),
    "Coordinate": (
        "698d0073e2b3",
        "MultiplayerOnly; CoordinatePower is a TemporaryStrengthPower at 5 upgrading by 3",
    ),
    "CorrosiveWave": (
        "71cb6893f0ca",
        "CorrosiveWavePower 2/3: poison all enemies per card DRAWN, for one turn",
    ),
    "Corruption": (
        "b0a0fc0db828",
        "CorruptionPower 1; the upgrade cuts the cost",
    ),
    "CosmicIndifference": (
        "fc551e3adcb8",
        "6/9 block, then a CHOSEN discarded card goes on top of the draw pile",
    ),
    "Countdown": (
        "fe80682a2c1d",
        "CountdownPower 6/9: Dooms one RANDOM enemy for that much at every player turn start",
    ),
    "CreativeAi": (
        "19982becfe13",
        "BeforeHandDraw adds Amount random POWER cards to hand; the upgrade is the cost",
    ),
    "Defragment": ("3e9d0372c793", "PowerVar<FocusPower>(1m) +1, permanent"),
    "CrescentSpear": (
        "cc261c42ff4d",
        "1 star; damage 8 + 2/3 per card with a STAR COST the player holds, itself included",
    ),
    "CrimsonMantle": (
        "99f07a8dd302",
        "CrimsonMantlePower at 8/10 and IncrementSelfDamage on play",
    ),
    "Cruelty": (
        "fb996cba1975",
        "CrueltyPower 25, upgrading by another 25",
    ),
    "CrushUnder": (
        "e46be7e27d45",
        "7/8 at ALL enemies and a temporary StrengthLoss of 1/2 on all of them",
    ),
    "DanseMacabre": (
        "ec6739a160d1",
        "DanseMacabrePower 4/6: block per card played at a RESOLVED cost of 2 or more",
    ),
    "DarkEmbrace": (
        "26b6580c09cd",
        "DarkEmbracePower 1; the upgrade cuts the cost",
    ),
    "DarkShackles": (
        "f47124de99a9",
        "DarkShacklesPower IS a TemporaryStrengthPower with IsPositive false; 9 upgrading by 6",
    ),
    "Dazed": (
        "6ee276da3954",
        "Ethereal and Unplayable, MaxUpgradeLevel 0, no OnPlay at all",
    ),
    "DeadlyPoison": (
        "9d449a094209",
        "Poison 5/7 on the target",
    ),
    "Debilitate": (
        "1309afb34411",
        "10/12 damage then DebilitatePower 2/3: doubles Vulnerable and Weak, amount is a DURATION",
    ),
    "DecisionsDecisions": (
        "80a50680eeae",
        "6 stars: draw 3/5, then AUTO-PLAY a CHOSEN playable Skill three times",
    ),
    "DefendIronclad": (
        "f44f2ddf1ff3",
        "5 block upgrading by 3",
    ),
    "Defile": (
        "a6b5e6033e67",
        "13/17 damage, Ethereal, and nothing else -- no exhaust",
    ),
    "Demesne": (
        "7b0253f7a76a",
        "DemesnePower 1: +1 hand draw AND +1 max energy every turn; the upgrade is a discount",
    ),
    "DemonForm": (
        "f52b476dd53f",
        "DemonFormPower at the StrengthPower var, 2 upgrading by 1",
    ),
    "DemonicShield": (
        "11bc94552c75",
        "MultiplayerOnly; 1 HP then block equal to the OWNER's block onto the target",
    ),
    "DevourLife": (
        "57cc551acf61",
        "DevourLifePower 1/2: playing a SOUL summons Osty for that much",
    ),
    "Dirge": (
        "f3e9d933e2bd",
        "HasEnergyCostX: X summons of 3/4, and X Souls into the DRAW pile",
    ),
    "Discovery": (
        "c37ed6ee736e",
        "3 GetDistinctForCombat cards on CombatCardGeneration, choose one free this turn; canSkip NOT modelled",
    ),
    "Dismantle": (
        "a0cb031ae545",
        "8/10, and TWO hits if the target is Vulnerable",
    ),
    "Dominate": (
        "8b5e72efd2ac",
        "Vulnerable 1/2 first, then Strength equal to the target's TOTAL Vulnerable",
    ),
    "DramaticEntrance": (
        "83c2602f30c7",
        "11/15 to all enemies; Exhaust AND Innate",
    ),
    "DrumOfBattle": (
        "ee20d3312b87",
        "draw 2 always; the upgrade raises the ENERGY paid when it is exhausted",
    ),
    "DyingStar": (
        "15b136ced646",
        "3 stars, Ethereal: 9/11 at ALL enemies and a temporary StrengthLoss of 9/11 on each",
    ),
    "EchoForm": (
        "53673a33b554",
        "ModifyCardPlayCount +1 while the turn's first-in-series plays are under Amount; upgrade removes Ethereal",
    ),
    "Eidolon": (
        "5b700da2e1b9",
        "exhausts the hand; Intangible 1 ONLY if nine or more were exhausted",
    ),
    "EnfeeblingTouch": (
        "0d82bda24362",
        "a StrengthLoss var of 8/11 on the TARGET, as a negative TemporaryStrengthPower",
    ),
    "Entropy": (
        "2f53842680ef",
        "EntropyPower at CardsVar(1) flat; the upgrade adds INNATE only",
    ),
    "Equilibrium": (
        "2c5a07c23183",
        "13/16 block then RetainHandPower 1 -- the block comes from ApplyBaseDamageAndBlock, not the case",
    ),
    "Eradicate": (
        "51bfec7ad75c",
        "HasEnergyCostX: 11/14 damage ONCE PER ENERGY SPENT, one target, and it Retains",
    ),
    "EternalArmor": (
        "1b597260757c",
        "PlatingPower 9 upgrading by 3",
    ),
    "EvilEye": (
        "7c6b062b5cc4",
        "the block gain runs TWICE if a card was exhausted this turn",
    ),
    "ExpectAFight": (
        "722db1388dbf",
        "gains 1 energy per Attack in hand, THEN NoEnergyGainPower on itself",
    ),
    "Expose": (
        "4d739690e03b",
        "strip block, remove the WHOLE ArtifactPower, then Vulnerable 2/3",
    ),
    "FanOfKnives": (
        "d7e8bd0fc0fb",
        "FanOfKnivesPower 1 (Single) plus 4/5 Shivs; the power retargets Shiv to AllEnemies",
    ),
    "Fasten": (
        "722085cc0e95",
        "FastenPower 4/6 -- extra block on Defend-tagged cards",
    ),
    "Feed": (
        "9a10f38d7248",
        "GainMaxHp heals as well as raising the cap; gated on ShouldOwnerDeathTriggerFatal",
    ),
    "FeelNoPain": (
        "c6a7a56768d2",
        "CreatureCmd.GainBlock(..., Unpowered) -- the command, so Juggernaut sees it",
    ),
    "FiendFire": (
        "66ba4f725312",
        "hand exhausted first, hit count is the pre-exhaust hand size",
    ),
    "FightMe": (
        "c12475a629c3",
        "5/6 x2, Strength 3/4 to self and 1 to the TARGET -- a buff, not a debuff",
    ),
    "Finesse": (
        "f1f61fb75f18",
        "4/7 block and draw 1",
    ),
    "Fisticuffs": (
        "d81463205aa6",
        "block equals the damage that LANDED plus overkill, not the printed number",
    ),
    "FlakCannon": (
        "5dea026eae0b",
        "exhausts every Status outside the exhaust pile FIRST, then hits once per status at rolled targets",
    ),
    "FlameBarrier": (
        "2d01c20a3d54",
        "12/16 block and FlameBarrierPower 4/6",
    ),
    "FlashOfSteel": (
        "63571cc33a2e",
        "5 damage and draw 1",
    ),
    "ForbiddenGrimoire": (
        "854c1d49c67e",
        "one stack; the power adds that many card-REMOVAL rewards at combat end (payout not modelled)",
    ),
    "ForegoneConclusion": (
        "233753cbb3a8",
        "ForegoneConclusionPower 2/3: that many CHOSEN draw-pile cards to hand before the next draw",
    ),
    "ForgottenRitual": (
        "7d1014df5741",
        "3/4 energy, and only if a card was exhausted this turn",
    ),
    "Friendship": (
        "9958b8c7e451",
        "COSTS 2/1 Strength, and FriendshipPower gives +1 max energy for the rest of the combat",
    ),
    "Furnace": (
        "def1ec636ce0",
        "FurnacePower 5/7: a Forge of that much at the start of EVERY turn",
    ),
    "GangUp": (
        "b3b1683dd737",
        "MultiplayerOnly; 5 + 5/7 per ALLY hit on the target this turn, so 5 flat in singleplayer",
    ),
    "Genesis": (
        "7121a6770ee6",
        "GenesisPower 2/3: that many stars at the start of EVERY turn, and it does not expire",
    ),
    "GeneticAlgorithm": (
        "9c7cc2b93b94",
        "BlockVar(CurrentBlock) starts at 1 and rises by IntVar(Increase, 3m) per play, on the card AND its DeckVersion",
    ),
    "GiantRock": (
        "5d12e05575b0",
        "16/20 damage",
    ),
    "Glimmer": (
        "b20d5ba4d78c",
        "draw 3/4, then put ONE CHOSEN hand card back on top of the draw pile",
    ),
    "Glow": (
        "be94b7c9e4f1",
        "a star (1/2), a card now, and a card next turn",
    ),
    "GoldAxe": (
        "40b23415dc9f",
        "damage equals CardPlaysFinished this combat; the upgrade adds Retain",
    ),
    "Guards": (
        "0ad47cb3b12c",
        "ANY NUMBER of CHOSEN hand cards become MINION SACRIFICES; declining is legal",
    ),
    "HammerTime": (
        "057d32b9c3d4",
        "MultiplayerOnly: HammerTimePower forges for the OTHER players, so nothing at all in solo",
    ),
    "HandOfGreed": (
        "6d55f411d5c9",
        "same Fatal gate as Feed -- Minion AND Reattach, not Minion alone",
    ),
    "Hang": (
        "002b9c9c5d72",
        "10/13 damage, then HangPower max(2, existing) -- a doubling damage multiplier for Hang only",
    ),
    "Haunt": (
        "3736a06e888d",
        "HauntPower 6/8: Unblockable Unpowered damage to a random enemy when a SOUL is played",
    ),
    "Havoc": (
        "5046a8d1658e",
        "AutoPlayFromDrawPile top 1 with forceExhaust",
    ),
    "Headbutt": (
        "c989485a6f72",
        "9/12 then a CHOSEN discard-pile card onto the top of the draw pile",
    ),
    "HeavenlyDrill": (
        "ed29701b26bb",
        "HasEnergyCostX: 8/10 damage per energy spent, the whole COUNT doubled at four or more",
    ),
    "HelixDrill": (
        "0cf3cb9ebaea",
        "hit count is the turn's EnergySpentEntry total minus its own cost, which is zero",
    ),
    "Hellraiser": (
        "4ada1dc48662",
        "HellraiserPower 1; the upgrade cuts the cost",
    ),
    "Hemokinesis": (
        "a26ba01b34ce",
        "2 HP unblockable first, then 15/20",
    ),
    "HiddenCache": (
        "09543e7bd582",
        "a star now and StarNextTurnPower 3/4 for next turn",
    ),
    "HiddenGem": (
        "5a945d8a60b5",
        "Replay 2/3 onto a random draw-pile card on CombatCardSelection; skips Unplayable, Status, Curse and anything already replaying",
    ),
    "HowlFromBeyond": (
        "b16c87857714",
        "16/21 to all. NO CanonicalKeywords -- the Exhaust is an ExtraHoverTips, as Havoc's is -- so it discards itself; AfterAutoPostPlayPhaseEntered replays it from EXHAUST at the END of the play phase, and that AutoPlay takes no forceExhaust, so the replay spends it into the discard. Once per trip to the pile, not every turn.",
    ),
    "HuddleUp": (
        "4f3af10da7dc",
        "MultiplayerOnly; CardsVar(2) drawn by EACH living ally, so 2/3 for the player alone",
    ),
    "Hyperbeam": (
        "9f924a47ae24",
        "PowerVar<FocusPower>(3m) is NOT upgraded -- only the damage is; the Focus is spent, not gained",
    ),
    "IceLance": ("52b81d7a8986", "RepeatVar(3) Frost, not upgraded; the damage is"),
    "IAmInvincible": (
        "2dae14823342",
        "10 block; the card auto-plays ITSELF when on TOP of the draw pile as the play phase ends",
    ),
    "Ignition": (
        "8f6b9df8dc23",
        "MultiplayerOnly; channels Plasma on the TARGET ally, which is the player alone",
    ),
    "Impatience": (
        "0d4f7a2af561",
        "draw 2/3, and only when the hand holds NO Attack",
    ),
    "Impervious": (
        "359bc2d2f386",
        "30/40 block; Exhaust comes from the keyword, not the body",
    ),
    "InfernalBlade": (
        "0106fa2512e6",
        "one FilterForCombat Attack from the character pool, free this turn, on CombatCardGeneration",
    ),
    "Inferno": (
        "9b0065783023",
        "InfernoPower 6/9 plus IncrementSelfDamage on play",
    ),
    "Inflame": (
        "5b400a015a56",
        "plain StrengthPower 2/3, applied immediately",
    ),
    "Intercept": (
        "796edd2d43e6",
        "MultiplayerOnly; 9/13 block and CoveredPower on an ALLY -- nothing at all alone",
    ),
    "Invoke": (
        "4d07ccd9ae42",
        "next turn: 2/3 energy AND a summon of 2/3, the summon removing itself after it fires",
    ),
    "IronWave": (
        "5ae0af44ddee",
        "BLOCK first then damage, 5/7 each",
    ),
    "JackOfAllTrades": (
        "477ef04667d0",
        "1/2 distinct colourless cards excluding itself, on CombatCardGeneration",
    ),
    "Jackpot": (
        "68f80173c2da",
        "25/30 then 3 ZERO-COST character cards via GetForCombat, upgraded if the card is",
    ),
    "Juggernaut": (
        "38b6caecf3ef",
        "JuggernautPower 6/8",
    ),
    "Juggling": (
        "0781143380ce",
        "JugglingPower 1; the upgrade adds INNATE, not an amount",
    ),
    "Knockdown": (
        "a4d6f5a6e460",
        "MultiplayerOnly; KnockdownPower multiplies only ANOTHER player's damage, so nothing alone",
    ),
    "KnowThyPlace": (
        "fbc908f02a8e",
        "Weak 1 and Vulnerable 1 on the TARGET; Exhausts until upgraded",
    ),
    "Largesse": (
        "1b14a0e745ae",
        "MultiplayerOnly and AnyAlly, so unplayable solo; it gives an ALLY a colourless card",
    ),
    "LegionOfBone": (
        "82fbd8dc1fdd",
        "summons 6/8 per LIVING player creature; MultiplayerOnly, so one in solo",
    ),
    "Lethality": (
        "95f284e85bce",
        "LethalityPower 50/75 PERCENT, and only on the first Attack card of the turn",
    ),
    "Lift": (
        "865a36dfcb82",
        "MultiplayerOnly; 11/16 block to the targeted ally, which is the player alone",
    ),
    "LunarBlast": (
        "6c471b61c02d",
        "4/5 damage once per SKILL finished this turn -- nothing at all if none",
    ),
    "MachineLearning": (
        "b89f428eb295",
        "ModifyHandDraw + CardsVar(1); the upgrade adds Innate",
    ),
    "MeteorStrike": ("15edd7313998", "5-cost, 24 +6, and three Plasma"),
    "MadScience": (
        "f83aed93d37c",
        "ONE of Attack/Skill/Power plus ONE rider, both from Tinker Time; the upgrade adds INNATE only",
    ),
    "Mangle": (
        "9f81ad699e06",
        "15/20 then ManglePower 10/15 -- a TemporaryStrengthPower, so it lapses",
    ),
    "MasterOfStrategy": (
        "b7c6b4a4d2df",
        "draw 3/4, Exhaust",
    ),
    "Mayhem": (
        "9f0073aca2ef",
        "MayhemPower 1; the upgrade cuts the cost",
    ),
    "Melancholy": (
        "1a827bd8c371",
        "13/17 block, and every creature death makes each copy in a combat pile one cheaper",
    ),
    "MementoMori": (
        "23dd9431e144",
        "9 + 4 per card discarded this turn; OnUpgrade raises BOTH -- base +2 and per-discard +1",
    ),
    "Mimic": (
        "4e4946c8fbe1",
        "MultiplayerOnly; block equal to the TARGET's block, so self-doubling alone",
    ),
    "MindBlast": (
        "adbf0809685f",
        "damage equals the draw pile size, Innate; the upgrade cuts the cost",
    ),
    "Modded": (
        "1910ca9b66bf",
        "one orb SLOT, draw 1/2, and the played COPY costs one more for the combat",
    ),
    "MoltenFist": (
        "74f5009156a4",
        "10/14, then reapplies the target's CURRENT Vulnerable if it survives",
    ),
    "MonarchsGaze": (
        "1bb8f34631d6",
        "MonarchsGazePower 1: every powered attack takes that much temp Strength off its target",
    ),
    "Monologue": (
        "d7187ee82305",
        "MonologuePower: +1 Strength per card played, ALL taken back at the turn end",
    ),
    "MultiCast": (
        "f22064bd3dad",
        "X-cost; evokes the FRONT orb X times, dequeuing only on the last",
    ),
    "NecroMastery": (
        "97601e8d4de7",
        "SummonVar(5)+3, then NecroMasteryPower -- not 10/13 and not Strength",
    ),
    "NeowsFury": (
        "19c48697bb9d",
        "10/14 then up to 2/3 CHOSEN discard cards, minimum ZERO, capped by hand room",
    ),
    "Neurosurge": (
        "547ae627a131",
        "3/4 energy and 2 cards, and NeurosurgePower 3 on YOURSELF -- it Dooms you every turn",
    ),
    "NeutronAegis": (
        "01739e6c113a",
        "5 stars for Plating 8/11",
    ),
    "NoEscape": (
        "71a015232024",
        "Doom 10/15 plus 5 per FULL ten already on the target",
    ),
    "Nostalgia": (
        "7a737716050f",
        "NostalgiaPower 1; the upgrade cuts the cost",
    ),
    "NotYet": (
        "d23576d6487a",
        "heal 10/13; CanBeGeneratedInCombat is false",
    ),
    "Oblivion": (
        "28cec0ebd7c5",
        "OblivionPower 3/4 on the TARGET -- every later card that turn Dooms it for that much",
    ),
    "Offering": (
        "93f6ff753d2f",
        "6 HP, 2 energy, draw 3/5 -- the upgrade raises the CARDS, not the energy",
    ),
    "Omnislice": (
        "f0db6829de5b",
        "8/11 to the target, then its TOTAL plus overkill splashed Unpowered to the others",
    ),
    "OneTwoPunch": (
        "a56222737af0",
        "OneTwoPunchPower at the Attacks var, 1 upgrading by 1",
    ),
    "Orbit": (
        "a367fc160548",
        "OrbitPower at EnergyVar(1); the upgrade cuts COST, not the amount",
    ),
    "PactsEnd": (
        "53815c93c362",
        "17/23 to all, and ONLY when the exhaust pile holds 3+",
    ),
    "Pagestorm": (
        "6cad5c1db797",
        "PagestormPower 1: drawing an ETHEREAL card draws that many more",
    ),
    "PaleBlueDot": (
        "668bf3c26656",
        "PaleBlueDotPower 1/2: draw that many more, ONLY after five or more plays last turn",
    ),
    "Panache": (
        "d063d1dcfd21",
        "PanachePower at PanacheDamage 10 upgrading by 4",
    ),
    "PanicButton": (
        "03795b5f26a9",
        "30/40 block then NoBlockPower for 2 turns; the Turns var does not upgrade",
    ),
    "Parry": (
        "e28e91f8adc1",
        "ParryPower 10/14 -- inert, and the blade gains that much block after its attack",
    ),
    "ParticleWall": (
        "f583f01d1f1d",
        "2 stars for 9 block, and the card RETURNS TO HAND instead of discarding",
    ),
    "PerfectedStrike": (
        "c59e041d1155",
        "6 + 2/3 per CardTag.Strike in AllCards -- which includes the PlayPile, so it counts itself",
    ),
    "PhotonCut": (
        "526b6075b707",
        "10/13 damage, draw 1/2, then put ONE CHOSEN hand card on top of the draw pile",
    ),
    "Pillage": (
        "bb4f6f43b2ee",
        "6/9 then draw one at a time while the drawn card is an Attack and the hand has room",
    ),
    "PillarOfCreation": (
        "b6c1677a5bdc",
        "PillarOfCreationPower 3/4: Unpowered block for every card GENERATED",
    ),
    "PoisonedStab": (
        "2d65de36f7c5",
        "6/8 then Poison 3/4",
    ),
    "PommelStrike": (
        "18799fa29bff",
        "9/10 and draw 1/2",
    ),
    "Pounce": (
        "4ee72fbd4d78",
        "14/20 then FreeSkillPower 1",
    ),
    "PrepTime": (
        "a6111c9632c6",
        "PrepTimePower 4 upgrading by 2",
    ),
    "PrimalForce": (
        "b118064bae03",
        "every transformable Attack in hand becomes a GiantRock, upgraded if the card is",
    ),
    "Production": (
        "49d19af65d48",
        "2/3 energy, Exhaust",
    ),
    "Prolong": (
        "7508e4fb5d26",
        "BlockNextTurnPower at the owner's CURRENT block; the upgrade removes Exhaust",
    ),
    "Prophesize": (
        "ce709f690c20",
        "draw 6/9 -- the shared body it sat in drew 1/2",
    ),
    "Prowess": (
        "37cb66906f00",
        "Strength AND Dexterity, 1 each, both upgrading by 1",
    ),
    "PullFromBelow": (
        "930364783206",
        "hit count = Ethereal cards PLAYED this combat; base 0, no floor",
    ),
    "Purity": (
        "80e66546919d",
        "exhaust up to 3/5 CHOSEN cards -- CardSelectorPrefs minimum is ZERO, so it can be declined",
    ),
    "Putrefy": (
        "d072ff556580",
        "one Power var of 2/3 spent on BOTH Weak and Vulnerable, on the TARGET only",
    ),
    "Pyre": (
        "06ccde0bb08d",
        "PyrePower at EnergyVar(1), upgrading by 1",
    ),
    "Quadcast": (
        "152560e8ca9a",
        "RepeatVar(4) evokes of the FRONT orb, dequeuing on the last; the upgrade is the cost",
    ),
    "Quasar": (
        "02a6c8250d5c",
        "2 stars: three DISTINCT colourless cards offered, one taken, and skipping is allowed",
    ),
    "Rage": (
        "cfadf3ad1cf6",
        "RagePower 3/5",
    ),
    "Rainbow": (
        "8e12f5e76a5e",
        "one of Lightning, Frost, Dark; the upgrade removes Exhaust",
    ),
    "Rally": (
        "4840419816ce",
        "MultiplayerOnly; 12/17 block to each living ally, so the player alone",
    ),
    "Rampage": (
        "fe99395a9326",
        "hits for its CURRENT damage then raises its own var by 5/9; growth lives on the copy",
    ),
    "ReaperForm": (
        "ab8571e643f9",
        "ReaperFormPower 1: a powered attack Dooms for TotalDamage -- blocked plus unblocked",
    ),
    "Reboot": (
        "ec209d59f6ad",
        "hand into the draw pile, shuffle, then draw CardsVar(4) +2",
    ),
    "RefineBlade": (
        "439879250eda",
        "Forge 9/13 and an energy next turn",
    ),
    "Reflect": (
        "b0ac9b14efc7",
        "3 stars for 15/20 block and ReflectPower 1 -- BLOCKED damage goes back at the dealer",
    ),
    "Rend": (
        "02ee2f05ba1a",
        "15/18 plus 5/8 per NON-TEMPORARY debuff on the target; the upgrade raises both",
    ),
    "Resonance": (
        "1f212e7a33e2",
        "3 stars: +1/+2 Strength to the player and a flat -1 to every enemy",
    ),
    "Restlessness": (
        "afe77c487409",
        "draw 2/3 and 2/3 energy, and ONLY when it was the last card in hand",
    ),
    "RollingBoulder": (
        "38d50e459556",
        "RollingBoulderPower 5/10; IncrementAmount 5 is the power's growth",
    ),
    "RoyalGamble": (
        "2a67a322d546",
        "5 stars for NINE stars, and it Exhausts; upgrade adds Retain",
    ),
    "Royalties": (
        "43f3b31cb28c",
        "GoldVar 30/40 as its own reward row at combat end",
    ),
    "Rupture": (
        "165723b123ef",
        "RupturePower 1/2",
    ),
    "Salvo": (
        "cbec2fff1d11",
        "12/16 then RetainHandPower 1",
    ),
    "Scrawl": (
        "45e5038838e0",
        "draw until the hand is full; the upgrade adds Retain",
    ),
    "SculptingStrike": (
        "379436715a78",
        "9/12 damage, then a CHOSEN hand card gains ETHEREAL -- filtered to those without it",
    ),
    "Seance": (
        "86f68be7e66f",
        "a CHOSEN draw-pile card becomes a Soul in place; the upgrade is a discount, not a second card",
    ),
    "SecondWind": (
        "c58bbda5f4af",
        "the block gain is INSIDE the exhaust loop -- per non-Attack exhausted, not once",
    ),
    "SecretTechnique": (
        "712326758390",
        "a CHOSEN Skill from the draw pile, minimum 1 so not skippable",
    ),
    "SecretWeapon": (
        "5ffd99f94395",
        "a CHOSEN Attack from the draw pile, minimum 1 so not skippable",
    ),
    "SeekerStrike": (
        "0b84fd59f9b3",
        "9/12 then one of THREE draw-pile cards sampled by StableShuffle on CombatCardSelection",
    ),
    "SeekingEdge": (
        "59e6df5684e0",
        "an inert power the blade reads to hit ALL enemies, plus Forge 7/11",
    ),
    "SentryMode": (
        "e37e1a42ddc0",
        "SentryModePower 1: that many Sweeping Gazes into HAND before every hand draw",
    ),
    "SetupStrike": (
        "e5ed2377e470",
        "7/9 then SetupStrikePower 2/3, which is a TemporaryStrengthPower",
    ),
    "SharedFate": (
        "b4f51371fa8f",
        "PERMANENT Strength loss: 2 on the player and 2/3 on the target, both negative StrengthPowers",
    ),
    "Shatter": (
        "31e3abaee6cc",
        "hits all, then evokes the front orb TWICE per orb held",
    ),
    "Shiv": (
        "4c1f1ec8c53f",
        "4/6 damage, Exhaust; TargetType becomes AllEnemies while the owner holds FanOfKnivesPower",
    ),
    "Shockwave": (
        "17380b0e2bed",
        "Weak AND Vulnerable at 3/5 to every hittable enemy",
    ),
    "Shroud": (
        "8364a8c12d55",
        "ShroudPower 2/3: block whenever its owner applies DOOM, not block next turn",
    ),
    "ShrugItOff": (
        "cfb33fefb5a4",
        "8/11 block and draw 1",
    ),
    "SignalBoost": (
        "ad8073ddb8d1",
        "ModifyCardPlayCount +1 for POWER cards, then decrements",
    ),
    "Skewer": (
        "2e4678debc04",
        "HasEnergyCostX; 8/11 damage X times at one target",
    ),
    "SleightOfFlesh": (
        "76e743eaa827",
        "PowerVar 9/13; the power deals its amount Unpowered per DEBUFF the player lands on an enemy",
    ),
    "Slimed": (
        "066f790a162e",
        "Exhaust, and it DRAWS 1 on play -- not a do-nothing status",
    ),
    "Snap": (
        "a8b149a8f00c",
        "OstyDamage 7/10, then a HAND card of your choosing gains Retain -- the select is outside the missing-Osty guard",
    ),
    "SoulStorm": (
        "580df34b806c",
        "9 damage plus 2/3 per SOUL in the EXHAUST pile, at the target only",
    ),
    "SovereignBlade": (
        "739edf0963d8",
        "the Forge token: 10 + all forged damage, once; SeekingEdge hits all, Parry gives block",
    ),
    "SpectrumShift": (
        "e943b24eb7db",
        "SpectrumShiftPower 1: that many DISTINCT colourless cards into hand every turn",
    ),
    "Spinner": (
        "2e4bbee2ca95",
        "the Glass orb on play is the WHOLE upgrade; the power channels one at each energy reset",
    ),
    "Supercritical": ("f347dfeb9219", "EnergyVar(4) +2, Exhaust"),
    "SpiritOfAsh": (
        "cd5ba635948b",
        "a BlockOnExhaust var of 4/5, gained when an ETHEREAL card is PLAYED -- not on exhaust",
    ),
    "Spite": (
        "fec43030abca",
        "5 damage, 2/3 hits if unblocked damage was received this turn",
    ),
    "Splash": (
        "d45d8307477e",
        "three Attacks from the OTHER characters' pools, canSkip, upgraded if the card is, free this turn",
    ),
    "SpoilsOfBattle": (
        "80e64c39eb5d",
        "Forge 5/8 then draw two",
    ),
    "Stampede": (
        "a9fc2868fdae",
        "StampedePower 1; the upgrade cuts the cost",
    ),
    "Stardust": (
        "3e1566d64563",
        "HasStarCostX: 5/7 damage at RANDOM enemies once per star SPENT",
    ),
    "Stoke": (
        "43556213b53a",
        "exhaust the hand, then that many cards from GetForCombat -- duplicates allowed",
    ),
    "Stomp": (
        "370912279495",
        "12/15 to all; its cost falls by the Attacks played this turn, both on entry and per play",
    ),
    "StoneArmor": (
        "79f0cdcae3a0",
        "PlatingPower 4/6",
    ),
    "Strangle": (
        "2ae905512994",
        "8/10 then StranglePower 2/3 -- which the upgrade DOES raise; not Vulnerable",
    ),
    "Stratagem": (
        "0946b8866ed9",
        "StratagemPower 1; the upgrade cuts the cost",
    ),
    "StrikeIronclad": (
        "67c1d4a0a5d2",
        "6 damage upgrading by 3",
    ),
    "SummonForth": (
        "7de39cc98838",
        "pulls every Sovereign Blade to HAND, then Forge 8/11",
    ),
    "Supermassive": (
        "d5febe1376e7",
        "5 damage plus 3/4 per card GENERATED this combat",
    ),
    "SwordBoomerang": (
        "07bb371af7ba",
        "DamageVar(3) x 3/4 at random opponents, re-rolled per hit; the upgrade raises the REPEAT",
    ),
    "SwordSage": (
        "a943db99b1f4",
        "every SOVEREIGN BLADE gains a REPLAY, including ones that arrive later",
    ),
    "TagTeam": (
        "eac3765d3fcc",
        "MultiplayerOnly; TagTeamPower only helps OTHER players, so damage alone is the whole card solo",
    ),
    "Tank": (
        "37d5bdd8725d",
        "MultiplayerOnly; TankPower does nothing with no allies",
    ),
    "Taunt": (
        "a49b0ccef74a",
        "7/8 block and Vulnerable 1/2 -- the upgrade raises BOTH",
    ),
    "TearAsunder": (
        "90dc93ac180a",
        "5/7 x (1 + unblocked hits received this COMBAT, not this turn)",
    ),
    "Terraforming": (
        "ff1775948457",
        "VigorPower 6/8 -- Vigor, which the next attack spends, not Strength",
    ),
    "TheBomb": (
        "181cc426bc25",
        "Turns 3 flat, BombDamage 40 upgrading by 10",
    ),
    "TheGambit": (
        "87d3ab31fa9b",
        "50/75 block then TheGambitPower 1, which kills on the next unblocked powered attack",
    ),
    "TheHunt": (
        "f5fda73d95f0",
        "extra CardReward of 3 from the room's own pool, behind the Fatal gate",
    ),
    "TheSealedThrone": (
        "8644e7141677",
        "3 stars, Ancient: a STAR for every card its owner plays",
    ),
    "TheSmith": (
        "b9d799312c6c",
        "4 stars for a Forge of 30/40",
    ),
    "ThinkingAhead": (
        "d108fc8a7af1",
        "draw 2 then a CHOSEN hand card onto the draw pile top; minimum 1, so not skippable",
    ),
    "Thrash": (
        "9ba3bc8cab0c",
        "4/6 x2, then eats a random Attack and permanently ADDS its damage",
    ),
    "ThrummingHatchet": (
        "dd8e9746aa77",
        "11/14, and BeforeHandDraw returns it to hand next turn if it was played",
    ),
    "Thunderclap": (
        "332719e08b6f",
        "4/7 to all plus Vulnerable 1 to all; the upgrade raises damage only",
    ),
    "Transfigure": (
        "cf6f49a825b6",
        "a CHOSEN hand card gains a REPLAY and costs one more; upgrade only drops Exhaust",
    ),
    "TrashToTreasure": (
        "5176cb66ae7c",
        "a generated Status channels Amount RANDOM orbs on Rng.CombatOrbGeneration",
    ),
    "Tremble": (
        "88e37b1f3dc3",
        "Vulnerable 3/4, Exhaust",
    ),
    "TrueGrit": (
        "c0e7e5c71d11",
        "7/9 block; upgraded the exhaust is CHOSEN, otherwise it is CombatCardSelection",
    ),
    "TwinStrike": (
        "d7183e4fde0a",
        "5/7 x2",
    ),
    "Tyranny": (
        "0a9e35d5ee91",
        "draw one more every turn AND exhaust one CHOSEN hand card at the start of it",
    ),
    "UltimateDefend": (
        "0ed2403c30c8",
        "11/15 block",
    ),
    "UltimateStrike": (
        "6ef98ae43eeb",
        "14/20",
    ),
    "Unmovable": (
        "97a2d149b01f",
        "UnmovablePower 1; the upgrade cuts the cost",
    ),
    "Unrelenting": (
        "3bd320e53b1e",
        "14/20 then FreeAttackPower 1",
    ),
    "Uppercut": (
        "86c14967d906",
        "13 damage with NO damage upgrade; Weak and Vulnerable both at the Power var 1/2",
    ),
    "Veilpiercer": (
        "4f43e45d74bb",
        "VeilpiercerPower: ETHEREAL cards cost 0, one stack spent per Ethereal played",
    ),
    "Venerate": (
        "bd3c22b2985d",
        "GainStars 2/3, and nothing else -- no Strength, no Dexterity",
    ),
    "Vicious": (
        "2bf236b729ef",
        "ViciousPower at CardsVar 1/2",
    ),
    "VoidForm": (
        "beb38ba10164",
        "Ethereal 3-cost: the first two cards each turn cost nothing at all, and it ENDS THE TURN",
    ),
    "Volley": (
        "0fffb46d98dd",
        "HasEnergyCostX; 10/14 x X at RANDOM opponents, re-rolled per hit",
    ),
    "Voltaic": (
        "2acb93296cb5",
        "CalculatedChannels is computed ONCE from the combat's OrbChanneledEntry history",
    ),
    "BootSequence": ("e01dbe9af7e2", "BlockVar(10m) +3, Innate and Exhaust"),
    "BulkUp": (
        "0a2421e342a9",
        "OrbSlots is a literal 1 at both levels; Strength and Dexterity are what upgrade",
    ),
    "Capacitor": ("e508f28ff29f", "RepeatVar(2) +1 orb slots"),
    "Chaos": (
        "0f62a91d3f72",
        "GetRandomOrb rolls over all FIVE valid orbs on Rng.CombatOrbGeneration",
    ),
    "Chill": (
        "9b26e393b3c2",
        "one Frost per hittable enemy; the upgrade removes Exhaust",
    ),
    "Compact": (
        "710b256c3dcd",
        "BlockVar(6m) upgrades by ONE; transforms every transformable Status in hand to Fuel",
    ),
    "Darkness": (
        "a12b11f0b2b2",
        "channel Dark, then fire EVERY Dark orb's passive, twice each when upgraded",
    ),
    "DoubleEnergy": (
        "570b80131645",
        "GainEnergy(current energy) -- doubles what is left after paying for it",
    ),
    "EnergySurge": (
        "e58d97272713",
        "MultiplayerOnly; EnergyVar(2) +1 to every living ally",
    ),
    "Feral": (
        "7ef74f19df0f",
        "FeralPower returns a 0-cost ATTACK to hand instead of the discard, Amount times a turn",
    ),
    "FightThrough": ("3aef3edb6d12", "BlockVar(13m) +4 and two Wounds"),
    "Ftl": (
        "b176bb6a6ce8",
        "CardPlaysFinished this turn excludes the Ftl itself, which has not finished",
    ),
    "Fusion": ("3eeb08ffc723", "one Plasma; the upgrade removes Exhaust"),
    "Glacier": ("c73f81cce1b2", "BlockVar(6m) +3 and TWO Frost"),
    "Glasswork": ("5b76d459d83e", "BlockVar(5m) +3 and one Glass"),
    "Hailstorm": (
        "2ef18de7a9e5",
        "BeforeSideTurnEnd, gated on holding at least one FROST orb",
    ),
    "Iteration": (
        "16078bc1e3a9",
        "AfterCardDrawn on the FIRST Status of the turn draws Amount -- not a next-turn draw",
    ),
    "Loop": (
        "721bb266ec01",
        "AfterPlayerTurnStart fires the FRONT orb's passive Amount times",
    ),
    "Null": ("42a9e3faad6f", "both vars upgrade, then a Dark orb"),
    "Overclock": ("27e70081f004", "CardsVar(2) +1 and a Burn"),
    "Refract": (
        "0416ab50ef9a",
        "WithHitCount(2) -- 9 twice; RepeatVar(2) is the ORB count and does not upgrade",
    ),
    "RocketPunch": (
        "be1632392101",
        "AfterCardGeneratedForCombat on a Status calls EnergyCost.SetUntilPlayed(0) on itself",
    ),
    "Scavenge": (
        "f3117ff2fd54",
        "CardSelectCmd.FromHand for the exhaust -- the player picks what burns",
    ),
    "Scrape": (
        "189e0c727d6f",
        "draws, then discards the drawn cards that do not cost zero",
    ),
    "ShadowShield": ("67396ea772fc", "BlockVar(11m) +4 and one Dark"),
    "Skim": ("e8d7f3058e7d", "CardsVar(3) +1"),
    "Smokestack": (
        "31a949c4f69a",
        "AfterCardGeneratedForCombat on a Status hits every enemy for Amount, unpowered",
    ),
    "Storm": (
        "53893dc93a20",
        "BeforeCardPlayed records the amount for POWER cards; AfterCardPlayed channels THAT",
    ),
    "Subroutine": (
        "b8ae1c4472ca",
        "the same before/after reading as Storm, paying energy instead of orbs; literal 1m",
    ),
    "Sunder": (
        "6175a3ee5d03",
        "EnergyVar(3) refunded only if the attack KILLED; the damage is what upgrades",
    ),
    "Synchronize": (
        "fea9f4dcf64c",
        "CalculatedFocus is extra 2 times the DISTINCT orb count, and it is temporary",
    ),
    "Synthesis": (
        "d0ef7cdc7869",
        "FreePowerPower -- the next POWER is free, and Synthesis is an Attack",
    ),
    "Tempest": (
        "448d97582b3a",
        "X-cost, one Lightning per energy spent, plus one when upgraded",
    ),
    "TeslaCoil": (
        "f37f58d22b32",
        "every LIGHTNING orb fires its passive at the card's target, twice when upgraded",
    ),
    "Thunder": (
        "a42888593e4b",
        "AfterOrbEvoked on a Lightning orb adds Amount at that orb's targets",
    ),
    "Whirlwind": (
        "5d4c9161e732",
        "HasEnergyCostX; 5/8 x the X value to all enemies",
    ),
    "Whistle": (
        "24a523b51488",
        "33 damage then CreatureCmd.Stun -- a genuine stun, unlike Knockdown's power",
    ),
    "WhiteNoise": (
        "fb9249522b8f",
        "a random POWER from the character's pool, free this turn, into HAND",
    ),
    "Abrasive": (
        "e152f4afdd1e",
        "OnUpgrade names THORNS only, so the Dexterity is 1 at both levels",
    ),
    "Adrenaline": (
        "446fa8f0c221",
        "OnUpgrade names ENERGY only, so the draw is 2 at both levels",
    ),
    "Afterimage": (
        "c236c576a77a",
        "E173: BeforeCardPlayed records the amount and AfterCardPlayed spends THAT; the block is Unpowered",
    ),
    "Assassinate": ("a051e9ecff82", "both vars upgrade; Innate and Exhaust"),
    "Backflip": ("7c61de5e136b", "block upgrades, the CardsVar(2) draw does not"),
    "BallLightning": ("f2baf9e878e7", "hit, then channel Lightning"),
    "Barrage": ("03acaf7ec63f", "hit count is the orb COUNT, whatever they are"),
    "BeamCell": ("0af09001f220", "both vars upgrade"),
    "BladeDance": ("21577c5c857c", "CardsVar(3) +1, Exhaust"),
    "BladeOfInk": (
        "2eb056bfa62e",
        "E160: its Shivs are Inky-enchanted, +1 damage and Weak 1, both from the enchantment's own vars rather than its amount",
    ),
    "BoostAway": ("1d8ef6461c5e", "block, then a Dazed into the discard"),
    "BubbleBubble": (
        "e7ebd65364c7",
        "the whole effect is gated on the target already having PoisonPower",
    ),
    "CalculatedGamble": (
        "e3bc361fc7a5",
        "CardCmd.DiscardAndDraw, so Sly fires; the upgrade adds Retain",
    ),
    "ChargeBattery": (
        "c5d6c3cf0a90",
        "EnergyNextTurnPower(1) at both levels; OnUpgrade names the block",
    ),
    "Claw": (
        "5cc41597c0d1",
        "every Claw in AllCards gains the Increase, so the second hits for 5",
    ),
    "CloakAndDagger": (
        "51f489460688",
        "OnUpgrade names the CARDS var, so the block is 6 at both levels",
    ),
    "ColdSnap": ("c51e38a0a8cb", "hit, then channel Frost"),
    "CompileDriver": (
        "0f86ce0c38fe",
        "draws one per DISTINCT orb type -- group orb by orb.Id",
    ),
    "Coolheaded": ("051b06680fda", "channel Frost, then draw; CardsVar(1) +1"),
    "DaggerSpray": ("c9e411c15435", "WithHitCount(2) across all opponents"),
    "DefendDefect": ("f777302db6f2", "BlockVar(5m) +3, tagged CardTag.Defend"),
    "DefendSilent": (
        "da124b4c7b2e",
        "E175: tagged CardTag.Defend, which FastenPower reads",
    ),
    "Deflect": ("171879a2119f", "BlockVar(4m) +3, free"),
    "DodgeAndRoll": (
        "36d4abe1ba64",
        "E164: the power is applied at the block ACTUALLY gained, which CreatureCmd.GainBlock returns",
    ),
    "Dualcast": (
        "0983f2939dff",
        "evokes the FRONT orb twice -- once without dequeue, once with -- not the two front orbs",
    ),
    "EchoingSlash": (
        "15bd7515193a",
        "E159: the volley repeats once per creature it killed, and those repeats can kill in turn",
    ),
    "Envenom": (
        "1f4672fda9d1",
        "AfterDamageGiven needs IsPoweredAttack AND UnblockedDamage > 0",
    ),
    "EscapePlan": (
        "0707ad841f9a",
        "E168: the type check is on the card DRAWN, not on the draw pile's top",
    ),
    "Expertise": (
        "f755221e807e",
        "draws max(0, 6 - hand.Count) -- a top-up, not a draw",
    ),
    "FlickFlack": ("8a0333b2e8b3", "AllEnemies and Sly"),
    "FocusedStrike": (
        "eed7e570e1d7",
        "FocusedStrikePower is a TemporaryFocusPower, handed back at end of turn",
    ),
    "Footwork": ("f9410c1c3867", "PowerVar<DexterityPower>(2m) +1"),
    "GoForTheEyes": (
        "b768335e24d9",
        "E184: IntendsToAttack is Any() over the move's intents, not just the first",
    ),
    "GunkUp": (
        "b1cb72e05d4f",
        "RepeatVar(3) is not upgraded; the per-hit damage is. Slimed to discard",
    ),
    "Hologram": (
        "cd7db8eaafbc",
        "E183: CardSelectCmd.FromCombatPile over the DISCARD pile -- the player picks",
    ),
    "Hotfix": (
        "62a5d35f24ec",
        "PowerVar(2m) at both levels, TEMPORARY; the upgrade only removes Exhaust",
    ),
    "InfiniteBlades": (
        "82aa8d461cfc",
        "E169: BeforeHandDraw, so the Shiv takes its slot before the draw; the upgrade adds Innate",
    ),
    "LeadingStrike": (
        "eb91532f761f",
        'CardsVar("Shivs", 2) is not upgraded; the damage is',
    ),
    "Leap": ("d49fcb3c398f", "BlockVar(9m) +3"),
    "LegSweep": ("ee5383930caf", "E166: PowerVar<WeakPower>(2m) +1, not 3/4"),
    "LightningRod": (
        "92d73eba5860",
        "E180: channels a Lightning ORB at each AfterEnergyReset and decrements; 2 at both levels",
    ),
    "MomentumStrike": (
        "394de7899ac9",
        "E182: EnergyCost.SetThisCombat(0) -- free for the rest of the combat once played",
    ),
    "Neutralize": ("fb3938adbc47", "both vars upgrade, 0-cost"),
    "Nightmare": (
        "093e6009e06d",
        "E158: asks WHICH card, and the three clones arrive at the next BeforeHandDraw; the count is a literal 3 at both levels",
    ),
    "PiercingWail": (
        "6e0e8dc03b01",
        "TemporaryStrengthPower with IsPositive false, on every hittable enemy; Artifact eats it",
    ),
    "Predator": (
        "a7baf33b248b",
        "E163: DrawCardsNextTurnPower takes a literal 2m at both levels",
    ),
    "Reflex": ("f81451fe198b", "CardsVar(2) +1, 3-cost, Sly"),
    "Ricochet": (
        "9f623d51deff",
        "E162: AttackCommand rolls a target INSIDE the per-hit loop; the damage never upgrades, the repeat does",
    ),
    "Scare": (
        "f1a75a22a121",
        "E165: Weak 1 to every hittable enemy, a literal with no var; the upgrade removes Exhaust",
    ),
    "ShadowStep": (
        "bc6dd7d7335f",
        "E171: ShadowStepPower becomes DoubleDamagePower at the next turn start, then removes itself",
    ),
    "Shadowmeld": (
        "7eb3bacc8747",
        "E172: ModifyBlockMultiplicative is 2^Amount and does not read props, so unpowered block doubles too",
    ),
    "Slice": ("dd6038bbfe04", "DamageVar(6m) +3, free"),
    "Snakebite": ("70a215891521", "PowerVar<PoisonPower>(7m) +3, Retain"),
    "StormOfSteel": (
        "c47d66e7dd80",
        "hand size read BEFORE the discard; the upgrade upgrades the Shivs, not their count",
    ),
    "StrikeDefect": (
        "7f91a909dbf8",
        "DamageVar(6m) +3, the Silent Strike with a different portrait",
    ),
    "StrikeSilent": (
        "353b3a19c6f6",
        "DamageVar(6m) +3; tagged CardTag.Strike, which nothing reads yet",
    ),
    "SuckerPunch": (
        "c8dac7951530",
        "both vars upgrade -- the counterexample to Predator",
    ),
    "Suppress": (
        "de643430ecb8",
        "CardRarity.Ancient, not Rare -- outside the ordinary reward pool",
    ),
    "SweepingBeam": (
        "a4662c6806d7",
        "AllEnemies, then draw; CardsVar(1) is not upgraded",
    ),
    "Tactician": ("9930e634f49e", "EnergyVar(1) +1, Sly -- the point is to discard it"),
    "ToolsOfTheTrade": (
        "49661a25197a",
        "E174: ModifyHandDraw plus a COMPULSORY but CHOSEN discard at AfterPlayerTurnStart",
    ),
    "Turbo": (
        "544a156d21cd",
        "E181: the VOID into the discard is the card's whole cost",
    ),
    "Untouchable": (
        "a48822bd3df7",
        "E161: one GainBlock and nothing else; the draw pile never came into it",
    ),
    "UpMySleeve": (
        "1ccc99163ea9",
        "E167: CardsVar(3) +1, and EnergyCost.AddThisCombat(-1) on every play",
    ),
    "Uproar": (
        "513ccfe35585",
        "E185: StableShuffles the playable Attacks on Rng.Shuffle and takes the first",
    ),
    "WellLaidPlans": (
        "d437e8aecf97",
        "E170: BeforeFlushLate raises a min-0 selection every turn and gives each pick GiveSingleTurnRetain",
    ),
    "Zap": ("7552bd742ca7", "one Lightning channel; the upgrade is the cost"),
    "WroughtInWar": (
        "48a62bd32743",
        "7/9 damage and a Forge of 7/9",
    ),
}


def load_read_notes() -> dict[str, tuple[str, str]]:
    return READ


def generated_names() -> set[str]:
    return set(re.findall(r'Name: "(\w+)"', GENERATED.read_text(encoding="utf-8")))


def implemented_names() -> set[str]:
    return set(re.findall(r'"(\w+)",', IMPLEMENTED.read_text(encoding="utf-8")))


def pending_names() -> set[str]:
    text = COVERAGE.read_text(encoding="utf-8")
    body = text[text.index("Pending") :]
    return set(re.findall(r'^\s+"(\w+)",', body, re.MULTILINE))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--card", default=None, help="audit just this one")
    parser.add_argument(
        "--unread",
        action="store_true",
        help="list every unread card rather than counting them",
    )
    parser.add_argument(
        "--digests",
        nargs="*",
        default=None,
        metavar="CARD",
        help="print digests for these cards, for writing into READ (all cards if empty)",
    )
    args = parser.parse_args()

    known = generated_names()
    implemented = implemented_names()
    pending = pending_names()
    notes = load_read_notes()

    read: list[str] = []
    unread: list[str] = []
    stale: list[tuple[str, str, str]] = []
    missing_source: list[str] = []

    for name in sorted(known):
        if args.card and name != args.card:
            continue
        path = CARDS / f"{name}.cs"
        if not path.exists():
            # LOUD rather than skipped: a card the audit cannot find is a card it silently
            # reports as fine, and a renamed class is exactly when that would happen.
            missing_source.append(name)
            continue

        digest = card_digest(path.read_text(encoding="utf-8"))
        if args.digests is not None:
            if not args.digests or name in args.digests:
                print(f'    "{name}": ("{digest}", ""),')
            continue

        note = notes.get(name)
        if note is None:
            unread.append(name)
        elif note[0] != digest:
            stale.append((name, note[0], digest))
        else:
            read.append(name)

    if args.digests is not None:
        return

    audited = len(read) + len(unread) + len(stale)
    print(f"{len(read)}/{audited} cards read against the current source")

    # The headline. A card with a test and no reading LOOKS covered, and every divergence
    # this audit was built after was found in a card in exactly that state.
    tested_unread = [n for n in unread if n in implemented and n not in pending]
    if tested_unread:
        print(
            f"\n{len(tested_unread)} card(s) have a TEST SUITE and no reading behind it. "
            "A test written from a wrong reading passes forever:",
        )
        for i in range(0, min(len(tested_unread), 24), 6):
            print("  " + ", ".join(tested_unread[i : i + 6]))
        if len(tested_unread) > 24:
            print(
                f"  ... and {len(tested_unread) - 24} more (--unread for the full list)",
            )

    if args.unread:
        print(f"\nunread ({len(unread)}):")
        for i in range(0, len(unread), 6):
            print("  " + ", ".join(unread[i : i + 6]))
    elif unread:
        print(f"\n{len(unread)} unread. `--unread` lists them.")

    if missing_source:
        print(
            f"\n{len(missing_source)} generated card(s) have no decompiled source: "
            + ", ".join(missing_source[:12]),
        )
        print(
            "Renamed, or the extractor and the dump are out of step. Nothing is known "
            "about these either way.",
        )

    if stale:
        print(
            "\nREAD notes whose SOURCE HAS CHANGED since it was read -- re-read these "
            "before trusting anything about them:",
        )
        for name, was, now in stale:
            print(f"  {name}: read against {was}, the source now digests to {now}")
        raise SystemExit(1)


if __name__ == "__main__":
    main()
