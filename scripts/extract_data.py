#!/usr/bin/env python3
"""Parse decompiled sts2.dll C# source and emit Generated/*.g.cs files."""

import json
import operator
import re
import sys
from pathlib import Path

REPO = Path(__file__).parent.parent
DECOMPILED = REPO / "decompiled"
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated"

CARDS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.Cards"
MONSTERS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.Monsters"
POWERS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.Powers"
POTIONS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.Potions"
RELICS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.Relics"

# ── patterns ──────────────────────────────────────────────────────────────────

# Match card constructor arguments: cost, type, and rarity.
CARD_CTOR = re.compile(
    r"base\((-?\d+),\s*CardType\.(\w+),\s*CardRarity\.(\w+)"
    # The fourth argument, when a card gives one. Only AnyEnemy means "target selection
    # is performed" -- which is what decides whether a play carries a creature Target,
    # and so whether the Kaiser Crab's Surrounded turns the player (E101).
    r"(?:,\s*TargetType\.(\w+))?",
)
# DamageVar(6m, ...) or DamageVar(6, ...) -- and OstyDamageVar, which is the same thing
# for a Necrobinder card whose PET swings. Ten cards declare their damage that way, and
# matching only DamageVar gave every one of them BaseDamage 0: High Five dealt nothing at
# all, because its case leans on the printed number.
DAMAGE_VAR = re.compile(r"new (?:Osty)?DamageVar\((\d+(?:\.\d+)?)m?,")
# BlockVar(5m, ...)
BLOCK_VAR = re.compile(r"new BlockVar\((\d+(?:\.\d+)?)m?,")
# UpgradeValueBy on damage / block
# 56 cards get cheaper when upgraded via base.EnergyCost.UpgradeBy(-1). That was a
# hand-written list of five in CombatEngine, which is exactly the kind of thing that
# goes stale silently — read it off the card instead.
UPGRADE_COST = re.compile(r"EnergyCost\.UpgradeBy\((-?\d+)\)")

# X-cost cards are printed at cost 0 and declare themselves this way instead. Without
# the flag nothing downstream can tell "free" from "spends the whole bar".
HAS_ENERGY_COST_X = re.compile(r"HasEnergyCostX\s*=>\s*true")
# `CardModel.CanonicalStarCost => -1` unless a card overrides it. Twenty-one Regent cards
# do, and it is a SECOND resource the play has to have and spend -- not a variant of the
# energy cost. Nothing upgrades a star cost (`UpgradeStarCostBy` has no callers), so the
# printed number is the whole story.
STAR_COST = re.compile(r"CanonicalStarCost\s*=>\s*(\d+)")
HAS_STAR_COST_X = re.compile(r"HasStarCostX\s*=>\s*true")

# CardFactory.FilterForPlayerCount drops MultiplayerOnly cards from every pool in a solo
# run. Without the flag the reward pools are larger than the game's and offer cards that
# cannot appear.
MULTIPLAYER_ONLY = re.compile(
    r"MultiplayerConstraint\s*=>\s*CardMultiplayerConstraint\.MultiplayerOnly",
)
UPGRADE_DMG = re.compile(
    r"DynamicVars\.(?:Osty)?Damage\.UpgradeValueBy\((\d+(?:\.\d+)?)m?\)"
)
UPGRADE_BLOCK = re.compile(r"DynamicVars\.Block\.UpgradeValueBy\((\d+(?:\.\d+)?)m?\)")

# HP: plain int, or an AscensionHelper pair. Both branches are kept: the second is what
# the game rolls below AscensionLevel.ToughEnemies, and taking only the first made enemy
# HP ascension-blind while every damage number was ascension-aware.
HP_PLAIN = re.compile(r"(?:Min|Max)InitialHp\s*=>\s*(\d+)\s*;")
HP_ASCENSION = re.compile(
    r"(?:Min|Max)InitialHp\s*=>.+?GetValueIfAscension\([^,]+,\s*(\d+),\s*(\d+)\s*\)",
)
# `MinInitialHp => FirstFormHp;` -- a monster whose HP is named rather than stated.
# Only the Test Subject does this (its three forms each have their own), and it read as
# ZERO HP until the indirection was followed, which is worse than not extracting it.
HP_INDIRECT = re.compile(r"(?:Min|Max)InitialHp\s*=>\s*([A-Za-z_]\w*)\s*;")

# Monster move intents
SINGLE_ATTACK = re.compile(r"new SingleAttackIntent\((\d+)\)")
MULTI_ATTACK = re.compile(
    r"new MultiAttackIntent\((\d+),\s*(\d+)\)",
)  # (damage, repeats)

# Power type
POWER_TYPE = re.compile(r"PowerType\.(Buff|Debuff)")
POWER_STACK = re.compile(r"PowerStackType\.(\w+)")

