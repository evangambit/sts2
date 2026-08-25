#!/usr/bin/env python3
"""Flag EnemyAI intents that may be carrying a monster's A9 value at A8.

Monster numbers reach the game through
``AscensionHelper.GetValueIfAscension(level, high, low)``, and the enum's ordinal IS the
level: ``ToughEnemies = 8``, ``DeadlyEnemies = 9``. The emulator models A8, where the
Tough branch is live and the DEADLY branch is not -- so a Deadly value transcribed as a
bare literal is one to two points high, every turn, for that monster's whole fight. HP
matched while attacks did not on thirteen of the first sixteen captures, which is E11.

``CombatFactory`` was swept for this years ago and ``EnemyAI.SelectIntent`` never was,
and SelectIntent is the one that matters: ``ChooseIntents`` overwrites every opening
intent right after the roster is built, so the literals in CombatFactory are placeholders.

**This is a worklist, not a verdict.** It flags a bare occurrence of the high value
anywhere in a monster's case block, so it cannot tell a damage number from a hit count or
from an unrelated constant -- Exoskeleton's flagged 4 turned out to be the A9 REPEAT
count standing where the damage should be, which is a different bug than the one flagged.
Read the move state machine before changing a line. Nor does an absent flag mean correct:
a monster whose two branches differ by one may already read the low value by luck.

    uv run python scripts/audit_ascension_literals.py
    uv run python scripts/audit_ascension_literals.py --monster Myte
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MONSTERS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Monsters"
ENEMY_AI = REPO / "src" / "Sts2Emulator" / "Core" / "EnemyAI.cs"

DEADLY = re.compile(
    r"\b(\w+)\s*=>\s*AscensionHelper\.GetValueIfAscension\(\s*"
    r"AscensionLevel\.DeadlyEnemies,\s*(\d+),\s*(\d+)\)",
)


def deadly_pairs() -> dict[str, list[tuple[str, int, int]]]:
    """Collect every monster's DeadlyEnemies (high, low) pairs, by property name."""
    pairs: dict[str, list[tuple[str, int, int]]] = {}
    for path in sorted(MONSTERS.glob("*.cs")):
        found = [
            (name, int(high), int(low))
            for name, high, low in DEADLY.findall(path.read_text(encoding="utf-8"))
            if high != low
        ]
        if found:
            pairs[path.stem] = found
    return pairs


def ai_blocks() -> dict[str, str]:
    """Split EnemyAI into per-monster case blocks, concatenating a monster's several."""
    text = ENEMY_AI.read_text(encoding="utf-8")
    marks = [(m.start(), m.group(1)) for m in re.finditer(r"case KE\.(\w+):", text)]
    blocks: dict[str, str] = {}
    for index, (start, name) in enumerate(marks):
        end = marks[index + 1][0] if index + 1 < len(marks) else len(text)
        blocks[name] = blocks.get(name, "") + text[start:end]
    return blocks


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--monster", default=None, help="audit just this one")
    args = parser.parse_args()

    blocks = ai_blocks()
    flagged = 0
    modelled = 0
    for monster, pairs in sorted(deadly_pairs().items()):
        if args.monster and monster != args.monster:
            continue
        body = blocks.get(monster)
        if body is None:
            continue
        modelled += 1
        rows = []
        for name, high, low in pairs:
            # Already guarded: the pair appears together, as Ascension.Value's arguments.
            if re.search(rf"{high},\s*{low}\b", body):
                continue
            if re.search(rf"(?<![\w.]){high}(?![\w])", body):
                rows.append(f"    {name}: {high} at A9, {low} at A8")
        if rows:
            flagged += len(rows)
            print(monster)
            print("\n".join(rows))
    print(f"\n{flagged} suspect literal(s) across {modelled} modelled monsters")


if __name__ == "__main__":
    main()
