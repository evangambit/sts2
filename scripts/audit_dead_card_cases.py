#!/usr/bin/env python3
"""Find card `case` labels that nothing can reach.

`CardEffects` dispatches a card play through several switches in sequence. `ApplyCore`
takes the card by id; ids it does not handle fall through to
`ApplyGeneratedCardApproximation`, which tries `ApplyDefectCard`, `ApplyNecrobinderCard`,
`ApplyRegentCard` and `ApplyMiscGeneratedCard` in turn -- each returning true when it has
handled the card -- and only then reaches its own by-name switch.

So a card with a real body in an EARLIER switch and a second `case` in a later one has a
body nothing runs. That is not merely untidy. Five cards in the tested-but-unread sweep
were found wrong in a copy that could never execute: Necromastery and Orbit were being
maintained in the dead one, and Times Up, Unleash and Soul Storm carried guessed numbers
there while their real bodies sat elsewhere. A scan looking for a card's implementation
finds the dead copy just as readily as the live one.

    uv run python scripts/audit_dead_card_cases.py          # the count and the worst offenders
    uv run python scripts/audit_dead_card_cases.py --all    # every dead label

This is a worklist, not a verdict: it exits 0 and does not fail a build. Deleting a dead
label is safe -- nothing reaches it -- but each one is worth a glance first, because a
dead body occasionally holds the BETTER reading of the card and the live one is the
stale copy.
"""

from __future__ import annotations

import argparse
import collections
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SOURCE = REPO / "src" / "Sts2Emulator" / "Core" / "Effects" / "CardEffects.cs"

# Earlier in this list wins: each returns before the next is consulted.
DISPATCH_ORDER = [
    "ApplyCore",
    "ApplyDefectCard",
    "ApplyNecrobinderCard",
    "ApplyRegentCard",
    "ApplyMiscGeneratedCard",
    "ApplyGeneratedCardApproximation",
]

CASE = re.compile(r'case (?:IC|SI|CL|ST|NB|RG)\.(\w+):|case "(\w+)":')
METHOD = re.compile(r"\s*(?:private|internal|public) static \w+ (\w+)\(")


def dead_labels() -> list[tuple[str, str, int, str, int]]:
    """Return (card, live_fn, live_line, dead_fn, dead_line) for each unreachable case."""
    lines = SOURCE.read_text(encoding="utf-8").split("\n")
    fn = None
    seen: dict[str, list[tuple[str, int]]] = collections.defaultdict(list)
    for i, line in enumerate(lines):
        if m := METHOD.match(line):
            fn = m.group(1)
        if (g := CASE.search(line)) and fn in DISPATCH_ORDER:
            seen[g.group(1) or g.group(2)].append((fn, i + 1))

    out = []
    for card, locs in sorted(seen.items()):
        ranked = sorted(locs, key=lambda p: DISPATCH_ORDER.index(p[0]))
        live_fn, live_line = ranked[0]
        for dead_fn, dead_line in ranked[1:]:
            if dead_fn != live_fn:
                out.append((card, live_fn, live_line, dead_fn, dead_line))
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true", help="list every dead label")
    args = parser.parse_args()

    dead = dead_labels()
    print(f"{len(dead)} card case(s) sit behind an earlier switch and cannot be reached")
    if not dead:
        return

    by_pair = collections.Counter((live, dead_fn) for _, live, _, dead_fn, _ in dead)
    for (live, dead_fn), n in by_pair.most_common():
        print(f"  {n:4}  handled in {live}, dead copy in {dead_fn}")

    shown = dead if args.all else dead[:12]
    print("\n" + ("every dead label:" if args.all else "first twelve:"))
    for card, live_fn, live_line, dead_fn, dead_line in shown:
        print(f"  {card:22} live {live_fn}:{live_line}   dead {dead_fn}:{dead_line}")
    if not args.all and len(dead) > len(shown):
        print(f"  ... and {len(dead) - len(shown)} more (--all)")


if __name__ == "__main__":
    main()
