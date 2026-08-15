#!/usr/bin/env python3
"""Diff the current Generated/*.g.cs files against the previous git commit.

Print a human-readable change report.
"""

import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).parent.parent
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated"

# Patterns to parse entries from generated files
CARD_RE = re.compile(
    r'new CardDef\(Id:\s*(\d+),\s*Name:\s*"([^"]+)",\s*Cost:\s*(\d+),\s*BaseDamage:\s*(\d+),\s*BaseBlock:\s*(\d+)',
)
ENEMY_RE = re.compile(
    r'new EnemyDef\(Id:\s*(\d+),\s*Name:\s*"([^"]+)",\s*MinHp:\s*(\d+),\s*MaxHp:\s*(\d+)',
)
RELIC_RE = re.compile(r'new RelicDef\(Id:\s*(\d+),\s*Name:\s*"([^"]+)"\)')


def git_previous(path: Path) -> str | None:
    """Return the previous committed content of a file, or None if not tracked."""
    rel = path.relative_to(REPO)
    result = subprocess.run(
        ["git", "show", f"HEAD:{rel.as_posix()}"],
        capture_output=True,
        text=True,
        cwd=REPO,
    )
    return result.stdout if result.returncode == 0 else None


def parse_cards(text: str) -> dict[int, dict]:
    return {
        int(m[1]): {
            "name": m[2],
            "cost": int(m[3]),
            "damage": int(m[4]),
            "block": int(m[5]),
        }
        for m in CARD_RE.finditer(text)
    }


def parse_enemies(text: str) -> dict[int, dict]:
    return {
        int(m[1]): {"name": m[2], "min_hp": int(m[3]), "max_hp": int(m[4])}
        for m in ENEMY_RE.finditer(text)
    }


def parse_relics(text: str) -> dict[int, dict]:
    return {int(m[1]): {"name": m[2]} for m in RELIC_RE.finditer(text)}


def diff_dicts(label: str, old: dict, new: dict, field_names: list[str]) -> list[str]:
    lines: list[str] = []
    all_ids = sorted(set(old) | set(new))
    for id_ in all_ids:
        if id_ not in old:
            lines.append(f"[NEW]     {label} id={id_} \"{new[id_]['name']}\"")
        elif id_ not in new:
            lines.append(f"[REMOVED] {label} id={id_} \"{old[id_]['name']}\"")
        else:
            lines.extend(
                (
                    f"[CHANGED] {label} \"{new[id_]['name']}\": {f} "
                    f"{old[id_].get(f)} → {new[id_].get(f)}"
                )
                for f in field_names
                if old[id_].get(f) != new[id_].get(f)
            )
    return lines


def main() -> None:
    changes: list[str] = []
    needs_review: list[str] = []

    for filename, parser, label, fields in [
        ("Cards.g.cs", parse_cards, "Card", ["cost", "damage", "block"]),
        ("Enemies.g.cs", parse_enemies, "Enemy", ["min_hp", "max_hp"]),
        ("Relics.g.cs", parse_relics, "Relic", []),
    ]:
        path = GENERATED / filename
        if not path.exists():
            print(
                f"  {filename} not found — run extract_data.py first.",
                file=sys.stderr,
            )
            continue

        current_text = path.read_text(encoding="utf-8")
        previous_text = git_previous(path) or ""

        current = parser(current_text)
        previous = parser(previous_text)

        diffs = diff_dicts(label, previous, current, fields)
        changes.extend(diffs)

        new_count = sum(1 for line in diffs if line.startswith("[NEW]"))
        removed_count = sum(1 for line in diffs if line.startswith("[REMOVED]"))
        if new_count:
            needs_review.append(f"{new_count} new {label.lower()}(s)")
        if removed_count:
            needs_review.append(f"{removed_count} removed {label.lower()}(s)")

    if not changes:
        print("No data changes detected.")
        return

    print("\n".join(changes))
    print()
    if needs_review:
        print(f"Effects requiring manual review: {', '.join(needs_review)}.")
    else:
        print("All changes are numeric — no manual effect implementation needed.")


if __name__ == "__main__":
    main()
