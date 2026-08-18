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
CARD_CTOR = re.compile(r"base\((-?\d+),\s*CardType\.(\w+),\s*CardRarity\.(\w+)")
# DamageVar(6m, ...) or DamageVar(6, ...)
DAMAGE_VAR = re.compile(r"new DamageVar\((\d+(?:\.\d+)?)m?,")
# BlockVar(5m, ...)
BLOCK_VAR = re.compile(r"new BlockVar\((\d+(?:\.\d+)?)m?,")
# UpgradeValueBy on damage / block
UPGRADE_DMG = re.compile(r"DynamicVars\.Damage\.UpgradeValueBy\((\d+(?:\.\d+)?)m?\)")
UPGRADE_BLOCK = re.compile(r"DynamicVars\.Block\.UpgradeValueBy\((\d+(?:\.\d+)?)m?\)")

# HP: plain int or AscensionHelper (take the max-ascension value = 1st arg)
HP_PLAIN = re.compile(r"(?:Min|Max)InitialHp\s*=>\s*(\d+)\s*;")
HP_ASCENSION = re.compile(
    r"(?:Min|Max)InitialHp\s*=>.+?GetValueIfAscension\([^,]+,\s*(\d+),\s*\d+\s*\)",
)

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
    m = _ON_UPGRADE_BODY.search(text)
    return bool(m and "CardKeyword.Innate" in m.group(1))


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


def extract_cards() -> str:
    entries: list[str] = []

    for f in sorted(CARDS_DIR.glob("*.cs"), key=lambda p: p.stem.lower()):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "CardModel" not in text:
            continue
        if name in ("DeprecatedCard", "Modded"):
            continue

        ctor = CARD_CTOR.search(text)
        if not ctor:
            continue

        cost = int(ctor.group(1))
        card_type = ctor.group(2)  # Attack / Skill / Power / Status / Curse
        rarity = ctor.group(3)
        # Negative-cost cards are statuses/curses; keep the ones the id map knows
        # (they are referenced by the engine) and skip the rest, as before.
        if cost < 0 and name not in _ID_MAP.get("cards", {}):
            continue

        dmg_m = DAMAGE_VAR.search(text)
        blk_m = BLOCK_VAR.search(text)
        base_dmg = decimal_to_int(dmg_m.group(1)) if dmg_m else 0
        base_block = decimal_to_int(blk_m.group(1)) if blk_m else 0

        upg_dmg_m = UPGRADE_DMG.search(text)
        upg_blk_m = UPGRADE_BLOCK.search(text)
        upg_dmg = decimal_to_int(upg_dmg_m.group(1)) if upg_dmg_m else 0
        upg_block = decimal_to_int(upg_blk_m.group(1)) if upg_blk_m else 0

        def_id = stable_id("cards", name)
        flags = [
            f"{keyword}: true"
            for keyword in ("Ethereal", "Exhaust", "Unplayable")
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
        flags_cs = f", {', '.join(flags)}" if flags else ""

        entries.append(
            f'        new CardDef(Id: {def_id}, Name: "{name}", '
            f"Cost: {cost}, BaseDamage: {base_dmg}, BaseBlock: {base_block}, "
            f"UpgradeDamage: {upg_dmg}, UpgradeBlock: {upg_block}, "
            f"Type: CardType.{card_type}, Rarity: CardRarity.{rarity}{flags_cs}),",
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
            "FakeMerchantMonster",
            "BattleFriendV1",
            "BattleFriendV2",
            "BattleFriendV3",
            "TestSubject",
        ):
            continue

        # HP — try AscensionHelper form first, then plain int
        min_hps = HP_ASCENSION.findall(text) or HP_PLAIN.findall(text)
        min_hp = int(min_hps[0]) if min_hps else 0
        max_hp = int(min_hps[1]) if len(min_hps) > 1 else min_hp

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
            f"MinHp: {min_hp}, MaxHp: {max_hp}, Moves: {moves_cs}),",
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

        entries.append(
            f'        new RelicDef(Id: {stable_id("relics", name)}, Name: "{name}"),',
        )

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

    public static RelicDef Get(int id) =>
        Array.Find(_all, r => r.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown relic id {{id}}");

    public static int? FindId(string name) =>
        Array.Find(_all, r => r.Name == name) is {{ Id: > 0 }} def
            ? def.Id
            : null;
}}
"""


# ── potion extraction ─────────────────────────────────────────────────────────


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
            f'        new PotionDef(Id: {stable_id("potions", name)}, Name: "{name}"),',
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

    for filename, content in [
        ("Cards.g.cs", cards_cs),
        ("CardIds.g.cs", extract_card_ids(card_ids)),
        ("Enemies.g.cs", extract_enemies()),
        ("Powers.g.cs", extract_powers()),
        ("Relics.g.cs", extract_relics()),
        ("Potions.g.cs", extract_potions()),
    ]:
        out = GENERATED / filename
        out.write_text(content, encoding="utf-8")
        print(f"  wrote {out.relative_to(REPO)}")

    save_id_map()
    print("extract_data.py complete.")


if __name__ == "__main__":
    main()