# Innate attribution — the CanonicalKeywords property body vs the OnUpgrade body.
_CANONICAL_KEYWORDS_BODY = re.compile(
    r"CanonicalKeywords\s*=>(.*?)(?=\n\tprotected|\n\tpublic|\n\tprivate|\Z)",
    re.DOTALL,
)
_ON_UPGRADE_BODY = re.compile(r"OnUpgrade\(\)\s*\{(.*?)\n\t\}", re.DOTALL)


def has_canonical_keyword(text: str, keyword: str) -> bool:
    """Report whether the card declares this keyword in its own CanonicalKeywords.

    Must not be a substring search over the whole file. Keywords are referenced in
    plenty of places that do not make the card have them — TrueGrit mentions
    CardKeyword.Exhaust only in ExtraHoverTips, a tooltip explaining that it
    exhausts *another* card, and a naive check marked TrueGrit itself Exhaust.
    """
    m = _CANONICAL_KEYWORDS_BODY.search(text)
    return bool(m and f"CardKeyword.{keyword}" in m.group(1))


def innate_canonical(text: str) -> bool:
    return has_canonical_keyword(text, "Innate")


def innate_on_upgrade(text: str) -> bool:
    return keyword_on_upgrade(text, "Innate", "Add")


def keyword_on_upgrade(text: str, keyword: str, verb: str) -> bool:
    """Whether OnUpgrade ADDS or REMOVES this keyword.

    The direction is the whole point and the two are not interchangeable. Every
    OnUpgrade that mentions Innate or Retain ADDS it -- the upgrade grants the keyword --
    and every one that mentions Exhaust or Ethereal REMOVES it, which is usually the
    entire reason to upgrade the card. A check that only asked "is the keyword mentioned
    in OnUpgrade" would read those two groups as the same thing and get one of them
    backwards.
    """
    m = _ON_UPGRADE_BODY.search(text)
    if not m:
        return False
    for line in m.group(1).splitlines():
        if f"CardKeyword.{keyword}" in line and verb in line:
            return True
    return False


# ── helpers ───────────────────────────────────────────────────────────────────


def cs_header() -> str:
    return (
        "// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.\n"
        "using Sts2Emulator.Core;\n"
    )


def decimal_to_int(s: str) -> int:
    return int(float(s))


# ── stable ids ────────────────────────────────────────────────────────────────
#
# Ids used to be a running counter over sorted(glob(...)), which made every id a
# function of how many entities sort before it: adding or renaming one card
# renumbered everything after it, silently invalidating hand-written constants
# (IC.StrikeIronclad = 472), committed fixtures and test literals.
#
# data/id_map.json freezes the mapping. Known names keep their id, new names get
# appended after the current maximum, and ids of removed content stay reserved so
# they are never recycled onto something else.

ID_MAP_PATH = REPO / "data" / "id_map.json"
_ID_MAP: dict[str, dict[str, int]] = {}
_NEW_IDS: dict[str, list[str]] = {}


def load_id_map() -> None:
    global _ID_MAP
    if not ID_MAP_PATH.exists():
        raise SystemExit(
            f"{ID_MAP_PATH} is missing. Seed it with scripts/build_id_map.py.",
        )
    _ID_MAP = json.loads(ID_MAP_PATH.read_text(encoding="utf-8"))


def stable_id(category: str, name: str) -> int:
    """Id for this entity, appending a fresh one if the patch introduced it."""
    mapping = _ID_MAP.setdefault(category, {})
    if name in mapping:
        return mapping[name]
    # Skip the 10000+ reserved band when appending.
    next_id = max((i for i in mapping.values() if i < 10000), default=0) + 1
    mapping[name] = next_id
    _NEW_IDS.setdefault(category, []).append(f"{name}={next_id}")
    return next_id


def save_id_map() -> None:
    ordered = {
        cat: dict(sorted(names.items(), key=operator.itemgetter(1, 0)))
        for cat, names in _ID_MAP.items()
    }
    ID_MAP_PATH.write_text(json.dumps(ordered, indent=2) + "\n", encoding="utf-8")
    for category, added in _NEW_IDS.items():
        print(f"  NEW {category}: {', '.join(added)}")


# ── card id constant classes ──────────────────────────────────────────────────

CARD_ID_CLASSES_PATH = REPO / "data" / "card_id_classes.json"

CLASS_DOC = {
    "IC": "Ironclad cards.",
    "CL": "Colourless and shared cards.",
    "SI": "Silent cards.",
    "AN": "Ancient cards.",
    "ST": "Status and curse cards.",
}


