#!/usr/bin/env python3
"""Compare EnemyAI's hand-written move behaviour against the CURRENT decompiled source.

The emulator's data tables are generated, so a dev change to a card's damage or an
enemy's HP is re-extracted and reported by `diff_patch.py`. Monster BEHAVIOUR is not:
318 `new Intent(...)` constructions live in `EnemyAI.cs`, transcribed by hand from the
monster classes, and nothing links them back to what the game now says.

That matters more than it looks, because **a test written from the decompiled source is
a snapshot of the source at the time of writing**. If the devs change a move's damage,
its order or the intents it declares, the emulator keeps the old value, the test keeps
asserting the old value, and the suite stays green. Only a live capture or a check like
this one notices -- and a capture only covers the encounters that have one.

Three checks, each a WORKLIST rather than a verdict:

  hits   every MultiAttackIntent(damage, repeat) the game declares should have a
         matching `Hits:` in the emulator, or the hits are folded into the damage --
         which is a wrong number AND silently under-triggers every per-instance hook
         (E10, E83, E91, E98, E100, E106).

  types  a MoveState declares a LIST of intents and the readout follows the FIRST one.
         Announcing the type of a later one tells a policy the wrong thing about the
         turn (E12, E97, E105, E108).

  shape  the game gives each monster a MonsterMoveStateMachine -- follow-up pointers,
         conditional and random branches, repeat caps, cooldowns. The emulator walks it
         with `MoveIndex % n` arithmetic, which can only express a plain cycle. Of 117
         machines, 16 are plain chains (E93, E95, E100, E106, E107).

    uv run python scripts/audit_enemy_moves.py
    uv run python scripts/audit_enemy_moves.py --check hits
    uv run python scripts/audit_enemy_moves.py --monster Tunneler
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MONSTERS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Monsters"
ENEMY_AI = REPO / "src" / "Sts2Emulator" / "Core" / "EnemyAI.cs"

MULTI_ATTACK = re.compile(r"new MultiAttackIntent\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)")
MOVE_STATE = re.compile(r'new MoveState\(\s*"([^"]+)"\s*,\s*\w+\s*,\s*(new [^;]+?)\)\s*(?:\{|;|\))')
INTENT_CALL = re.compile(r"new (\w+)Intent\(")

# What the game's intent classes announce as, in the emulator's IntentType vocabulary.
ANNOUNCED_AS = {
    "SingleAttack": "Attack",
    "MultiAttack": "Attack",
    "DeathBlow": "Attack",
    "Defend": "Defend",
    "Buff": "Buff",
    "Summon": "Buff",
    "Heal": "Buff",
    "Debuff": "Debuff",
    "CardDebuff": "Debuff",
    "Status": "Debuff",
    "DebuffStrong": "Debuff",
    "Sleep": "Unknown",
    "Stun": "Unknown",
    "Hidden": "Unknown",
    "Escape": "Unknown",
}

BRANCHING = ("RandomBranchState", "ConditionalBranchState")

# Monster classes the game ships that no run can meet: mocks, dummies and the
# deprecated base. Listed rather than pattern-matched so a real monster cannot join
# them by having a plausible name.
NOT_A_MONSTER = {
    "BigDummy",
    "DeprecatedMonster",
    "MultiAttackMoveMonster",
    "OneHpMonster",
    "SingleAttackMoveMonster",
    "TenHpMonster",
}

# Where the emulator's KE constant is not the class name.
ALIASES = {"FakeMerchantMonster": "FakeMerchant"}


def emulator_blocks() -> dict[str, str]:
    """EnemyAI's text for each monster: its `case KE.X:` arms AND its helper methods.

    The case arms alone are not enough -- several monsters are implemented in a method
    named for them (`ExoskeletonIntent`, `FakeMerchantIntent`), and an audit that reads
    only the switch reports those as missing everything they have.
    """
    text = ENEMY_AI.read_text(encoding="utf-8")
    blocks: dict[str, str] = {}

    marks = [(m.start(), m.group(1)) for m in re.finditer(r"case KE\.(\w+):", text)]
    for index, (start, name) in enumerate(marks):
        end = marks[index + 1][0] if index + 1 < len(marks) else len(text)
        blocks[name] = blocks.get(name, "") + text[start:end]

    for method in re.finditer(r"\n    private static \w+ (\w+?)Intent\(", text):
        name = method.group(1)
        start = method.start()
        nxt = text.find("\n    private static ", start + 1)
        blocks[name] = blocks.get(name, "") + text[start : nxt if nxt > 0 else len(text)]

    return blocks


def move_states(source: str) -> list[tuple[str, list[str]]]:
    """Each MoveState's id and the intents it declares, IN ORDER."""
    states = []
    for name, args in MOVE_STATE.findall(source):
        kinds = [ANNOUNCED_AS.get(k, k) for k in INTENT_CALL.findall(args)]
        if kinds:
            states.append((name, kinds))
    return states


