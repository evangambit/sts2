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

`--reachable` narrows to the relics an ACT 1 run can actually end up holding: the shared
pool (shops, chests, combat rewards), plus anything an Act 1 event names, plus anything a
reachable relic replaces itself with. It used to mean "not in EventRelicPool", which is a
different question and a misleading answer -- events happen in Act 1, and the flag was also
dropping the three relics that are in the shared pool AND the event pool. Reporting
"156/156 reachable" off the old filter is how fourteen obtainable relics were called
unreachable.

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


def relic_pools() -> dict[str, set[str]]:
    """Relic -> EVERY pool that lists it.

    A set, not a single name. This used to `setdefault` the first pool file in alphabetical
    order, which silently filed the three relics that are in BOTH `EventRelicPool` and
    `SharedRelicPool` -- Lasting Candy, Razor Tooth, Sparkling Rouge -- as event-only, and
    `--reachable` then dropped them. They come out of the ordinary relic queue like any
    other shop or chest relic.
    """
    pools: dict[str, set[str]] = {}
    for f in sorted(POOLS.glob("*.cs")):
        for name in re.findall(
            r"ModelDb\.Relic<(\w+)>\(\)",
            f.read_text(encoding="utf-8"),
        ):
            pools.setdefault(name, set()).add(f.stem)
    return pools


ACTS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Acts"
EVENTS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Events"
MODELDB = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models" / "ModelDb.cs"
ENGINE_EVENTS = REPO / "src" / "Sts2Emulator" / "Core" / "Run" / "RunNonCombatEffects.cs"

#: The two Act 1 variants. A run draws one of them, so either counts as reachable.
ACT_ONE = ("Overgrowth", "Underdocks")


def _listed_events(text: str, marker: str) -> set[str]:
    r"""The `Event<X>()` names inside ONE declaration.

    Bounded by the next member declaration, not by the next `});`. The array literal these
    lists use closes with `\n\t});` only some of the time, and slicing to the first one
    ran `AllSharedEvents` straight on into `AllSharedAncients` -- whose sole entry is
    `AncientEvent<Darv>()`, which matches `Event<(\w+)>`. Darv is an ancient and not an Act 1
    one, and eight boss-flavoured relics came back "reachable" on the strength of it.
    """
    start = text.index(marker)
    rest = text[start + len(marker) :]
    end = re.search(r"\n\t(?:public|private|protected|internal|///)", rest)
    return set(re.findall(r"(?<!Ancient)Event<(\w+)>", rest[: end.start() if end else len(rest)]))


def act_one_events() -> set[str]:
    """Every event an Act 1 run can be shown.

    The act's own `AllEvents` plus `ModelDb.AllSharedEvents`, minus the shared ones whose
    `IsAllowed` refuses `CurrentActIndex 0` -- the emulator already keeps that list, and
    reading it here rather than re-deriving keeps the two answers from drifting.

    Ancients are NOT in this: `AllAncients` is a separate list, and Act 1's is Neow alone.
    """
    events: set[str] = _listed_events(MODELDB.read_text(encoding="utf-8"), "AllSharedEvents =>")
    for act in ACT_ONE:
        events |= _listed_events((ACTS / f"{act}.cs").read_text(encoding="utf-8"), "AllEvents =>")

    engine = ENGINE_EVENTS.read_text(encoding="utf-8")
    gated = re.search(r"ActTwoAndLaterEvents\s*=\s*\[(.*?)\];", engine, re.DOTALL)
    return events - set(re.findall(r"RunConstants\.Event(\w+)", gated.group(1)))