def extract_card_ids(card_ids: dict[str, int]) -> str:
    """Emit the IC/CL/SI/AN/ST id constants from the generated card data.

    These used to be hand-written. With ids no longer tied to sort order they were
    stable for *existing* cards, but a rename would have left a constant silently
    pointing at whatever else landed on that id — with ~340 test references behind
    it. Generating them means the values cannot drift, and a card that disappears
    from the data fails this step loudly instead.

    Raises:
        SystemExit: if a name in card_id_classes.json no longer exists as a card.

    """
    spec = json.loads(CARD_ID_CLASSES_PATH.read_text(encoding="utf-8"))
    blocks: list[str] = []
    missing: list[str] = []

    for cls, members in spec["classes"].items():
        lines = [f"/// <summary>{CLASS_DOC.get(cls, 'Card ids.')}</summary>"]
        lines.extend((f"public static class {cls}", "{"))
        for name in members:
            if name not in card_ids:
                missing.append(f"{cls}.{name}")
                continue
            lines.append(f"    public const int {name} = {card_ids[name]};")
        lines.append("}")
        blocks.append("\n".join(lines))

    if missing:
        raise SystemExit(
            "These id constants name cards that no longer exist in Cards.g.cs:\n  "
            + "\n  ".join(missing)
            + "\n\nA patch renamed or removed them. Update data/card_id_classes.json "
            "(and any code using the constant) rather than letting it point elsewhere.",
        )

    print(f"  Card ids: {sum(len(m) for m in spec['classes'].values())} constants.")
    return (
        cs_header()
        + "\nnamespace Sts2Emulator.Core.Effects;\n\n"
        + "\n\n".join(blocks)
        + "\n"
    )


# ── card extraction ───────────────────────────────────────────────────────────

SPECIAL_CARD_IDS = {
    "AscendersBane": 10001,
    "Dazed": 10002,
}


def slugify(name: str) -> str:
    r"""Reproduce the game's StringHelper.Slugify, which produces a ModelId.Entry.

    Worth having in the data rather than derived at runtime: the mid-combat reshuffle
    sorts the pile by ModelId before shuffling (ListExtensions.StableShuffle), and
    ModelId compares Category then Entry as ordinal *strings*. Sorting by our own
    numeric ids instead puts the pile in a different order, and Fisher-Yates over a
    different order is a different shuffle even from the same stream.

    .NET's \G continuation has no direct Python equivalent, so the camel-case split is
    done by hand: an underscore goes before every capital that follows an alphanumeric,
    and before every capital in a run that started at such a boundary.
    """
    out: list[str] = []
    for i, ch in enumerate(name.strip()):
        if ch.isupper() and i > 0 and (name[i - 1].isalnum()):
            out.append("_")
        out.append(ch)
    slug = re.sub(r"\s+", "_", "".join(out).upper())
    return re.sub(r"[^A-Z0-9_]", "", slug)


