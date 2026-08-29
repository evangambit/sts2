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
    "AdaptiveStrike": (
        "5a7e572eee34",
        "a clone of itself with SetThisCombat(0) into the discard",
    ),
    "AllForOne": (
        "078fa2a6ae3b",
        "every 0-cost Attack/Skill/Power in the DISCARD returns to hand; the filter excludes Status and Curse",
    ),
    "BiasedCognition": (
        "d4f233f5adf2",
        "TWO vars: FocusPower(4m) +1, and BiasedCognitionPower(1m) which drains a Focus every turn",
    ),
    "BlackHole": (
        "21c730e6ad53",
        "applies BlackHolePower 3/4; the POWER hits all enemies Unpowered on stars gained",
    ),
    "Buffer": (
        "6d70e0ecc365",
        "PowerVar<BufferPower>(1m) +1; each charge zeroes one instance of HP loss",
    ),
    "ConsumingShadow": (
        "ab55da889c51",
        "RepeatVar(2) +1 Dark orbs, and a power that evokes the LAST orb at each side turn end",
    ),
    "Coolant": (
        "5b1ac7befd20",
        "AfterSideTurnStart blocks distinct-orb-types times Amount, unpowered",
    ),
    "CreativeAi": (
        "19982becfe13",
        "BeforeHandDraw adds Amount random POWER cards to hand; the upgrade is the cost",
    ),
    "Defragment": ("3e9d0372c793", "PowerVar<FocusPower>(1m) +1, permanent"),
    "EchoForm": (
        "53673a33b554",
        "ModifyCardPlayCount +1 while the turn's first-in-series plays are under Amount; upgrade removes Ethereal",
    ),
    "ExpectAFight": (
        "722db1388dbf",
        "gains 1 energy per Attack in hand, THEN NoEnergyGainPower on itself",
    ),
    "Feed": (
        "9a10f38d7248",
        "GainMaxHp heals as well as raising the cap; gated on ShouldOwnerDeathTriggerFatal",
    ),
    "FeelNoPain": (
        "c6a7a56768d2",
        "CreatureCmd.GainBlock(..., Unpowered) -- the command, so Juggernaut sees it",
    ),
    "FlakCannon": (
        "5dea026eae0b",
        "exhausts every Status outside the exhaust pile FIRST, then hits once per status at rolled targets",
    ),
    "GeneticAlgorithm": (
        "9c7cc2b93b94",
        "BlockVar(CurrentBlock) starts at 1 and rises by IntVar(Increase, 3m) per play, on the card AND its DeckVersion",
    ),
    "HandOfGreed": (
        "6d55f411d5c9",
        "same Fatal gate as Feed -- Minion AND Reattach, not Minion alone",
    ),
    "HelixDrill": (
        "0cf3cb9ebaea",
        "hit count is the turn's EnergySpentEntry total minus its own cost, which is zero",
    ),
    "Hyperbeam": (
        "9f924a47ae24",
        "PowerVar<FocusPower>(3m) is NOT upgraded -- only the damage is; the Focus is spent, not gained",
    ),
    "IceLance": ("52b81d7a8986", "RepeatVar(3) Frost, not upgraded; the damage is"),
    "Ignition": (
        "8f6b9df8dc23",
        "MultiplayerOnly; channels Plasma on the TARGET ally, which is the player alone",
    ),
    "MachineLearning": (
        "b89f428eb295",
        "ModifyHandDraw + CardsVar(1); the upgrade adds Innate",
    ),
    "MeteorStrike": ("15edd7313998", "5-cost, 24 +6, and three Plasma"),
    "MultiCast": (
        "f22064bd3dad",
        "X-cost; evokes the FRONT orb X times, dequeuing only on the last",
    ),
    "NecroMastery": (
        "97601e8d4de7",
        "SummonVar(5)+3, then NecroMasteryPower -- not 10/13 and not Strength",
    ),
    "Orbit": (
        "a367fc160548",
        "OrbitPower at EnergyVar(1); the upgrade cuts COST, not the amount",
    ),
    "Quadcast": (
        "152560e8ca9a",
        "RepeatVar(4) evokes of the FRONT orb, dequeuing on the last; the upgrade is the cost",
    ),
    "Rainbow": (
        "8e12f5e76a5e",
        "one of Lightning, Frost, Dark; the upgrade removes Exhaust",
    ),
    "Reboot": (
        "ec209d59f6ad",
        "hand into the draw pile, shuffle, then draw CardsVar(4) +2",
    ),
    "Shatter": (
        "31e3abaee6cc",
        "hits all, then evokes the front orb TWICE per orb held",
    ),
    "SignalBoost": (
        "ad8073ddb8d1",
        "ModifyCardPlayCount +1 for POWER cards, then decrements",
    ),
    "Spinner": (
        "2e4bbee2ca95",
        "the Glass orb on play is the WHOLE upgrade; the power channels one at each energy reset",
    ),
    "Supercritical": ("f347dfeb9219", "EnergyVar(4) +2, Exhaust"),
    "TheHunt": (
        "f5fda73d95f0",
        "extra CardReward of 3 from the room's own pool, behind the Fatal gate",
    ),
    "TrashToTreasure": (
        "5176cb66ae7c",
        "a generated Status channels Amount RANDOM orbs on Rng.CombatOrbGeneration",
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