def act_one_relics(relics: dict[str, int]) -> set[str]:
    """Relics an ordinary Act 1 run can end up holding.

    Three routes, and leaving any of them out is how "reachable" stops meaning reachable:

      * the ordinary queue -- anything in `SharedRelicPool`, which is shops, chests and
        combat rewards;
      * an Act 1 EVENT that names the relic in its own source;
      * a relic another reachable relic REPLACES itself with, which is Sword of Stone
        turning into Sword of Jade after three elites.
    """
    pools = relic_pools()
    reachable = {name for name, ps in pools.items() if "SharedRelicPool" in ps}

    allowed = act_one_events()
    event_text = {p.stem: p.read_text(encoding="utf-8") for p in EVENTS.glob("*.cs")}
    for name in relics:
        pattern = re.compile(rf"(?<![A-Za-z0-9_]){name}(?![A-Za-z0-9_])")
        if any(pattern.search(event_text[e]) for e in allowed if e in event_text):
            reachable.add(name)

    # `RelicCmd.Replace(this, ModelDb.Relic<X>())` -- one relic becoming another.
    for name in list(reachable):
        path = RELICS / f"{name}.cs"
        if not path.exists():
            continue
        for successor in re.findall(r"RelicCmd\.Replace\([^)]*?Relic<(\w+)>", path.read_text(encoding="utf-8")):
            reachable.add(successor)

    return reachable & set(relics)


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
    "BingBong": (
        "10fe0eb39dd4",
        "Every card entering the DECK is CLONED to the bottom of it. Written from scratch. The clone must not clone itself -- the game keeps a CardsToSkip set; the emulator uses a re-entrancy flag, which cannot leak an entry. It doubles a curse as happily as anything else.",
    ),
    "DaughterOfTheWind": (
        "597198d06d36",
        "1 unpowered block per ATTACK played. Written from scratch.",
    ),
    "FakeAnchor": (
        "c4ffaa3eb924",
        "4 unpowered block at combat start, against the real Anchor's 10. Written from scratch.",
    ),
    "FakeBloodVial": (
        "5b6b01ae6355",
        "Heal 1 at TURN ONE's start. Written from scratch. A different hook from the real Blood Vial, which heals 2 at COMBAT start, as well as a different number.",
    ),
    "FakeHappyFlower": (
        "fca8a1647e09",
        "+1 energy every FIVE turns; the real flower is three. Written from scratch, riding the same CountTowards clock.",
    ),
    "FakeLeesWaffle": (
        "b3877df3281e",
        "Heal MaxHp * 10 / 100 on pickup -- a PERCENTAGE, where the real waffle grants 7 flat max HP. A different mechanic, not just a smaller number. Written from scratch.",
    ),
    "FakeMango": (
        "98ebaffcfa95",
        "+3 max HP on pickup, against the real Mango's 14. Written from scratch.",
    ),
    "FakeMerchantsRug": (
        "9f4456526abc",
        "An EMPTY RelicModel -- it declares no behaviour at all. Named in RelicEffects.NoEffectRelics so 'nobody wrote this' is distinguishable from 'there is nothing to write'.",
    ),
    "FakeOrichalcum": (
        "a34efb03f076",
        "3 unpowered block for ending a turn with none, against the real Orichalcum's 6. Written from scratch. Its VeryEarly/Early hook split exists so Plating cannot rob it of the trigger; the emulator reads block once, which is the same answer.",
    ),
    "FakeSneckoEye": (
        "a3a8feee695c",
        "Applies ConfusedPower and nothing else -- the real Snecko Eye's downside without its two cards of draw. NOT modelled: Confused re-rolls every card's cost as it is drawn and the emulator has no such power. Declared in RelicEffects.UnmodelledInRun.",
    ),
    "FakeStrikeDummy": (
        "7c00c61396cb",
        "+1 damage on a Strike-TAGGED card, against the real dummy's 3. Written from scratch, and it moved the real dummy off a card-NAME test onto the extracted tag at the same time.",
    ),
    "FakeVenerableTeaSet": (
        "a109af99b59d",
        "+1 energy on the first energy reset after a rest site, against the real set's 2. Written from scratch, onto the counter that now arms both.",
    ),
    "ForgottenSoul": (
        "51a6fe2a387e",
        "1 unpowered damage to ONE random enemy off CombatTargets per card exhausted -- Charon's Ashes' shape at a tenth of the reach. Written from scratch.",
    ),
    "LostWisp": (
        "3e9878601d3e",
        "8 unpowered damage to every hittable enemy whenever a POWER is played. Written from scratch. The event that grants it was implemented three commits before the relic did anything.",
    ),
    "MrStruggles": (
        "e60707301df2",
        "Unpowered damage to every enemy equal to the TURN NUMBER at the start of each player turn -- 1, then 2, then 3, climbing all fight. Written from scratch.",
    ),
    "PollinousCore": (
        "9524c09788cd",
        "Two extra cards on every FOURTH turn's hand draw, through ModifyHandDraw, with AfterModifyingHandDraw resetting the counter. Written from scratch.",
    ),
    "RoyalPoison": (
        "4598d15961da",
        "4 Unblockable, Unpowered damage to its OWN owner on turn one. Written from scratch -- the tea party's gift bites once a fight, and block does not save you.",
    ),
    "StrikeDummy": (
        "f026030df1d7",
        "+3 on a Strike-tagged card. Was reading the card NAME behind a comment saying tags were not extracted; they are, and the name agreed on all 22, so this is the same answer written so it stays right.",
    ),
    "VenerableTeaSet": (
        "043da6cf06de",
        "+2 energy on the first energy reset of the combat after a rest. RE-READ: the effect was right and the ARMING did not exist -- the armed state was a synthetic VenerableTeaSetActive relic id nothing in the run ever added, so the relic was inert for the whole run while two tests drove the marker directly. Now a counter on the relic, set when the run rests.",
    ),
    "WongoCustomerAppreciationBadge": (
        "2914fca90401",
        "An EMPTY RelicModel, like Fake Merchant's Rug. Named in NoEffectRelics.",
    ),
    "WongosMysteryTicket": (
        "4602c8723aba",
        "After FIVE combats the next reward screen carries THREE relics, then GaveRelic retires it. Written from scratch -- a one-off, not a tap.",
    ),
    "BoneTea": (
        "dcec93b0c05b",
        "Turn one, UPGRADE EVERY CARD IN HAND, for ONE combat. Written from scratch. The remaining-combats count is run state, which is what forced the Girya ordering fix.",
    ),
    "DarkstonePeriapt": (
        "9ad6ca9c8fa6",
        "+6 max HP for every CURSE entering the deck, by any route -- the hook is AfterCardChangedPiles on the deck pile, not an event's gift. Written from scratch.",
    ),
    "DreamCatcher": (
        "9099a590b9e9",
        "Resting also offers a card reward, from the MONSTER room's creation options. Written from scratch.",
    ),
    "EmberTea": (
        "ba150ecffb95",
        "Strength 2 at the top of a fight, for FIVE fights. Written from scratch.",
    ),
    "HandDrill": (
        "ee77171035f9",
        "Vulnerable 2 on an enemy whose block the hit BROKE -- block that was there, and a hit that got through it. Written from scratch; needed the block-before value, which the damage path was discarding.",
    ),
    "HistoryCourse": (
        "7827cef548bb",
        "From turn two, auto-play a DUPE of the last Attack or Skill played LAST turn. Written from scratch. The !IsDupe clause is the one that matters: without it the relic latches onto one card forever off a single play.",
    ),
    "LastingCandy": (
        "638d3311d43c",
        "Every SECOND combat's reward screen gains an extra Power option. The CLOCK is modelled; the extra option is not -- the screen is three slots and RewardSkipAction is 3, so a fourth card is an action-space change. Declared in RelicEffects.UnmodelledInRun with the reason.",
    ),
    "MawBank": (
        "f16f7f6245a5",
        "12 gold on entering a room, EVERY room, until any merchant purchase with goldSpent > 0 closes it for the run. Written from scratch.",
    ),
    "RazorTooth": (
        "39ec087d73b0",
        "The Attack or Skill just played is upgraded, on the copy -- so it lands in the discard pile upgraded and comes back that way. Written from scratch.",
    ),
    "SparklingRouge": (
        "45e9bf4e3b9d",
        "Strength 1 and Dexterity 1 when block clears on TURN THREE exactly -- not from turn three on. Written from scratch; a Barricade run never gets it, because block never clears.",
    ),
    "SwordOfJade": (
        "2f648392eecc",
        "Strength 3 at the top of every fight. What Sword of Stone becomes. Written from scratch.",
    ),
    "SwordOfStone": (
        "3eb34a92814a",
        "FIVE elite VICTORIES, not three -- DynamicVar('Elites', 5m) -- and RelicCmd.Replace swaps it for Sword of Jade. Written from scratch. A replacement, so the relic list keeps its length, and it counts wins rather than rooms entered.",
    ),
    "TeaOfDiscourtesy": (
        "fd60754de0d3",
        "Two Dazed into the draw pile at random positions, for ONE combat. Written from scratch -- the price the Tea Master's free tea charges.",
    ),
    "TheBoot": (
        "02385c555d6b",
        "A powered attack by the player or their pet that would take 1..4 off an enemy takes 5. A FLOOR on HP LOST, so it neither punches through block nor lowers a bigger hit. Written from scratch. Its DamageThreshold var of 4 is display text -- the comparison reads DamageMinimum.",
    ),
    "BigHat": (
        "e9dc5b461163",
        "Two DISTINCT Ethereal cards from the player's OWN pool into hand on turn one. Written from scratch. The Ironclad pool has no Ethereal card at all, and neither does the Silent's, so `readOnlyList.Count > 0` is false and a Rare relic does nothing for two of the five characters -- the emulator runs one of them.",
    ),
    "BoneFlute": (
        "bcab4362f28b",
        "2 Unpowered block whenever the owner's Osty attacks. Written from scratch. The guard is on the ATTACKER being an Osty whose PetOwner is this player, so it is per swing rather than per card that orders one.",
    ),
    "BookRepairKnife": (
        "40d864612ad1",
        "Heal 3 per creature that died to Doom, counting only those whose `Powers.All(ShouldOwnerDeathTriggerFatal)` -- a Minion and an attached Decimillipede segment do not count. Written from scratch.",
    ),
    "Bookmark": (
        "ff1177d0a658",
        "After the flush, one RETAINED card with a non-X cost above zero gets -1 until played, chosen off CombatCardSelection. Written from scratch; it needed the AfterFlush hook, which is a different boundary from AfterCardDiscarded.",
    ),
    "Brimstone": (
        "478a4923b28f",
        "2 Strength to the player and 1 to every LIVING opponent, every turn, with no turn guard. Written from scratch.",
    ),
    "CharonsAshes": (
        "01de682014e7",
        "3 Unpowered damage to every hittable enemy per card exhausted, with no Ethereal exception. Written from scratch.",
    ),
    "DemonTongue": (
        "6eee5c9962f0",
        "The first unblocked hit taken on the player's OWN side turn is healed straight back, once per turn. Written from scratch. Self-inflicted damage only -- an enemy attack lands on the enemy's side and does not qualify.",
    ),
    "EmotionChip": (
        "a1d415cbb391",
        "If the player took unblocked damage since the last player turn start, every orb fires its passive at the next one. Written from scratch. `HappenedLastPlayerTurn` stamps the entry with the player's TurnNumber, which does not move during the enemy phase, so an enemy attack counts.",
    ),
    "FencingManual": (
        "2e86ce30befb",
        "Forge 10 on turn one -- a Sovereign Blade from a Common relic. Written from scratch.",
    ),
    "FresnelLens": (
        "160ce3d2e463",
        "Every card entering the deck that Nimble can take arrives enchanted at 2. Written from scratch. Three hooks, one rule at three doors; `TryModifyCardBeingAddedToDeck` is the one that actually lands, and modelling only the reward screen would have missed an event's gift.",
    ),
    "FuneraryMask": (
        "cb74dc8efe58",
        "Three Souls into the DRAW pile at random positions before the opening draw. Written from scratch. Its guard is `TurnNumber == 1` where Ninja Scroll's is `<= 1`; both mean turn one.",
    ),
    "GalacticDust": (
        "67ce2a27ae82",
        "10 Unpowered block per full ten stars spent, counted across the RUN (`[SavedProperty]`). Written from scratch. `floor(StarsSpent / 10) * 10` then modulo, so one spend of twenty-five pays twenty and carries five.",
    ),
    "GoldPlatedCables": (
        "8f83d47ceccc",
        "The orb at the FRONT of the queue triggers its passive one extra time. Written from scratch, as a repeat of the whole passive rather than a doubled value -- a trigger COUNT, so Lightning re-rolls its target.",
    ),
    "HelicalDart": (
        "2e88fc614088",
        "1 Dexterity when a card TAGGED Shiv is played. Written from scratch; needed CardTag.Shiv extracted, because Knife Trap carries it too.",
    ),
    "LoomingFruit": (
        "c96e981748cc",
        "+31 max HP on pickup. Written from scratch. Its cornucopia is decided by the last byte of the PROFILE's unique id and changes only `IconBaseName` -- a joke about multiplayer, not a mechanic.",
    ),
    "LunarPastry": (
        "780b596d45fb",
        "1 star at the end of the player's side turn, through GainStars so Black Hole sees it. Written from scratch.",
    ),
    "Metronome": (
        "dd14461516c6",
        "The SEVENTH orb channelled in a combat deals 30 Unpowered to all. Written from scratch. `== OrbCount`, not `>=`, so the eighth does nothing and only entering a combat room resets it.",
    ),
    "MiniRegent": (
        "fe50c2ff37b7",
        "1 Strength on the first star spend each turn. Written from scratch.",
    ),
    "NinjaScroll": (
        "2610db0185c9",
        "Three Shivs into HAND before the opening draw. Written from scratch.",
    ),
    "OrangeDough": (
        "cf1d0479f436",
        "Two DISTINCT colourless cards into hand on turn one, off CombatCardGeneration. Written from scratch.",
    ),
    "PaperKrane": (
        "9150ea947038",
        "Weak multiplier -0.15 when the relic's owner is the TARGET, so a Weak enemy hits them at 0.60. Written from scratch. It reads the target, not the attacker -- it does nothing to the Weak the player applies.",
    ),
    "PaperPhrog": (
        "6617309e74cf",
        "Vulnerable multiplier +0.25 when the target is NOT its owner, so a Vulnerable enemy takes 1.75. Written from scratch. Note the asymmetry against Paper Krane: one helps only when you are hit, the other only when you are not.",
    ),
    "PowerCell": (
        "ee637e93e158",
        "Two ZERO-COST cards MOVED out of the draw pile into hand on turn one, off CombatCardSelection. Written from scratch. A move, not a generation -- and an X-cost card is never free however low its cost reads.",
    ),
    "Regalite": (
        "0619d8df9ecb",
        "2 Unpowered block per card the player generates for combat. Written from scratch; it rides the same hook as PillarOfCreationPower, and unlike the power its block is Unpowered.",
    ),
    "RuinedHelmet": (
        "5b88e6ac0d64",
        "The first positive Strength the player receives each combat is doubled. Written from scratch; it needed a chokepoint for player Strength, which had twenty-eight bare call sites. A LOSS passes through untouched and does not spend it.",
    ),
    "RunicCapacitor": (
        "23d95088ec8e",
        "Three orb slots on turn one. Written from scratch.",
    ),
    "SneckoSkull": (
        "ad002429e4f3",
        "One more Poison on every Poison the owner applies. Written from scratch. Additive on the amount GIVEN, so once per application rather than once per stack.",
    ),
    "SymbioticVirus": (
        "59319caa0354",
        "One Dark orb channelled on turn one. Written from scratch.",
    ),
    "Tingsha": (
        "4627ac82f68f",
        "3 Unpowered damage to one random enemy per card an effect discards, re-rolled per card off CombatTargets. Written from scratch.",
    ),
    "ToughBandages": (
        "dfae95782275",
        "3 Unpowered block per card an effect discards. Written from scratch. NOT the end-of-turn hand dump: `FlushPlayerHand` is a plain pile add followed by AfterFlush, with no CardDiscarded between them.",
    ),
    "TwistedFunnel": (
        "e3839309fb43",
        "Poison 4 on every hittable enemy on turn one. Written from scratch. Poison is a debuff, so an Artifact enemy swallows it whole.",
    ),
    "UndyingSigil": (
        "972b6070ebe9",
        "A powered attack on the owner by an attacker whose HP is at or below its own Doom lands at half. Written from scratch. Its own doc comment says the relic 'doesn't actually do anything' -- that is about its OTHER half, moving enemy Doom to the start of the enemy turn, and the halving right below the comment is real.",
    ),
    "VeryHotCocoa": (
        "a0064c9d7492",
        "4 energy on turn one. Written from scratch.",
    ),
    "VitruvianMinion": (
        "2f9efd314c17",
        "2x damage AND 2x block from a Minion-tagged card. Written from scratch; needed CardTag.Minion extracted. Minion Sacrifice is the only Minion card that gains block.",
    ),
    "BigMushroom": (
        "d38b4dfad613",
        "+20 max HP on pickup (GainMaxHp heals with it), and `ModifyHandDraw` SUBTRACTS 2 on turn one -- the opening hand is three, which is the price. Only the pickup half was modelled; the drawback was missing, so the relic was all upside.",
    ),
    "ChosenCheese": ("e5f888237a51", "+1 max HP after every combat; correct."),
    "FragrantMushroom": (
        "052f878e0ebd",
        "15 unblockable damage on pickup and two upgradable deck cards upgraded off Rng.Niche, no type filter; correct.",
    ),
    "NeowsBones": (
        "114c262003a7",
        "Two relics offered with skipping disallowed, then a curse once they are claimed; correct.",
    ),
    "NeowsTalisman": (
        "850ad1ef7bcc",
        "Upgrades the LAST BASIC card tagged Strike and the last tagged Defend. It matched Ironclad's two ids, so for any other character it upgraded nothing -- Leafy Poultice's bug in the other direction.",
    ),
    "NutritiousOyster": ("4199481b7619", "+11 max HP on pickup; correct."),
    "NutritiousSoup": (
        "22479861b577",
        "Tezcatara's Ember onto every BASIC Strike-tagged card; correct, and the tag test is the real tag now rather than a 'STRIKE' substring.",
    ),
    "PhialHolster": (
        "e7a9890064ec",
        "+1 potion slot then two potions, slot first so they fit; correct.",
    ),
    "PhilosophersStone": (
        "b0b9d72d22f7",
        "+1 max energy and Strength 1 to every enemy, including ones that join mid-combat; both correct.",
    ),
    "Pomander": ("bc8802baef4c", "One CHOSEN deck card upgraded; correct."),
    "PrecariousShears": (
        "b4295aed20df",
        "Two CHOSEN cards removed and then 16 unpowered damage, the damage owed only once they are gone; correct.",
    ),
    "PreciseScissors": ("414755898b04", "One CHOSEN card removed; correct."),
    "SandCastle": ("39adf06da9b3", "Six cards StableShuffled off Rng.Niche; correct."),
    "ScrollBoxes": (
        "02c9c9dd37d4",
        "Two bundles offered on a choose-a-bundle screen; correct.",
    ),
    "SeaGlass": (
        "ecd35bae95fa",
        "Fifteen cards from a branded character's pool, five each of Common, Uncommon and Rare; correct.",
    ),
    "SilverCrucible": (
        "49be81553ba6",
        "Three card-reward upgrades, one per reward, counted down on the relic; correct.",
    ),
    "SmallCapsule": (
        "915148e2c2d4",
        "A single RelicReward on a SCREEN the player claims from, not a relic granted outright; correct.",
    ),
    "Sozu": (
        "973954fd60b4",
        "+1 max energy and no potions ever procured; both correct.",
    ),
    "SpikedGauntlets": (
        "93ec361dff23",
        "+1 max energy and POWERS cost one more; both correct.",
    ),
    "StoneHumidifier": ("7cdf8486baea", "+5 max HP after a rest-site heal; correct."),
    "Storybook": ("283a84fe70c1", "A Brightest Flame into the deck; correct."),
    "VelvetChoker": (
        "ccc297357f38",
        "+1 max energy and no card plays past the sixth in a turn; both correct.",
    ),
    "WingedBoots": (
        "8b13976f606e",
        "Three free map travels, counted down on the relic; correct.",
    ),
    "YummyCookie": ("87b6e72a4475", "Four CHOSEN cards upgraded; correct."),
    "PaelsClaw": (
        "cc7295a9cb5c",
        "GOOPY onto every deck card that can take it -- no screen, no choice; correct. Its `CardsVar(3)` is display text, not a count.",
    ),
    "PaelsGrowth": (
        "acdb18543b5b",
        "One CHOSEN card enchanted with CLONE, and a rest-site option that copies every Clone-enchanted card; both halves correct.",
    ),
    "PaelsHorn": ("52d0182ed3ac", "Two Relax into the deck; correct."),
    "PaelsLegion": (
        "76d1df9a9d21",
        "A PET that doubles a card's block and then sits out two of its owner's turns. It had the creature in EnemyAI and the relic in Pael's options and no behaviour at all -- the pet existed and did nothing.",
    ),
    "PaelsTooth": (
        "16c488437c26",
        "Five CHOSEN cards removed, offered only from the upgradable ones; correct.",
    ),
    "Ectoplasm": (
        "ff4aca149d60",
        "+1 max energy and all gold gains zeroed; both halves correct.",
    ),
    "ElectricShrymp": (
        "e6e0f97ae5c0",
        "One CHOSEN deck card enchanted with IMBUED; correct -- the source's local is named `canonicalMomentum` and its TYPE is Imbued, so the name is a MegaCrit copy-paste and reading it would have got the wrong enchantment.",
    ),
    "FishingRod": (
        "cedf09ce1f1d",
        "Every third MONSTER-room combat, one random upgradable deck card upgraded off Rng.Niche; correct.",
    ),
    "GlassEye": (
        "66024c334ef1",
        "Five card rewards on one screen -- Common, Common, Uncommon, Uncommon, Rare -- each offering three at Uniform odds; correct.",
    ),
    "GoldenPearl": ("56dc7bd27793", "150 gold on pickup; correct."),
    "HeftyTablet": (
        "796600817581",
        "Three RARE cards from the owner's pool on a skippable choose-a-card screen, and its Injury lands with whichever is taken; correct.",
    ),
    "Kaleidoscope": (
        "481ff0ec6990",
        "Two card rewards from the OTHER characters' pools; correct.",
    ),
    "LargeCapsule": (
        "c42c6a7e9f61",
        "Two relics off the pool front, plus a Basic Strike and Defend for the character; correct for the Ironclad, which is the only character a run models.",
    ),
    "LavaRock": (
        "e59da0a26598",
        "TWO relic rewards added to the ACT-1 BOSS room, once per run. It had no effect at all -- an id constant and nothing behind it.",
    ),
    "LeafyPoultice": (
        "9ced28bfa596",
        "Lose 12 max HP, then TRANSFORM the first BASIC Strike-tagged and first BASIC Defend-tagged card. It matched Ironclad's two card ids rather than the tags, so it found nothing for any other character.",
    ),
    "LostCoffer": ("9b3c15ab1cd7", "A custom reward screen on pickup; correct."),
    "NewLeaf": (
        "4df3dac6ba68",
        "One CHOSEN deck card transformed at random off Rng.Niche; correct.",
    ),
    "AlchemicalCoffer": (
        "2f97e5a40e58",
        "+4 potion slots then four potions off CombatPotionGeneration, slots first so they all fit; correct.",
    ),
    "ArcaneScroll": (
        "ff74c3fb7138",
        "One RARE card from the character's own pool at Uniform odds with NoUpgradeRoll, into the deck; correct.",
    ),
    "BiiigHug": (
        "31993acb6a98",
        "Remove four CHOSEN cards on pickup -- and then a SOOT into the draw pile on every shuffle for the rest of the run. Only the pickup was modelled, which made a hug that costs nothing.",
    ),
    "BlackBlood": (
        "2511fa8bcc83",
        "Heal 12 after a combat victory; correct. Burning Blood's Ancient twin.",
    ),
    "BlessedAntler": (
        "42864be085a2",
        "+1 max energy, and three Dazed into the draw pile before turn one's hand; both halves correct.",
    ),
    "BoomingConch": (
        "3013ed9dced0",
        "In an ELITE only: two cards on turn one through `ModifyHandDraw`, and +1 energy at turn start. The cards were a separate `DrawCards` at combat start -- the third relic with that mechanic and the second modelled the wrong way.",
    ),
    "CursedPearl": ("5e6436a77046", "A Greed curse and 333 gold on pickup; correct."),
    "LeadPaperweight": (
        "26d28dd678a7",
        "Two COLOURLESS cards at RegularEncounter odds on a skippable choose-a-card screen; correct.",
    ),
    "NeowsTorment": ("3f921eca9c5f", "A Neow's Fury into the deck on pickup; correct."),
    "SilkenTress": (
        "51ca42828b23",
        "Pickup takes ALL the player's gold; correct. Its other half -- enchanting card REWARDS with Glam until used -- needs an enchantment the emulator does not model, and is not there.",
    ),
    "Akabeko": ("b0c13a3b38d2", "Vigor 8 on turn one; correct."),
    "Anchor": ("2a4f263578aa", "10 unpowered block at combat start; correct."),
    "ArtOfWar": (
        "29b1793f8651",
        "+1 energy on a turn following one with no Attack played; correct.",
    ),
    "BagOfMarbles": (
        "7c6d92cc4a6d",
        "Vulnerable 1 to every enemy on turn one; correct.",
    ),
    "BagOfPreparation": (
        "c74af48a9760",
        "`ModifyHandDraw` +2 on turn one -- the OPENING HAND is seven. It drew its two separately at combat start, so they were not part of the hand draw and the opening-hand size that feeds the Innate reorder was wrong. Ring of the Snake is the same mechanic, modelled the other way.",
    ),
    "BloodVial": ("e7b8669ac523", "Heal 2 at the start of turn one; correct."),
    "BronzeScales": ("ac3489f7b30c", "Thorns 3 on entering a combat room; correct."),
    "BurningBlood": ("23a82e46abf8", "Heal 6 after a combat victory; correct."),
    "CaptainsWheel": ("2885bba195bb", "18 unpowered block on turn three; correct."),
    "CentennialPuzzle": (
        "2c744f4db876",
        "Draw 3 the first time unblocked damage lands in a combat; correct.",
    ),
    "Circlet": (
        "01a6de05d3e1",
        "No effect at all -- the stackable fallback relic. Correct.",
    ),
    "CloakClasp": (
        "f1a3dc72c23f",
        "1 unpowered block per card left in hand at end of turn; correct.",
    ),
    "DataDisk": ("b38e2280c6f1", "Focus 1 at combat start; correct."),
    "FestivePopper": (
        "92c5a2092917",
        "9 unpowered damage to every enemy on turn one; correct.",
    ),
    "FrozenEgg": (
        "1e74f50d3ef8",
        "Powers added to the deck arrive upgraded, and it stops being offered past floor 41; correct.",
    ),
    "Gorget": ("05467b3a5070", "Plating 4 at combat start; correct."),
    "GremlinHorn": (
        "f5ec2d8dca70",
        "+1 energy and draw 1 per enemy death, even one something undoes; correct.",
    ),
    "HappyFlower": ("05e599069104", "+1 energy every third turn; correct."),
    "HornCleat": ("f39c54170c6a", "14 unpowered block on turn two; correct."),
    "IvoryTile": (
        "4db0cf8e9749",
        "+1 energy after a card that spent three or more; correct.",
    ),
    "Kunai": (
        "9f9295a9f568",
        "Dexterity 1 every third ATTACK in a turn, counter resetting each turn; correct.",
    ),
    "Kusarigama": (
        "0e323b20dea5",
        "6 unpowered damage to a random enemy every third Attack in a turn; correct.",
    ),
    "Lantern": ("83cdb21d95b2", "+1 energy on turn one; correct."),
    "LeesWaffle": ("76cf5c61a3d7", "+7 max HP and a full heal on pickup; correct."),
    "LetterOpener": (
        "c1caf678d705",
        "5 unpowered damage to EVERY enemy every third Skill in a turn; correct.",
    ),
    "LizardTail": (
        "7ba788ed0e09",
        "Refuses one death per run and revives at half max HP; correct.",
    ),
    "Mango": ("d0df8bc4529b", "+14 max HP on pickup; correct."),
    "MealTicket": (
        "03122f3f58c9",
        "Heal 15 on entering a shop, skipped when dead; correct.",
    ),
    "MeatOnTheBone": (
        "aa0f27f19830",
        "Heal 12 after a combat victory at half HP or below; correct.",
    ),
    "MembershipCard": ("de279e608c8e", "Merchant prices halved; correct."),
    "MoltenEgg": (
        "55618e2c31bb",
        "Attacks added to the deck arrive upgraded; correct.",
    ),
    "MummifiedHand": (
        "848a35f917fc",
        "After a POWER, one card in hand goes free -- and both of its filters count STARS as well as energy. The emulator read energy only, so a Regent hand of 0-energy star cards read as free and fell to the last-resort branch.",
    ),
    "Nunchaku": (
        "d1255b5ef641",
        "+1 energy every tenth Attack of the COMBAT, not the turn; correct.",
    ),
    "OddlySmoothStone": ("2bee6c42865b", "Dexterity at combat start; correct."),
    "OldCoin": ("5253274e1dc1", "300 gold on pickup; correct."),
    "Orichalcum": (
        "43c5ac425423",
        "6 unpowered block at end of turn if block was zero -- latched before the other end-of-turn block relics run. Correct.",
    ),
    "OrnamentalFan": (
        "c37a7442a7fb",
        "4 unpowered block every third Attack in a turn; correct.",
    ),
    "ParryingShield": (
        "cc06d2fdcb59",
        "6 unpowered damage to a random enemy at end of turn while holding 10+ block, counted AFTER the other end-of-turn block; correct.",
    ),
    "Pear": ("2bb38f453822", "+10 max HP on pickup; correct."),
    "Pendulum": ("bf98b5a115fe", "Draw 1 every third turn; correct."),
    "Permafrost": (
        "1ba262dd31e3",
        "7 unpowered block on the FIRST Power of a combat; correct.",
    ),
    "Pocketwatch": (
        "48de70e5067f",
        "+3 cards next turn after a turn of more than three card plays; correct.",
    ),
    "RedMask": ("4de7f367fd6f", "Weak 1 to every enemy on turn one; correct."),
    "RegalPillow": (
        "f7fa9f8f872e",
        "+15 on top of whatever a rest site was going to heal; correct.",
    ),
    "ScreamingFlagon": (
        "85a74019aeb3",
        "20 unpowered damage to every enemy at end of turn with an empty hand; correct.",
    ),
    "SelfFormingClay": (
        "3d17d6977f24",
        "3 block next turn per unblocked hit taken; correct.",
    ),
    "Shuriken": ("f733f5640805", "Strength 1 every third Attack in a turn; correct."),
    "StoneCalendar": (
        "8bf0617dcf12",
        "52 unpowered damage to every enemy at the end of turn seven; correct.",
    ),
    "StoneCracker": (
        "71efca6568d1",
        "Two upgradable cards off the draw pile upgraded before the opening hand; correct.",
    ),
    "Strawberry": ("2ab75adc89df", "+7 max HP on pickup; correct."),
    "TinyMailbox": ("d586a0fa375b", "A card reward at the rest site; correct."),
    "ToxicEgg": ("008cc2dc69f1", "Skills added to the deck arrive upgraded; correct."),
    "TuningFork": (
        "fb5be5f36b07",
        "7 unpowered block every tenth Skill, the tally wrapping rather than resetting per turn; correct.",
    ),
    "Vajra": ("0ffd7a67279d", "Strength at combat start; correct."),
    "WarPaint": ("6559622ec777", "Two cards off the deck upgraded on pickup; correct."),
    "Whetstone": (
        "8685fefd4317",
        "Two cards off the deck upgraded on pickup; correct.",
    ),
    "WhiteBeastStatue": (
        "6913a36cd04d",
        "Forces a potion reward after a combat, and stops being offered past floor 41; correct.",
    ),
    "BoundPhylactery": (
        "1974cb754224",
        "Osty at 1 HP on BeforeCombatStart, then AfterEnergyResetLate every turn but 1",
    ),
    "Cauldron": (
        "347be997cb85",
        "RewardsCmd.OfferCustom over FIVE potion rewards -- five screens, not five potions",
    ),
    "CrackedCore": (
        "ed1f64e1fb2a",
        "one Lightning, TurnNumber <= 1 -- a combat-start channel, not a per-turn one",
    ),
    "DingyRug": (
        "55c82644d092",
        "colourless pool CONCATENATED onto the character pool, not swapped for it",
    ),
    "DivineRight": (
        "45a08da82257",
        "3 Stars per CombatRoom entered; stars are per-combat, so 3 every fight",
    ),
    "DollysMirror": (
        "a4e8e0269cc0",
        "FromDeckGeneric filtered to c.Type != Quest, then RunState.CloneCard into the deck",
    ),
    "DragonFruit": (
        "cc169ada2578",
        "AfterGoldGained -> GainMaxHp(1), which HEALS 1 too; per gain event, not per gold",
    ),
    "GnarledHammer": (
        "4cc92a15551a",
        "CardSelectorPrefs(prompt, 0, 3) then Sharp at 3 -- its own amount, not Self-Help Book's 2",
    ),
    "Kifuda": (
        "743acbcfa22c",
        "up to 3 cards enchanted Adroit 3; Adroit blocks its amount on play, any card type",
    ),
    "LavaLamp": (
        "ff2048c78530",
        "upgrade flag read ONCE for the whole screen; gated on unblocked damage taken",
    ),
    "Orrery": (
        "8e30d4ed2a03",
        "five whole CardReward screens via OfferCustom, not five cards on one screen",
    ),
    "PunchDagger": (
        "820601153a7c",
        "one ATTACK enchanted Momentum 5; Momentum accumulates on play and pays what it has banked",
    ),
    "RedSkull": (
        "ae0375b0d602",
        "re-asks on every CURRENT-hp change and REMOVES the 3 when healed back over half",
    ),
    "RingOfTheSnake": (
        "6bf562b9a37f",
        "ModifyHandDraw +2 while TurnNumber is 1 -- the OPENING hand, not turn-start",
    ),
    "RoyalStamp": (
        "b3d522ada300",
        "one Attack or Skill enchanted RoyallyApproved, whose OnEnchant adds Innate AND Retain",
    ),
    "BeltBuckle": (
        "5e49a1df2ee2",
        "Dexterity 2 while the belt is EMPTY, applied and REMOVED as potions come and go",
    ),
    "Bread": (
        "8fb48b3c1227",
        "ModifyMaxEnergy +1 from turn two, and LoseEnergy(2) at turn one's side-turn start",
    ),
    "BurningSticks": (
        "fa843fb29415",
        "the first SKILL exhausted each combat is cloned back into hand",
    ),
    "ChemicalX": ("fd59024bb334", "ModifyXValue +2 on every X-cost card"),
    "GhostSeed": (
        "47df9c72ce0a",
        "every BASIC card tagged Strike or Defend gains Ethereal",
    ),
    "MiniatureTent": (
        "146a42576172",
        "ShouldDisableRemainingRestSiteOptions returns false, so one option does not end the visit",
    ),
    "MysticLighter": (
        "12bd29d1afac",
        "9 more damage from a powered attack whose card carries ANY enchantment",
    ),
    "RingingTriangle": (
        "b900ec558dcd",
        "ShouldFlush is false on turn ONE, so the opening hand is kept whole",
    ),
    "SlingOfCourage": (
        "198e914c8f58",
        "AfterRoomEntered(RoomType.Elite) applies StrengthPower(2)",
    ),
    "TheAbacus": (
        "4f2984365e33",
        "AfterShuffle gains BlockVar(6, Unpowered), every shuffle",
    ),
    "GamblingChip": (
        "d45aa622fe97",
        "AfterPlayerTurnStart on turn 1: a min-0 max-unbounded discard screen, then DiscardAndDraw of the whole list",
    ),
    "Toolbox": (
        "b4af222a7789",
        "3 DISTINCT colourless cards, choose 1 to hand, at combat start",
    ),
    "UnsettlingLamp": (
        "273f5adb0eba",
        "the first card each combat to land a debuff on an enemy has ALL its debuffs doubled; the latch is per CARD",
    ),
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
    "Vambrace": (
        "661865f6a216",
        "the FIRST card block of a combat is doubled; it latches only once an amount above zero lands",
    ),
    "WingCharm": (
        "4cb68d06f63c",
        "Swift on one option; Powers-only, so a screen with no Power gets nothing",
    ),
}