def extract_cards() -> str:
    entries: list[str] = []

    for f in sorted(CARDS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "CardModel" not in text:
            continue
        # `Modded` was excluded here from the initial commit with no reason given, and it
        # is unconditionally in DefectCardPool between Meteor Strike and Momentum Strike --
        # so the emulator's Defect pool was 87 cards where the game has 88, and the card
        # could never be offered, generated or played. A live capture of it refused to
        # generate because the id map had never heard of it.
        if name == "DeprecatedCard":
            continue

        ctor = CARD_CTOR.search(text)
        if not ctor:
            continue

        cost = int(ctor.group(1))
        card_type = ctor.group(2)  # Attack / Skill / Power / Status / Curse
        rarity = ctor.group(3)
        # A card with no fourth argument takes CardModel's default, which is Self.
        target_type = ctor.group(4) or "Self"
        # Negative-cost cards are statuses/curses; keep the ones the id map knows
        # (they are referenced by the engine) and skip the rest, as before.
        if cost < 0 and name not in _ID_MAP.get("cards", {}):
            continue

        dmg_m = DAMAGE_VAR.search(text)
        blk_m = BLOCK_VAR.search(text)
        base_dmg = decimal_to_int(dmg_m.group(1)) if dmg_m else 0
        base_block = decimal_to_int(blk_m.group(1)) if blk_m else 0

        upg_cost_m = UPGRADE_COST.search(text)
        upg_cost = int(upg_cost_m.group(1)) if upg_cost_m else 0

        upg_dmg_m = UPGRADE_DMG.search(text)
        upg_blk_m = UPGRADE_BLOCK.search(text)
        upg_dmg = decimal_to_int(upg_dmg_m.group(1)) if upg_dmg_m else 0
        upg_block = decimal_to_int(upg_blk_m.group(1)) if upg_blk_m else 0

        def_id = stable_id("cards", name)
        # Retain and Sly were missing from this tuple, and both are read by the engine:
        # `CardInstanceExtensions.IsRetained` decides what survives the end-of-turn hand
        # discard, and Sly decides what auto-plays when an effect discards it. Eleven cards
        # declare Retain and eight declare Sly, and not one of them was marked -- a field
        # the extractor never emits reads exactly like a card that does not have it.
        flags = [
            f"{keyword}: true"
            for keyword in (
                "Ethereal",
                "Exhaust",
                "Unplayable",
                "Retain",
                "Sly",
                "Eternal",
            )
            if has_canonical_keyword(text, keyword)
        ]
        # Innate needs precise attribution, unlike the flags above: 9 cards declare
        # it in CanonicalKeywords (always innate) while 15 others only gain it via
        # OnUpgrade.  A substring check would mark the latter permanently innate and
        # silently corrupt the turn-1 draw-pile reorder.
        if innate_canonical(text):
            flags.append("Innate: true")
        if innate_on_upgrade(text):
            flags.append("InnateWhenUpgraded: true")
        # Retain is granted by an upgrade on twelve cards, exactly as Innate is on
        # fifteen -- and like Innate it needs its own flag, because a card that only
        # retains once upgraded must not retain before then.
        if keyword_on_upgrade(text, "Retain", "Add"):
            flags.append("RetainWhenUpgraded: true")
        # Exhaust and Ethereal go the other way: nineteen cards drop Exhaust when
        # upgraded and three drop Ethereal, and for most of those it is the whole
        # benefit of the upgrade.
        if keyword_on_upgrade(text, "Exhaust", "Remove"):
            flags.append("ExhaustRemovedWhenUpgraded: true")
        if keyword_on_upgrade(text, "Ethereal", "Remove"):
            flags.append("EtherealRemovedWhenUpgraded: true")
        if HAS_ENERGY_COST_X.search(text):
            flags.append("HasEnergyCostX: true")
        if match := STAR_COST.search(text):
            flags.append(f"StarCost: {match.group(1)}")
        if HAS_STAR_COST_X.search(text):
            flags.append("HasStarCostX: true")
        if MULTIPLAYER_ONLY.search(text):
            flags.append("MultiplayerOnly: true")
        # CardModel.CanBeGeneratedByModifiers. Eight curses refuse to be handed out by
        # anything that rolls one -- Neow's Bones among them -- so the roll has to read it.
        if "CanBeGeneratedByModifiers => false" in text:
            flags.append("CanBeGeneratedByModifiers: false")
        # CardModel.CanBeGeneratedInCombat. Fourteen cards refuse to be rolled by an
        # in-combat generator, and CardFactory.FilterForCombat drops them alongside the
        # Basic, Ancient and Event rarities. Infernal Blade kept a hand-written copy of
        # that answer and it had drifted by two entries in opposite directions.
        if "CanBeGeneratedInCombat => false" in text:
            flags.append("CanBeGeneratedInCombat: false")
        # CardTag.OstyAttack -- the Necrobinder's pet attacks. Squeeze's damage counts how
        # many of them the deck holds, so the tag has to be data rather than a comment.
        if "CardTag.OstyAttack" in text:
            flags.append("OstyAttack: true")
        # CardTag.Strike / CardTag.Defend. `Card.cs` stood these in with an entry-slug
        # prefix test and said so in a comment: true for Basic cards, where the tag and the
        # name agree, and wrong past them -- Perfected Strike is tagged Strike and is not
        # Basic. Amalgamator filters the deck on the real tag, so the real tag is extracted.
        if "CardTag.Strike" in text:
            flags.append("StrikeTag: true")
        if "CardTag.Defend" in text:
            flags.append("DefendTag: true")
        # CardTag.Shiv and CardTag.Minion. Helical Dart reads the Shiv tag off the card it
        # was just played from, and Vitruvian Minion doubles both damage and block from a
        # Minion-tagged card -- neither is answerable from the card's name, and Blade of
        # Ink's Shivs and Knife Trap's replays are the reason.
        if "CardTag.Shiv" in text:
            flags.append("ShivTag: true")
        if "CardTag.Minion" in text:
            flags.append("MinionTag: true")
        # CardModel.MaxUpgradeLevel. The base is 1; the cards that override it all
        # override it to 0, which is what IsUpgradable reads to refuse an upgrade.
        if "MaxUpgradeLevel => 0" in text:
            flags.append("Upgradable: false")
        flags_cs = f", {', '.join(flags)}" if flags else ""

        entries.append(
            f'        new CardDef(Id: {def_id}, Name: "{name}", '
            f'Entry: "{slugify(name)}", '
            f"Cost: {cost}, BaseDamage: {base_dmg}, BaseBlock: {base_block}, "
            f"UpgradeDamage: {upg_dmg}, UpgradeBlock: {upg_block}, "
            f"UpgradeCost: {upg_cost}, "
            f"Type: CardType.{card_type}, Rarity: CardRarity.{rarity}, "
            f"Target: CardTarget.{target_type}{flags_cs}),",
        )
    if not entries:
        entries = ["        // No cards extracted — check CARDS_DIR path."]

    print(f"  Cards: {len(entries)} extracted.")
    lines = "\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

internal static class Cards
{{
    private static readonly CardDef[] _all =
    [
{lines}
    ];

    /// <summary>Every card, for callers that need to ask a question of the whole set.</summary>
    public static ReadOnlySpan<CardDef> All => _all;

    public static CardDef Get(int id) =>
        Array.Find(_all, c => c.Id == id) is {{ Id: not 0 }} def
            ? def
            : throw new ArgumentException($"Unknown card id {{id}}");

    public static int? FindId(string name) =>
        Array.Find(_all, c => c.Name == name) is {{ Id: not 0 }} def
            ? def.Id
            : null;
}}
"""


# ── monster / enemy extraction ────────────────────────────────────────────────


def extract_enemies() -> str:
    entries: list[str] = []

    for f in sorted(MONSTERS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "MonsterModel" not in text:
            continue
        if name in (
            "DeprecatedMonster",
            "MultiAttackMoveMonster",
            "SingleAttackMoveMonster",
            "OneHpMonster",
            "TenHpMonster",
            "BigDummy",
        ):
            continue

        # HP — follow a named property to what it actually says, first: the regexes
        # below match a literal or a GetValueIfAscension call, and neither is what
        # `MinInitialHp => FirstFormHp` is.
        for referent in dict.fromkeys(HP_INDIRECT.findall(text)):
            named = re.search(
                rf"\b{re.escape(referent)}\s*=>\s*(.+?);",
                text,
            )
            if named is not None:
                text += f"\n\tpublic override int MinInitialHp => {named.group(1)};\n"

        # HP — try AscensionHelper form first, then plain int
        ascension_hps = HP_ASCENSION.findall(text)
        if ascension_hps:
            high = [int(pair[0]) for pair in ascension_hps]
            low = [int(pair[1]) for pair in ascension_hps]
        else:
            plain = [int(x) for x in HP_PLAIN.findall(text)]
            high = low = plain
        min_hp = high[0] if high else 0
        max_hp = high[1] if len(high) > 1 else min_hp
        min_hp_low = low[0] if low else min_hp
        max_hp_low = low[1] if len(low) > 1 else min_hp_low

        # Collect attack intents (damage values) for the move list
        single_attacks = [(int(m), 1) for m in SINGLE_ATTACK.findall(text)]
        multi_attacks = [(int(d), int(r)) for d, r in MULTI_ATTACK.findall(text)]
        attacks = single_attacks + multi_attacks

        # Encode moves as a compact int array: [damage, repeats, ...]
        if attacks:
            move_arr = ", ".join(f"{d}, {r}" for d, r in attacks)
            moves_cs = f"new int[] {{ {move_arr} }}"
        else:
            moves_cs = "Array.Empty<int>()"

        entries.append(
            f'        new EnemyDef(Id: {stable_id("enemies", name)}, Name: "{name}", '
            f"MinHp: {min_hp}, MaxHp: {max_hp}, "
            f"MinHpBelowToughEnemies: {min_hp_low}, MaxHpBelowToughEnemies: {max_hp_low}, "
            f"Moves: {moves_cs}),",
        )

    if not entries:
        entries = ["        // No enemies extracted — check MONSTERS_DIR path."]

    print(f"  Enemies: {len(entries)} extracted.")
    lines = "\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

internal static class Enemies
{{
    private static readonly EnemyDef[] _all =
    [
{lines}
    ];

    public static EnemyDef Get(int id) =>
        Array.Find(_all, e => e.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown enemy id {{id}}");

    public static Intent ChooseIntent(int enemyId, int moveIndex, int turn, Random rng)
    {{
        var def = Get(enemyId);
        if (def.Moves.Length == 0) return new Intent(IntentType.Unknown, 0);
        // Moves array: [damage0, repeats0, damage1, repeats1, ...]
        // Cycle through move pairs based on moveIndex
        int pairIndex = moveIndex % (def.Moves.Length / 2);
        int damage  = def.Moves[pairIndex * 2];
        int repeats = def.Moves[pairIndex * 2 + 1];
        return damage == 0
            ? new Intent(IntentType.Buff, 0)
            : new Intent(IntentType.Attack, damage * repeats);
    }}

    public static void ApplyBuffIntent(EnemyState enemy, CombatState state, Random rng)
    {{
        // Per-enemy buff logic is hand-implemented in Core/Effects after reviewing decompiled moves
    }}
}}
"""


# ── power extraction ──────────────────────────────────────────────────────────


def extract_powers() -> str:
    entries: list[str] = []

    for f in sorted(POWERS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "PowerModel" not in text:
            continue

        pt_m = POWER_TYPE.search(text)
        stack_m = POWER_STACK.search(text)
        is_buff = pt_m.group(1) == "Buff" if pt_m else True
        stack = stack_m.group(1) if stack_m else "Counter"
        ticks = "TickDownDuration" in text

        entries.append(
            f'        new PowerDef(Id: {stable_id("powers", name)}, Name: "{name}", '
            f'IsBuff: {str(is_buff).lower()}, StackType: "{stack}", TicksDown: {str(ticks).lower()}),',
        )

    if not entries:
        entries = ["        // No powers extracted — check POWERS_DIR path."]

    print(f"  Powers: {len(entries)} extracted.")
    lines = "\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

internal static class Powers
{{
    private static readonly PowerDef[] _all =
    [
{lines}
    ];

    public static PowerDef Get(int id) =>
        Array.Find(_all, p => p.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown power id {{id}}");

    public static int? FindId(string name) =>
        Array.Find(_all, p => p.Name == name) is {{ Id: > 0 }} def
            ? def.Id
            : null;
}}
"""


# ── relic extraction ──────────────────────────────────────────────────────────


def extract_relics() -> str:
    entries: list[str] = []

    for f in sorted(RELICS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "RelicModel" not in text:
            continue
        if name == "DeprecatedRelic":
            continue

        # RelicModel.IsTradable decides whether a relic can be handed over -- Ranwid
        # the Elder's third option, and the relic trader's stock -- and it reads three
        # things off the model: the rarity, whether picking it up did something that
        # cannot be given back, and whether it spawns a pet.
        rarity = re.search(r"RelicRarity Rarity => RelicRarity\.(\w+)", text)
        fields = [
            f'Id: {stable_id("relics", name)}',
            f'Name: "{name}"',
            f'Entry: "{slugify(name)}"',
            f"Rarity: RelicRarity.{rarity.group(1) if rarity else 'None'}",
        ]
        if "HasUponPickupEffect => true" in text:
            fields.append("HasUponPickupEffect: true")
        if "SpawnsPets => true" in text:
            fields.append("SpawnsPets: true")
        # RelicModel.IsAllowedInShops. Five relics refuse to be sold, and the shop pulls
        # are filtered on it -- Welcome to Wongos included, whose bargain bin and featured
        # item both go through PullNextRelicFromFront with this filter.
        if "IsAllowedInShops => false" in text:
            fields.append("IsAllowedInShops: false")
        # `IsAllowed => RelicModel.IsBeforeAct3TreasureChest(runState)`: the relic is not
        # offered once the run passes floor 41. Extracted rather than listed, because the
        # hand-kept list in RelicGrabBag had drifted by three -- MealTicket, OldCoin and
        # WhiteBeastStatue -- and a list that is short fails only on the paths that touch
        # its missing members.
        if "IsBeforeAct3TreasureChest" in text:
            fields.append("StopsAfterAct3Chest: true")
        entries.append(f"        new RelicDef({', '.join(fields)}),")

    if not entries:
        entries = ["        // No relics extracted — check RELICS_DIR path."]

    print(f"  Relics: {len(entries)} extracted.")
    lines = "\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

internal static class Relics
{{
    private static readonly RelicDef[] _all =
    [
{lines}
    ];

    /// <summary>Every relic, for audits and for tests that assert over the whole set.</summary>
    public static ReadOnlySpan<RelicDef> All => _all;

    public static RelicDef Get(int id) =>
        Array.Find(_all, r => r.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown relic id {{id}}");

    public static int? FindId(string name) =>
        Array.Find(_all, r => r.Name == name) is {{ Id: > 0 }} def
            ? def.Id
            : null;

    /// <summary>Lookup that does not throw, for ids that may not name a relic at all.</summary>
    public static bool TryGet(int id, out RelicDef def)
    {{
        def = Array.Find(_all, r => r.Id == id);
        return def.Id > 0;
    }}
}}
"""


# ── potion extraction ─────────────────────────────────────────────────────────


def potion_rarity(text: str) -> str:
    """Return the potion's declared rarity, or None when it does not declare one."""
    match = re.search(r"PotionRarity Rarity => PotionRarity\.(\w+)", text)
    return match.group(1) if match else "None"


def extract_potions() -> str:
    entries: list[str] = []

    for f in sorted(POTIONS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "PotionModel" not in text:
            continue
        if name in (
            "DeprecatedPotion",
            "PotionBody",
            "PotionBodyExtensions",
            "PotionOverlay",
            "PotionProcureFailureReason",
            "PotionProcureResult",
            "PotionRarity",
            "PotionRarityExtensions",
            "PotionUsage",
        ):
            continue

        entries.append(
            # Rarity decides which potions a roll may land on, and PotionFactory rolls
            # a rarity before it picks -- so a potion whose rarity is unknown is a potion
            # in the wrong bucket. A hand-written table used to supply this and defaulted
            # anything it did not know to Common.
            f'        new PotionDef(Id: {stable_id("potions", name)}, Name: "{name}", '
            f"Rarity: PotionRarity.{potion_rarity(text)}),",
        )

    if not entries:
        entries = ["        // No potions extracted — check POTIONS_DIR path."]

    print(f"  Potions: {len(entries)} extracted.")
    lines = "\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

internal static class Potions
{{
    private static readonly PotionDef[] _all =
    [
{lines}
    ];

    public static PotionDef Get(int id) =>
        Array.Find(_all, p => p.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown potion id {{id}}");

    public static int? FindId(string name) =>
        Array.Find(_all, p => p.Name == name) is {{ Id: > 0 }} def
            ? def.Id
            : null;
}}
"""


# ── main ──────────────────────────────────────────────────────────────────────


CARD_POOLS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.CardPools"

# The pools a run can draw from. Deprecated/Mock are the game's own test scaffolding,
# and Curse/Status/Token/Event/Quest are not character pools.
# Curse and Status are not character pools, but a transform draws from the pool its
# original card came from -- so transforming a curse needs the curse pool.
PLAYABLE_POOLS = (
    "Ironclad",
    "Silent",
    "Defect",
    "Necrobinder",
    "Regent",
    "Colorless",
    "Curse",
    "Status",
)


def extract_card_pools(card_ids: dict[str, int]) -> str:
    """Each character's card pool, in the order the pool declares it.

    Kaleidoscope needs this and nothing else could supply it: it offers cards from the
    pools of characters the player is NOT, and a CardDef carries no character. Order is
    preserved because the game shuffles the pool list itself (StableShuffle on Niche) and
    a differently-ordered list shuffles differently.
    """
    entries: list[str] = []
    for pool in PLAYABLE_POOLS:
        path = CARD_POOLS_DIR / f"{pool}CardPool.cs"
        if not path.exists():
            print(f"  Card pools: {pool} not found, skipping.")
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        body = re.search(r"GenerateAllCards\(\)[\s\S]*?\{([\s\S]*?)\n\t\}", text)
        if body is None:
            print(f"  Card pools: could not parse {pool}.")
            continue
        names = re.findall(r"ModelDb\.Card<(\w+)>\(\)", body.group(1))
        ids = [card_ids[name] for name in names if name in card_ids]
        missing = [name for name in names if name not in card_ids]
        if missing:
            print(f"  Card pools: {pool} skipped {len(missing)} unextracted cards.")
        entries.append(
            f"    /// <summary>{pool}: {len(ids)} cards, in pool order.</summary>\n"
            f"    public static ReadOnlySpan<int> {pool} =>\n        [{', '.join(map(str, ids))}];",
        )
        print(f"  Card pools: {pool} {len(ids)} cards.")

    joined = "\n\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

/// <summary>
/// The character card pools, extracted from the game's CardPoolModel declarations.
/// A CardDef says nothing about which character owns it, so anything that draws from
/// another character's pool — Kaleidoscope at Neow — has to read it from here.
/// </summary>
internal static class CardPools
{{
{joined}
}}
"""


POTION_POOLS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.PotionPools"
EPOCHS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Timeline.Epochs"

# The pools a run's potions come from: the shared pool plus the pool of the character
# being played. Event/Token/Mock/Deprecated are not draw pools.
POTION_POOL_NAMES = ("Shared", "Ironclad", "Silent", "Defect", "Necrobinder", "Regent")


def _potion_names_in(text: str) -> list[str]:
    """Potion class names in declaration order, following an epoch indirection.

    A character's pool does not list its potions: it returns <Character>4Epoch.Potions,
    and the epoch builds the list. The shared pool declares its own array inline.
    """
    names = re.findall(r"ModelDb\.Potion<(\w+)>\(\)", text)
    if names:
        return names

    epoch = re.search(r"(\w+Epoch)\.Potions", text)
    if epoch is None:
        return []
    path = EPOCHS_DIR / f"{epoch.group(1)}.cs"
    if not path.exists():
        return []
    return re.findall(
        r"ModelDb\.Potion<(\w+)>\(\)",
        path.read_text(encoding="utf-8", errors="replace"),
    )


def extract_potion_pools(potion_ids: dict[str, int]) -> str:
    """Each character's potion pool plus the shared one, in declaration order.

    PotionFactory.GetPotionOptions builds what a shop or a reward can offer as the
    character's pool concatenated with the shared pool, and NextItem indexes into that
    list -- so both the membership and the order are load-bearing. A hand-written stand-in
    for this was 43 potions against the real 48, which is why the merchant stocked the
    wrong ones.
    """
    entries: list[str] = []
    for pool in POTION_POOL_NAMES:
        path = POTION_POOLS_DIR / f"{pool}PotionPool.cs"
        if not path.exists():
            print(f"  Potion pools: {pool} not found, skipping.")
            continue
        names = _potion_names_in(path.read_text(encoding="utf-8", errors="replace"))
        ids = [potion_ids[name] for name in names if name in potion_ids]
        missing = [name for name in names if name not in potion_ids]
        if missing:
            print(f"  Potion pools: {pool} skipped {len(missing)} unextracted potions.")
        entries.append(
            f"    /// <summary>{pool}: {len(ids)} potions, in pool order.</summary>\n"
            f"    public static ReadOnlySpan<int> {pool} =>\n        [{', '.join(map(str, ids))}];",
        )
        print(f"  Potion pools: {pool} {len(ids)} potions.")

    joined = "\n\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

/// <summary>
/// The potion pools, extracted from the game's PotionPoolModel declarations. What a shop
/// or a reward may offer is the character's pool followed by the shared one — see
/// PotionFactory.GetPotionOptions — and the order matters because NextItem indexes into
/// the concatenation.
/// </summary>
internal static class PotionPools
{{
{joined}
}}
"""


RELIC_POOLS_DIR = DECOMPILED / "MegaCrit.Sts2.Core.Models.RelicPools"

# The pools a run's relic grab bag is built from: the shared pool plus the pool of the
# character being played. Event/Fallback/Deprecated are not grab-bag pools.
RELIC_POOL_NAMES = ("Shared", "Ironclad", "Silent", "Defect", "Necrobinder", "Regent")


def extract_relic_pools(relic_ids: dict[str, int]) -> str:
    """Each relic pool, in the order the pool declares it.

    Order is load-bearing. RelicGrabBag.Populate concatenates the shared pool and the
    character's, buckets the result by rarity and UnstableShuffles each bucket, so a
    differently-ordered list shuffles into a different queue and every relic the run ever
    hands out changes.
    """
    entries: list[str] = []
    for pool in RELIC_POOL_NAMES:
        path = RELIC_POOLS_DIR / f"{pool}RelicPool.cs"
        if not path.exists():
            print(f"  Relic pools: {pool} not found, skipping.")
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        body = re.search(r"GenerateAllRelics\(\)[\s\S]*?\{([\s\S]*?)\n\t\}", text)
        if body is None:
            print(f"  Relic pools: could not parse {pool}.")
            continue
        names = re.findall(r"ModelDb\.Relic<(\w+)>\(\)", body.group(1))
        ids = [relic_ids[name] for name in names if name in relic_ids]
        missing = [name for name in names if name not in relic_ids]
        if missing:
            print(f"  Relic pools: {pool} skipped {len(missing)} unextracted relics.")
        entries.append(
            f"    /// <summary>{pool}: {len(ids)} relics, in pool order.</summary>\n"
            f"    public static ReadOnlySpan<int> {pool} =>\n        [{', '.join(map(str, ids))}];",
        )
        print(f"  Relic pools: {pool} {len(ids)} relics.")

    joined = "\n\n".join(entries)
    return f"""{cs_header()}namespace Sts2Emulator.GeneratedData;

/// <summary>
/// The relic pools, extracted from the game's RelicPoolModel declarations.
///
/// RelicGrabBag.Populate builds a run's relic queue from the shared pool plus the
/// character's, so this is where the queue's contents AND their pre-shuffle order come
/// from. A RelicDef carries a rarity but not a pool, and the grab bag needs both.
/// </summary>
internal static class RelicPools
{{
{joined}
}}
"""


def main() -> None:
    load_id_map()
    if not DECOMPILED.exists():
        print("decompiled/ not found. Run scripts/decompile.sh first.", file=sys.stderr)
        sys.exit(1)

    for d in (CARDS_DIR, MONSTERS_DIR, POWERS_DIR, RELICS_DIR, POTIONS_DIR):
        if not d.exists():
            print(f"Warning: {d.name} not found in decompiled/", file=sys.stderr)

    GENERATED.mkdir(parents=True, exist_ok=True)

    cards_cs = extract_cards()
    # Card-id constants are derived from the freshly extracted card data, so the two
    # can never disagree.
    card_ids = {
        name: int(raw)
        for raw, name in re.findall(
            r'new CardDef\(Id: (\d+), Name: "([^"]+)"',
            cards_cs,
        )
    }

    potions_cs = extract_potions()
    potion_ids = {
        name: int(raw)
        for raw, name in re.findall(
            r'new PotionDef\(Id: (\d+), Name: "([^"]+)"',
            potions_cs,
        )
    }

    relics_cs = extract_relics()
    relic_ids = {
        name: int(raw)
        for raw, name in re.findall(
            r'new RelicDef\(Id: (\d+), Name: "([^"]+)"',
            relics_cs,
        )
    }

    for filename, content in [
        ("Cards.g.cs", cards_cs),
        ("CardIds.g.cs", extract_card_ids(card_ids)),
        ("Enemies.g.cs", extract_enemies()),
        ("Powers.g.cs", extract_powers()),
        ("Relics.g.cs", relics_cs),
        ("Potions.g.cs", potions_cs),
        ("CardPools.g.cs", extract_card_pools(card_ids)),
        ("PotionPools.g.cs", extract_potion_pools(potion_ids)),
        ("RelicPools.g.cs", extract_relic_pools(relic_ids)),
    ]:
        out = GENERATED / filename
        out.write_text(content, encoding="utf-8")
        print(f"  wrote {out.relative_to(REPO)}")

    save_id_map()
    print("extract_data.py complete.")


if __name__ == "__main__":
    main()
