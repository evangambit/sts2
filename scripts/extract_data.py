#!/usr/bin/env python3
"""Parse decompiled sts2.dll C# source and emit Generated/*.g.cs files."""

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

# ── helpers ───────────────────────────────────────────────────────────────────


def cs_header() -> str:
    return (
        "// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.\n"
        "using Sts2Emulator.Core;\n"
    )


def decimal_to_int(s: str) -> int:
    return int(float(s))


# ── card extraction ───────────────────────────────────────────────────────────

SPECIAL_CARD_IDS = {
    "AscendersBane": 10001,
    "Dazed": 10002,
}


def extract_cards() -> str:
    entries: list[str] = []
    card_id = 1

    for f in sorted(CARDS_DIR.glob("*.cs")):
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
        if cost < 0 and name not in SPECIAL_CARD_IDS:
            continue

        dmg_m = DAMAGE_VAR.search(text)
        blk_m = BLOCK_VAR.search(text)
        base_dmg = decimal_to_int(dmg_m.group(1)) if dmg_m else 0
        base_block = decimal_to_int(blk_m.group(1)) if blk_m else 0

        upg_dmg_m = UPGRADE_DMG.search(text)
        upg_blk_m = UPGRADE_BLOCK.search(text)
        upg_dmg = decimal_to_int(upg_dmg_m.group(1)) if upg_dmg_m else 0
        upg_block = decimal_to_int(upg_blk_m.group(1)) if upg_blk_m else 0

        def_id = SPECIAL_CARD_IDS.get(name, card_id)
        flags = []
        if "CardKeyword.Ethereal" in text:
            flags.append("Ethereal: true")
        if "CardKeyword.Exhaust" in text:
            flags.append("Exhaust: true")
        if "CardKeyword.Unplayable" in text:
            flags.append("Unplayable: true")
        flags_cs = f", {', '.join(flags)}" if flags else ""

        entries.append(
            f'        new CardDef(Id: {def_id}, Name: "{name}", '
            f"Cost: {cost}, BaseDamage: {base_dmg}, BaseBlock: {base_block}, "
            f"UpgradeDamage: {upg_dmg}, UpgradeBlock: {upg_block}, "
            f"Type: CardType.{card_type}, Rarity: CardRarity.{rarity}{flags_cs}),",
        )
        if name not in SPECIAL_CARD_IDS:
            card_id += 1

    if not entries:
        entries = ["        // No cards extracted — check CARDS_DIR path."]

    print(f"  Cards: {card_id - 1} extracted.")
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
    enemy_id = 1

    for f in sorted(MONSTERS_DIR.glob("*.cs")):
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
            f'        new EnemyDef(Id: {enemy_id}, Name: "{name}", '
            f"MinHp: {min_hp}, MaxHp: {max_hp}, Moves: {moves_cs}),",
        )
        enemy_id += 1

    if not entries:
        entries = ["        // No enemies extracted — check MONSTERS_DIR path."]

    print(f"  Enemies: {enemy_id - 1} extracted.")
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
    power_id = 1

    for f in sorted(POWERS_DIR.glob("*.cs")):
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
            f'        new PowerDef(Id: {power_id}, Name: "{name}", '
            f'IsBuff: {str(is_buff).lower()}, StackType: "{stack}", TicksDown: {str(ticks).lower()}),',
        )
        power_id += 1

    if not entries:
        entries = ["        // No powers extracted — check POWERS_DIR path."]

    print(f"  Powers: {power_id - 1} extracted.")
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
    relic_id = 1

    for f in sorted(RELICS_DIR.glob("*.cs")):
        name = f.stem
        text = f.read_text(encoding="utf-8", errors="replace")

        if "RelicModel" not in text:
            continue
        if name == "DeprecatedRelic":
            continue

        entries.append(f'        new RelicDef(Id: {relic_id}, Name: "{name}"),')
        relic_id += 1

    if not entries:
        entries = ["        // No relics extracted — check RELICS_DIR path."]

    print(f"  Relics: {relic_id - 1} extracted.")
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
    potion_id = 1

    for f in sorted(POTIONS_DIR.glob("*.cs")):
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

        entries.append(f'        new PotionDef(Id: {potion_id}, Name: "{name}"),')
        potion_id += 1

    if not entries:
        entries = ["        // No potions extracted — check POTIONS_DIR path."]

    print(f"  Potions: {potion_id - 1} extracted.")
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
    if not DECOMPILED.exists():
        print("decompiled/ not found. Run scripts/decompile.sh first.", file=sys.stderr)
        sys.exit(1)

    for d in (CARDS_DIR, MONSTERS_DIR, POWERS_DIR, RELICS_DIR, POTIONS_DIR):
        if not d.exists():
            print(f"Warning: {d.name} not found in decompiled/", file=sys.stderr)

    GENERATED.mkdir(parents=True, exist_ok=True)

    for filename, content in [
        ("Cards.g.cs", extract_cards()),
        ("Enemies.g.cs", extract_enemies()),
        ("Powers.g.cs", extract_powers()),
        ("Relics.g.cs", extract_relics()),
        ("Potions.g.cs", extract_potions()),
    ]:
        out = GENERATED / filename
        out.write_text(content, encoding="utf-8")
        print(f"  wrote {out.relative_to(REPO)}")

    print("extract_data.py complete.")


if __name__ == "__main__":
    main()