def check_hits(monster: str, source: str, block: str) -> list[str]:
    rows = []
    for damage, repeat in MULTI_ATTACK.findall(source):
        if "Hits:" not in block:
            rows.append(f"    MultiAttackIntent({damage}, {repeat}) with no Hits: in the emulator")
    return rows


def check_types(monster: str, source: str, block: str) -> list[str]:
    rows = []
    for name, kinds in move_states(source):
        if len(kinds) < 2:
            continue
        announced = kinds[0]
        if announced == "Unknown":
            continue
        if f"IntentType.{announced}" not in block:
            rows.append(
                f"    {name} declares {' then '.join(kinds)}, so it announces as "
                f"{announced} -- which the emulator never says for this monster",
            )
    return rows


def check_shape(monster: str, source: str, block: str) -> list[str]:
    """Machines whose shape `MoveIndex %` arithmetic cannot express.

    Ranked, because the shapes are not equally suspicious. A **RandomBranchState** cannot
    be a cycle at all: if the emulator's block never touches `rng`, the monster is
    walking a fixed order where the game rolls. A ConditionalBranchState or a
    slot-keyed opening, on the other hand, is often modelled correctly by seeding
    MoveIndex per creature -- which is what the Myte and the Decimillipede do -- so those
    are a read-the-source prompt rather than a finding.
    """
    shapes = [s for s in BRANCHING if s in source]
    if re.search(r"(\w+)\.FollowUpState = \1\b", source):
        shapes.append("a move that follows up to ITSELF")
    if "StarterMoveIdx" in source or "SlotName ==" in source:
        shapes.append("an opening keyed to slot or starter index")
    if not shapes:
        return []
    # Only interesting where the emulator walks it with plain modular arithmetic.
    if not re.search(r"MoveIndex\s*%", block):
        return []

    rolls = re.search(r"\brng\.", block) is not None
    if "RandomBranchState" in shapes and not rolls:
        joined = ", ".join(shapes)
        strong = (
            "    ** the game ROLLS (RandomBranchState) and the emulator never touches "
            f"rng -- it walks a fixed order. Also has: {joined}"
        )
        return [strong]
    return [
        f"    walked with `MoveIndex %`; the machine has: {', '.join(shapes)}",
    ]


CHECKS = {"hits": check_hits, "types": check_types, "shape": check_shape}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", choices=[*CHECKS, "all"], default="all")
    parser.add_argument("--monster", default=None, help="audit just this one")
    args = parser.parse_args()

    blocks = emulator_blocks()
    wanted = list(CHECKS) if args.check == "all" else [args.check]
    flagged = 0
    modelled = 0
    unmapped: list[str] = []

    for path in sorted(MONSTERS.glob("*.cs")):
        monster = path.stem
        if args.monster and monster != args.monster:
            continue
        source = path.read_text(encoding="utf-8")
        if "GenerateMoveStateMachine" not in source:
            continue
        if monster in NOT_A_MONSTER:
            continue

        block = blocks.get(ALIASES.get(monster, monster))
        if block is None:
            # LOUD, not skipped. A monster the audit cannot find is a monster it silently
            # reports as clean, which is the one failure mode a staleness check must not
            # have -- and a patch that renames a class is exactly when it would happen.
            unmapped.append(monster)
            continue
        modelled += 1

        rows: list[str] = []
        for name in wanted:
            found = CHECKS[name](monster, source, block)
            rows += [f"[{name}]{row}" for row in found]
        if rows:
            flagged += len(rows)
            print(monster)
            print("\n".join(rows))

    print(f"\n{flagged} flag(s) across {modelled} modelled monsters")

    if unmapped:
        print(
            f"\n{len(unmapped)} monster(s) have a move machine and no emulator block: "
            + ", ".join(unmapped),
        )
        print(
            "Either the emulator does not implement them, or the class was renamed and "
            "ALIASES needs the new name. Until then this audit says nothing about them.",
        )
        raise SystemExit(1)


if __name__ == "__main__":
    main()
