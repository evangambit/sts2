#!/usr/bin/env python3
"""Find `case` bodies documented for ONE card but shared by several labels.

`CardEffects` stacks case labels over a common body, which is how a dozen plain-damage
cards share four lines. That is fine until someone gives "a card" a body by writing one
under its label -- because the label above it falls through into the same body, and the
edit lands on every card in the stack.

It has happened twice. Veilpiercer's power was added under the last of eight labels and
went to Defile, Reap and Sow with it; Sleight of Flesh's was added under the fifth of ten
and went to Melancholy, Misery, Reaper Form and Sentry Mode. Both were caught by a live
capture rather than by reading, and only because those cards happened to have captures.

The signal is a body whose comment names ONE card -- backticked class names, or the card's
own name -- while the stack above it carries several. A body written for one card and
reached by five is either a mistake or an undocumented claim that they behave alike.

    uv run python scripts/audit_shared_card_bodies.py          # the suspicious stacks
    uv run python scripts/audit_shared_card_bodies.py --all    # every stack of 2+

A worklist, not a verdict: it exits 0. A shared body is often perfectly correct.
"""

from __future__ import annotations

import argparse
import pathlib
import re

SOURCE = pathlib.Path("src/Sts2Emulator/Core/Effects/CardEffects.cs")

LABEL = re.compile(r'^\s*case "(\w+)":\s*$')
COMMENT = re.compile(r"^\s*//\s?(.*)$")


def stacks(lines: list[str]) -> list[tuple[int, list[str], list[str], bool]]:
    """Every run of consecutive `case "X":` labels, with the comment lines under it."""
    found = []
    i = 0
    while i < len(lines):
        if not LABEL.match(lines[i]):
            i += 1
            continue
        start = i
        names = []
        while i < len(lines) and (m := LABEL.match(lines[i])):
            names.append(m.group(1))
            i += 1
        # Every comment in the BODY, not just the ones directly under the labels. The
        # Veilpiercer edit put a statement first and the comment second, and a scan that
        # stopped at the first line of code read that body as undocumented.
        comments = []
        guarded = False
        j = i
        while j < len(lines) and not LABEL.match(lines[j]):
            if c := COMMENT.match(lines[j]):
                comments.append(c.group(1))
            elif "def.Name ==" in lines[j]:
                # A body that dispatches per card with `if (def.Name == ...)` is a shared
                # body ON PURPOSE, and a comment inside one of those branches naming one
                # card is exactly right. Those are most of what this scan would otherwise
                # report.
                guarded = True
            j += 1
        found.append((start + 1, names, comments, guarded))
    return found


def named_cards(comments: list[str], everyone: set[str]) -> set[str]:
    """Card names the comment mentions.

    Both spellings the comments actually use: the class name (`VeilpiercerPower`,
    `HighFive`) and the printed name split into words ("High Five"). A trailing Power or
    Card is dropped, because a comment naming `SicEmPower` is naming Sic Em.

    Every contiguous run of Capitalised words is tried, not the longest one: a single
    greedy pattern swallows "High Five" into "sharing High Five Osty" and finds nothing,
    which is exactly how the first version of this scan missed the bug it was written for.
    Possessives go first for the same reason -- "HighFives" is not a card.
    """
    text = re.sub(r"['\u2019]s\b", "", " ".join(comments))
    words = re.findall(r"[A-Za-z][A-Za-z]*", text)
    hits = set()
    for i in range(len(words)):
        if not words[i][:1].isupper():
            continue
        for j in range(i, len(words)):
            if not words[j][:1].isupper():
                break
            squashed = "".join(words[i : j + 1])
            for candidate in (
                squashed,
                squashed.removesuffix("Power"),
                squashed.removesuffix("Card"),
            ):
                if candidate in everyone:
                    hits.add(candidate)
    return hits


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--all", action="store_true", help="every stack of two or more labels"
    )
    args = ap.parse_args()

    lines = SOURCE.read_text().split("\n")
    everyone = {n for _, names, _, _ in stacks(lines) for n in names}

    suspicious = []
    for line_no, names, comments, guarded in stacks(lines):
        if len(names) < 2 or guarded:
            continue
        named = named_cards(comments, everyone)
        mentioned = named & set(names)
        # The body says something about a specific card and reaches several. Either it
        # names one of its own labels and not the others, or -- the Sleight of Flesh case
        # -- it names a card that is not in this stack at all, which is what a comment
        # written for a card that has since been split out looks like.
        # A body whose comment names NO card at all is the same defect wearing a different
        # hat: the one that found it was written for The Smith and swallowed Foregone
        # Conclusion and Hidden Cache, and it was invisible here only because the comment
        # happened not to say "The Smith". Naming the card a body is for is the convention
        # that makes this scan work, so an unnamed shared body is worth a look too.
        unnamed = bool(comments) and not named
        if (
            args.all
            or unnamed
            or (named and len(mentioned) <= 1 and len(mentioned) < len(names))
        ):
            suspicious.append((line_no, names, named, mentioned, comments))

    for line_no, names, named, mentioned, comments in suspicious:
        only = next(iter(mentioned)) if len(mentioned) == 1 else None
        others = [n for n in names if n != only]
        print(f"{SOURCE}:{line_no}  {len(names)} labels")
        print(f"  comment names:  {', '.join(sorted(named))}")
        print(f"  body reaches:   {', '.join(others)}")
        if comments:
            print(f"  // {comments[0][:88]}")
        print()

    print(f"{len(suspicious)} shared bodies documented for a single card.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
