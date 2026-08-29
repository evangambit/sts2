#!/usr/bin/env python3
"""Which relics the emulator MODELS, and which have been read against the source.

Cards have two guards: `CardCoverageTests.Pending` says a card has a test, and
`audit_cards.py` says someone compared it to the source. Relics had neither. Nothing in
the repo could answer "how many relics does the emulator implement", which meant nobody
could notice the answer drifting -- and the answer turns out to be about half.

Four states, and the middle two are the point:

  unmodelled  no id constant anywhere. The emulator can hand the player this relic and
              nothing will happen. A worklist, not a failure.
  declared    an id constant exists and is never USED. Someone named the relic and
              stopped -- which reads exactly like a modelled relic from the constant
              block, and is why "is it in RelicEffects" is not the question to ask.
  unread      wired up, but nobody has compared it to the decompiled source.
  read        in READ, and the source still digests the same.
  STALE       in READ, and the source has CHANGED. Exit code 1.

The number worth watching is `modelled but unread`, for the reason `audit_cards.py`
exists: a relic someone wrote from a wrong reading behaves wrongly forever, and one
written from a right reading of an old source drifts silently when the game moves.

`--reachable` narrows to the relics an ordinary run can actually be handed. Event-pool
relics need their event to be walked first, so they are real but further away.

    uv run python scripts/audit_relics.py
    uv run python scripts/audit_relics.py --unmodelled --reachable
    uv run python scripts/audit_relics.py --digests BurningBlood
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

# The stripper is IMPORTED rather than copied. A duplicated body that drifts from its
# original is the single most common defect this repo has found in itself -- six times at
# last count -- and a digest function that disagreed with its twin would silently re-flag
# every note in one of the two audits.
from audit_cards import card_digest

REPO = Path(__file__).resolve().parent.parent
RELICS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Relics"
POOLS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.RelicPools"
CHARACTERS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Characters"
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated" / "Relics.g.cs"
ENGINE = REPO / "src" / "Sts2Emulator"

CONST = re.compile(r"public const int (\w+)\s*=\s*(\d+);")
RELIC_DEF = re.compile(r'Id: (\d+), Name: "(\w+)"')


def generated_relics() -> dict[str, int]:
    return {
        m.group(2): int(m.group(1))
        for m in RELIC_DEF.finditer(GENERATED.read_text(encoding="utf-8"))
    }


def engine_sources() -> list[tuple[Path, str]]:
    out = []
    for f in sorted(ENGINE.rglob("*.cs")):
        parts = set(f.parts)
        if "obj" in parts or "bin" in parts:
            continue
        out.append((f, f.read_text(encoding="utf-8")))
    return out


def id_constants(
    sources: list[tuple[Path, str]],
    relics: dict[str, int],
) -> dict[str, set[str]]:
    """Relic name -> every constant that names it.

    Matching on the VALUE as well as the name is what makes this precise: a bare name
    search over the tree matches comments and unrelated identifiers, and a bare id search
    matches every literal in the file. A constant whose name and value both line up with a
    generated relic is the emulator saying "I know this relic".

    A SET rather than one constant, because several relics have two — `RelicEffects.
    LizardTail` and `RunConstants.RelicLizardTail` name the same thing. Keeping only the
    last one found made a relic used through the other alias read as declared-and-unused,
    which is precisely the wrong answer this function exists to avoid.
    """
    found: dict[str, set[str]] = {}
    for _, text in sources:
        for name, value in CONST.findall(text):
            base = name.removeprefix("Relic")
            if relics.get(base) == int(value):
                found.setdefault(base, set()).add(name)
    return found


def used_constants(
    sources: list[tuple[Path, str]],
    consts: dict[str, set[str]],
    relics: dict[str, int],
) -> set[str]:
    """Which relics are actually REACHED, by any of the routes the engine uses.

    Two routes, and missing either one under-reports. A constant referenced somewhere
    other than its own declaration is the common case. `GeneratedData.Relics.FindId("X")`
    is the other: Circlet is wired that way and has no constant at all.
    """
    used = set()
    for relic, aliases in consts.items():
        hits = 0
        for _, text in sources:
            for const in aliases:
                hits += len(re.findall(rf"\b{const}\b", text))
                hits -= len(re.findall(rf"public const int {const}\s*=", text))
        if hits > 0:
            used.add(relic)

    for _, text in sources:
        for name in re.findall(r'Relics\.FindId\("(\w+)"\)', text):
            if name in relics:
                used.add(name)
    return used


def relic_pools() -> dict[str, str]:
    pools: dict[str, str] = {}
    for f in sorted(POOLS.glob("*.cs")):
        for name in re.findall(
            r"ModelDb\.Relic<(\w+)>\(\)",
            f.read_text(encoding="utf-8"),
        ):
            pools.setdefault(name, f.stem)
    return pools


def starter_relics() -> dict[str, str]:
    """Relic -> character. Every run of that character carries it from turn zero."""
    out: dict[str, str] = {}
    for f in sorted(CHARACTERS.glob("*.cs")):
        for name in re.findall(
            r"StartingRelics =>.*?ModelDb\.Relic<(\w+)>",
            f.read_text(encoding="utf-8"),
            re.DOTALL,
        ):
            out[name] = f.stem
    return out


# Relics whose decompiled source has been READ against the emulator's implementation, with
# the digest of the source that was read. Same contract as `audit_cards.READ`: the entry is
# a claim about a VERSION, and it re-flags when the game moves underneath it.
#
# Empty on purpose. The 156 the emulator models were written before this audit existed and
# nobody can now say which version of the source they were written against; seeding a
# guessed digest would put exactly the false confidence here that the file exists to
# remove. They read as unread, which is true.
READ: dict[str, tuple[str, str]] = {
    "Girya": (
        "172ad3bb2bdb",
        "TryModifyRestSiteOptions offers LIFT while TimesLifted < 3; AfterRoomEntered(CombatRoom) applies that much Strength",
    ),
    "Shovel": (
        "c2680c695562",
        "TryModifyRestSiteOptions offers DIG unconditionally; it pulls the next relic from the FRONT of the bag",
    ),
    "BeatingRemnant": (
        "0071ec697a57",
        "caps the TURN's total unblocked damage at 20, not one hit; the running total resets at side-turn start",
    ),
    "Bellows": (
        "720fc94e1c7f",
        "AfterPlayerTurnStart on turn 1 upgrades every card in the OPENING hand",
    ),
    "Chandelier": (
        "057b30d9f8f4",
        "AfterSideTurnStart on TurnNumber == 3 exactly, EnergyVar(3)",
    ),
    "GamePiece": ("9d01bc3a78a2", "AfterCardPlayed on a POWER draws CardsVar(1)"),
    "IceCream": (
        "37fe63ac1cd9",
        "ShouldPlayerResetEnergy is false from turn two on, so unspent energy CARRIES",
    ),
    "IntimidatingHelmet": (
        "78a6f3b85a41",
        "BeforeCardPlayed with Resources.EnergyValue >= 2 gains BlockVar(4, Unpowered)",
    ),
    "PrayerWheel": (
        "f0d485d1704e",
        "TryModifyRewards adds a whole extra CardReward of three after a MONSTER room",
    ),
    "RainbowRing": (
        "4d48d3922c06",
        "one Attack, one Skill and one Power in a turn pays 1 Strength and 1 Dexterity, ONCE",
    ),
    "SturdyClamp": (
        "d7d32c98d21c",
        "ShouldClearBlock false, then AfterPreventingBlockClear trims anything over 10",
    ),
    "TheCourier": (
        "09d0dd9f1a48",
        "ModifyMerchantPrice times (1 - 20/100), and it refills merchant entries",
    ),
    "TungstenRod": ("42898f97e3a9", "ModifyHpLostAfterOsty is Math.Max(0, amount - 1)"),
    "UnceasingTop": (
        "122c6a203064",
        "AfterHandEmptied draws one, during the PLAY phase only",
    ),
    "VexingPuzzlebox": (
        "8f845e1b070f",
        "AfterPlayerTurnStart on turn 1 adds a card from the WHOLE pool, free for the turn",
    ),
    "WhiteStar": (
        "c8f0af5c2f10",
        "TryModifyRewards adds an extra CardReward after an ELITE, drawn from the BOSS pool",
    ),
    "AmethystAubergine": (
        "3da23ab46ccb",
        "TryModifyRewards adds GoldVar(15) after any combat room except the final act's boss",
    ),
    "EternalFeather": (
        "47d8d2ea33c6",
        "AfterRoomEntered(RestSiteRoom) heals HealVar(3) per CardsVar(5) cards, integer division",
    ),
    "JuzuBracelet": (
        "cb0cf7bf505b",
        "ModifyUnknownMapPointRoomTypes removes Monster from the SET before the odds roll",
    ),
    "Pantograph": (
        "6f94d1df7f97",
        "AfterRoomEntered only sets a display status; the HealVar(25) is BeforeCombatStart on a BOSS room",
    ),
    "Planisphere": (
        "c87591f85362",
        "AfterRoomEntered heals 5 when the MAP POINT was Unknown, whatever the room turned out to be",
    ),
    "BookOfFiveRings": (
        "65acd90720bc",
        "CardsAddedSinceLastTrigger is CardsAdded % 5, so it heals 20 on every fifth card",
    ),
    "BowlerHat": (
        "d8192144689c",
        "ModifyGoldGained times a DynamicVar(GoldIncrease, 1.25m)",
    ),
    "Candelabra": (
        "9f2b129bf03f",
        "AfterSideTurnStart on TurnNumber == 2 exactly, not from turn two onwards",
    ),
    "JossPaper": (
        "dacacf31828a",
        "every FIVE cards exhausted draws one; ETHEREAL exhausts are banked to AfterSideTurnEnd",
    ),
    "LuckyFysh": (
        "7da5cc2d6b6f",
        "AfterCardChangedPiles into the DECK gains GoldVar(15) -- per card, not per reward",
    ),
    "MercuryHourglass": (
        "37a2fa255576",
        "AfterPlayerTurnStart, DamageVar(3m, Unpowered) to every hittable enemy",
    ),
    "MiniatureCannon": (
        "2fdc7d0c0baa",
        "ModifyDamageAdditive +3 on a powered attack from an UPGRADED card",
    ),
    "PenNib": (
        "c352060f7e5d",
        "every TENTH Attack is doubled; the counter rises in BeforeCardPlayed and wraps at ten",
    ),
    "PetrifiedToad": (
        "44e106daea2f",
        "BeforeCombatStartLate procures a PotionShapedRock, failing silently on a full belt",
    ),
    "PotionBelt": ("ceb063db9d09", "AfterObtained GainMaxPotionCount(2)"),
    "ReptileTrinket": (
        "b45edc129fd7",
        "AfterPotionUsed applies ReptileTrinketPower, a TemporaryStrengthPower of 3",
    ),
    "RippleBasin": (
        "1a90c07dfd95",
        "BeforeSideTurnEnd, 4 unpowered block if NO Attack was played that turn",
    ),
    "StrikeDummy": (
        "f026030df1d7",
        "ModifyDamageAdditive +3 on a powered attack from a card tagged CardTag.Strike",
    ),
    "Vambrace": (
        "661865f6a216",
        "the FIRST card block of a combat is doubled; it latches only once an amount above zero lands",
    ),
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--relic", default=None, help="audit just this one")
    parser.add_argument(
        "--unmodelled",
        action="store_true",
        help="list the unmodelled relics in full",
    )
    parser.add_argument(
        "--reachable",
        action="store_true",
        help="only relics an ordinary run can be handed (skip the event pool)",
    )
    parser.add_argument(
        "--digests",
        nargs="*",
        default=None,
        metavar="RELIC",
        help="print digests for these relics, for writing into READ (all if empty)",
    )
    args = parser.parse_args()

    relics = generated_relics()
    sources = engine_sources()
    consts = id_constants(sources, relics)
    used = used_constants(sources, consts, relics)
    pools = relic_pools()
    starters = starter_relics()

    read: list[str] = []
    unread: list[str] = []
    declared: list[str] = []
    unmodelled: list[str] = []
    stale: list[tuple[str, str, str]] = []
    missing_source: list[str] = []

    for name in sorted(relics):
        if args.relic and name != args.relic:
            continue
        if args.reachable and pools.get(name) == "EventRelicPool":
            continue

        path = RELICS / f"{name}.cs"
        if not path.exists():
            # LOUD, not skipped: a relic the audit cannot find is one it reports as fine.
            missing_source.append(name)
            continue

        digest = card_digest(path.read_text(encoding="utf-8"))
        if args.digests is not None:
            if not args.digests or name in args.digests:
                print(f'    "{name}": ("{digest}", ""),')
            continue

        if name not in consts and name not in used:
            unmodelled.append(name)
        elif name not in used:
            declared.append(name)
        elif (note := READ.get(name)) is None:
            unread.append(name)
        elif note[0] != digest:
            stale.append((name, note[0], digest))
        else:
            read.append(name)

    if args.digests is not None:
        return

    total = len(read) + len(unread) + len(declared) + len(unmodelled) + len(stale)
    modelled = len(read) + len(unread) + len(stale)
    scope = "reachable relics" if args.reachable else "relics"
    print(
        f"{modelled}/{total} {scope} are modelled; {len(read)} of those have been read",
    )

    if unread:
        print(
            f"\n{len(unread)} modelled and UNREAD. Wired up, never compared to the source:",
        )
        for i in range(0, min(len(unread), 24), 5):
            print("  " + ", ".join(unread[i : i + 5]))
        if len(unread) > 24:
            print(f"  ... and {len(unread) - 24} more")

    if declared:
        print(
            f"\n{len(declared)} DECLARED BUT UNUSED -- an id constant and no code behind it. "
            "These read as modelled from the constant block and are not:",
        )
        for i in range(0, len(declared), 5):
            print("  " + ", ".join(declared[i : i + 5]))

    starter_gaps = [n for n in unmodelled + declared if n in starters]
    if starter_gaps:
        print(
            "\nSTARTER relics with nothing behind them — every run of that character:",
        )
        for n in sorted(starter_gaps):
            print(f"  {starters[n]:16} {n}")

    if unmodelled:
        if args.unmodelled:
            print(f"\nunmodelled ({len(unmodelled)}):")
            for i in range(0, len(unmodelled), 5):
                print("  " + ", ".join(unmodelled[i : i + 5]))
        else:
            print(f"\n{len(unmodelled)} unmodelled. `--unmodelled` lists them.")

    if missing_source:
        print(
            f"\n{len(missing_source)} generated relic(s) have no decompiled source: "
            + ", ".join(missing_source[:12]),
        )
        print("Renamed, or the extractor and the dump are out of step.")

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
