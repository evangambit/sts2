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
anywhere the emulator speaks about that monster, so it cannot tell a damage number from a hit count or
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


def statement_at(text: str, index: int) -> str:
    """The whole statement or arm the character at `index` belongs to.

    Walks back to the start of that line, then forward until the braces it opened are
    balanced again -- or, for a one-line arm that opens none, to the `;` or `,` that ends
    it. Deliberately generous: over-collecting costs a false MISS at worst, and this
    function exists because under-collecting was costing false FLAGS.
    """
    start = text.rfind("\n", 0, index) + 1
    depth = 0
    opened = False
    for cursor in range(start, len(text)):
        char = text[cursor]
        if char == "{":
            depth += 1
            opened = True
        elif char == "}":
            depth -= 1
            if opened and depth <= 0:
                return text[start : cursor + 1]
        elif char in ";," and depth == 0 and not opened:
            return text[start : cursor + 1]
    return text[start:]


def ai_blocks() -> dict[str, str]:
    """Everything in EnemyAI that speaks about each monster, concatenated.

    Not just the `case KE.X:` arms. The emulator carries a monster's numbers in at least
    four shapes, and for a while this collector saw only the first:

      * `case KE.X:` in SelectIntent or one of the Apply*Intent switches;
      * `if (enemy.DefId == KE.X && ...) { ... }` riders inside the shared attack, defend
        and buff branches, which is where every rider added since the Hive batches lives;
      * switch-expression arms, `KE.X => ...` and `KE.X when ... => ...`, as
        SecondaryIntentFor and BotIntent are written;
      * `if (defId == KE.X)` inside a helper.

    Reading only the case arms reported six monsters as carrying a bare A9 literal while
    the guarded `Ascension.Value(...)` sat in a rider ten lines up -- the same over-report
    the move audit had, for the same reason: a checker that reads code has to be able to
    see all of the code.
    """
    text = ENEMY_AI.read_text(encoding="utf-8")
    blocks: dict[str, str] = {}

    marks = [(m.start(), m.group(1)) for m in re.finditer(r"case KE\.(\w+):", text)]
    for index, (start, name) in enumerate(marks):
        end = marks[index + 1][0] if index + 1 < len(marks) else len(text)
        blocks[name] = blocks.get(name, "") + text[start:end]

    for match in re.finditer(r"\bKE\.(\w+)\b", text):
        name = match.group(1)
        blocks[name] = blocks.get(name, "") + "\n" + statement_at(text, match.start())

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
