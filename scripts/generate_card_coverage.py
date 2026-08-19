#!/usr/bin/env python3
"""Generate the list of cards that ``CardEffects.Apply`` implements.

The switch in ``Core/Effects/CardEffects.cs`` is the only registry of implemented
cards there is: a card is implemented exactly when it has a ``case`` label there.
Scraping it into ``ImplementedCards.g.cs`` lets ``CardCoverageTests`` fail the build
when a card gains an implementation without gaining a test, which a hand-maintained
list could never do -- it would go stale the first time someone forgot to append.

Nothing here is ground truth about card *behavior*; it is only the set of names.
Expected values still come from ``decompiled/`` or a live capture, never from the
emulator.

    python scripts/generate_card_coverage.py
    python scripts/generate_card_coverage.py --print-untested
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CARD_EFFECTS = REPO / "src" / "Sts2Emulator" / "Core" / "Effects" / "CardEffects.cs"
ID_MAP = REPO / "data" / "id_map.json"
TESTS = REPO / "src" / "Sts2Emulator.Tests"
OUT = TESTS / "Cards" / "ImplementedCards.g.cs"

# The id classes in Generated/CardIds.g.cs, and the folder each one's tests live in.
FOLDERS = {
    "IC": "Ironclad",
    "SI": "Silent",
    "CL": "Colorless",
    "AN": "Ancient",
    "ST": "StatusCurse",
}

# Two shapes implement a card, and only counting the first is how three whole
# characters stayed invisible: `Apply` switches on id constants for Ironclad, Silent,
# Colourless and friends, while Defect, Necrobinder and Regent are handled by
# ApplyDefectCard and its siblings, which switch on def.Name.
CONST_CASE_RE = re.compile(rf"case ({'|'.join(FOLDERS)})\.(\w+)\s*:")
NAME_CASE_RE = re.compile(r'case "(\w+)"\s*:')


def implemented_cards() -> dict[str, str]:
    """Map card name -> id class (or "" for name-cased cards), for every implemented card.

    A name-cased label only counts when it names a real card: the same switch shape is
    used for powers, so `case "ReanimatePower"` would otherwise be reported as a card
    nobody had tested.
    """
    source = CARD_EFFECTS.read_text(encoding="utf-8")
    cards = {name: cls for cls, name in CONST_CASE_RE.findall(source)}

    known = set(json.loads(ID_MAP.read_text(encoding="utf-8"))["cards"])
    for name in NAME_CASE_RE.findall(source):
        if name in known and name not in cards:
            cards[name] = ""
    return cards


def test_suites() -> set[str]:
    """Card names that already have a ``<Name>Tests`` class somewhere in the suite."""
    suites = set()
    for path in TESTS.rglob("*.cs"):
        suites.update(
            match.group(1)
            for match in re.finditer(
                r"public class (\w+)Tests\b",
                path.read_text(encoding="utf-8"),
            )
        )
    return suites


def render(cards: dict[str, str]) -> str:
    lines = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_card_coverage.py to update.",
        "namespace Sts2Emulator.Tests;",
        "",
        "/// <summary>",
        "/// Every card with an explicit <c>case</c> label in <c>CardEffects.Apply</c>, which is",
        '/// what "implemented" means for a card. Consumed by <c>CardCoverageTests</c>.',
        "/// </summary>",
        "internal static class ImplementedCards",
        "{",
        "    public static readonly string[] Names =",
        "    [",
    ]
    lines += [f'        "{name}",' for name in sorted(cards)]
    lines += [
        "    ];",
        "}",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--print-untested",
        action="store_true",
        help="list implemented cards with no <Name>Tests class instead of writing the file",
    )
    args = parser.parse_args()

    cards = implemented_cards()

    if args.print_untested:
        untested = sorted(set(cards) - test_suites())
        for name in untested:
            print(f'        "{name}",')
        print(
            f"\n{len(untested)} of {len(cards)} implemented cards have no test suite.",
        )
        return

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(cards), encoding="utf-8")
    print(f"Wrote {len(cards)} card names to {OUT.relative_to(REPO)}")


if __name__ == "__main__":
    main()
