#!/usr/bin/env python3
"""Compare every card keyword the GAME declares against what the generated table emits.

This exists because of how `Retain` and `Sly` were lost. `extract_data.py` gathers
canonical keywords from a tuple -- it read ``("Ethereal", "Exhaust", "Unplayable")`` --
and ``CardDef`` had fields for two more that were not in it. **A keyword the extractor
never emits reads exactly like a card that does not have it**, so nothing failed and
nothing looked wrong, and eleven cards that should have stayed in hand were discarded at
the end of every turn for as long as that tuple was three long.

A field-by-field sweep of all five generated tables found no other never-emitted field.
That is the narrow question. This is the broad one: does every keyword the game has reach
the emulator, at the right COUNT and in the right DIRECTION?

The direction matters and the two are not interchangeable. Twelve cards GAIN Retain when
upgraded, the way fifteen gain Innate; nineteen LOSE Exhaust when upgraded and three lose
Ethereal, and for most of those losing it is the whole benefit of the upgrade. A check
that only asked "is the keyword mentioned in OnUpgrade" would read those two groups as
the same thing and get one of them backwards.

    uv run python scripts/audit_card_keywords.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CARDS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Cards"
TABLE = REPO / "src" / "Sts2Emulator" / "Generated" / "Cards.g.cs"

CANONICAL = re.compile(
    r"CanonicalKeywords\s*=>(.*?)(?=\n\tprotected|\n\tpublic|\n\tprivate|\Z)",
    re.DOTALL,
)
ON_UPGRADE = re.compile(r"OnUpgrade\(\)\s*\{(.*?)\n\t\}", re.DOTALL)

# The placeholder the game falls back to when a card id no longer resolves. It declares
# Exhaust, no run can hold it, and extract_data.py skips it -- so it is excluded here
# rather than left to look like a one-card discrepancy forever.
NOT_A_CARD = {"DeprecatedCard"}

# keyword -> (CardDef field for the canonical declaration, field for the upgrade change,
#             which direction the upgrade goes)
KEYWORDS = {
    "Exhaust": ("Exhaust", "ExhaustRemovedWhenUpgraded", "Remove"),
    "Ethereal": ("Ethereal", "EtherealRemovedWhenUpgraded", "Remove"),
    "Unplayable": ("Unplayable", None, None),
    "Retain": ("Retain", "RetainWhenUpgraded", "Add"),
    "Sly": ("Sly", None, None),
    "Innate": ("Innate", "InnateWhenUpgraded", "Add"),
    "Eternal": ("Eternal", None, None),
}


def declared(keyword: str) -> tuple[int, int, int]:
    """How many cards declare it canonically, add it on upgrade, and remove it."""
    canonical = added = removed = 0
    for path in sorted(CARDS.glob("*.cs")):
        if path.stem in NOT_A_CARD:
            continue
        src = path.read_text(encoding="utf-8")
        body = CANONICAL.search(src)
        if body and f"CardKeyword.{keyword}" in body.group(1):
            canonical += 1
        upgrade = ON_UPGRADE.search(src)
        if not upgrade:
            continue
        for line in upgrade.group(1).splitlines():
            if f"CardKeyword.{keyword}" not in line:
                continue
            if "Add" in line:
                added += 1
            elif "Remove" in line:
                removed += 1
    return canonical, added, removed


def main() -> None:
    if not CARDS.is_dir():
        print(f"decompiled/ not found at {CARDS} -- run scripts/decompile.sh first.")
        raise SystemExit(1)

    table = TABLE.read_text(encoding="utf-8")
    rows = []
    for keyword, (field, upgrade_field, direction) in KEYWORDS.items():
        canonical, added, removed = declared(keyword)
        emitted = len(re.findall(rf"\b{field}: true", table))
        if emitted != canonical:
            rows.append(
                f"  {keyword}: {canonical} card(s) declare it, {emitted} emitted as "
                f"`{field}`",
            )
        if upgrade_field is None:
            # Nothing claims to model an upgrade change for this one -- so if the source
            # has grown one, that is a field the emulator has no home for at all.
            changes = added + removed
            if changes:
                rows.append(
                    f"  {keyword}: {changes} card(s) change it on upgrade and CardDef has "
                    "no field for that",
                )
            continue
        want = added if direction == "Add" else removed
        other = removed if direction == "Add" else added
        got = len(re.findall(rf"\b{upgrade_field}: true", table))
        if got != want:
            rows.append(
                f"  {keyword}: {want} card(s) {direction.lower()} it on upgrade, "
                f"{got} emitted as `{upgrade_field}`",
            )
        if other:
            rows.append(
                f"  {keyword}: {other} card(s) change it on upgrade in the OTHER "
                f"direction, which nothing models",
            )

    if rows:
        print("Card keywords the generated table does not match:\n")
        print("\n".join(rows))
        print(
            "\nAdd the keyword to extract_data.py's flag tuple (and a CardDef field if it "
            "has none), then re-run scripts/extract_data.py.",
        )
        sys.exit(1)

    print(f"All {len(KEYWORDS)} card keywords match the decompiled source.")


if __name__ == "__main__":
    main()