def unread_names(reachable: bool = False) -> list[str]:
    """List the modelled-but-unread relics, for `relic_pair.py --list`.

    Exported rather than re-derived, for the reason the digest function is imported rather
    than copied: two answers to "what is left" that can disagree is how a worklist quietly
    stops matching the audit it is supposed to burn down.
    """
    relics = generated_relics()
    sources = engine_sources()
    consts = id_constants(sources, relics)
    used = used_constants(sources, consts, relics)
    obtainable = act_one_relics(relics)

    names = []
    for name in sorted(relics):
        if reachable and name not in obtainable:
            continue
        path = RELICS / f"{name}.cs"
        if not path.exists() or name not in used:
            continue
        if name not in READ:
            names.append(name)
    return names


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--relic", default=None, help="audit just this one")
    parser.add_argument(
        "--unmodelled",
        action="store_true",
        help="list the unmodelled relics in full",
    )
    parser.add_argument(
        "--unread",
        action="store_true",
        help="list the modelled-but-unread relics in full",
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
    obtainable = act_one_relics(relics)
    unread: list[str] = []
    declared: list[str] = []
    unmodelled: list[str] = []
    stale: list[tuple[str, str, str]] = []
    missing_source: list[str] = []

    for name in sorted(relics):
        if args.relic and name != args.relic:
            continue
        if args.reachable and name not in obtainable:
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
        shown = len(unread) if args.unread else min(len(unread), 24)
        for i in range(0, shown, 5):
            print("  " + ", ".join(unread[i : i + 5]))
        if shown < len(unread):
            print(f"  ... and {len(unread) - shown} more (--unread lists them)")

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
