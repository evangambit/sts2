#!/usr/bin/env python3
"""Generate the list of events ``RunEngine`` can run.

``StepEvent``'s switch in ``Core/Run/RunEngine.cs`` is the only registry of modelled
events there is: an event exists exactly when it has a case there that answers the
player's choice. Scraping it into ``ImplementedEvents.g.cs`` lets ``EventCoverageTests``
fail the build when an event is added without a test, which a hand-maintained list
could never do.

This is the event-side twin of ``generate_combat_coverage.py`` and works the same way.
An ``EventX`` constant in ``RunConstants`` is not enough on its own -- ids exist for
events the emulator only knows by name -- so the switch is what counts.

Nothing here is ground truth about event *behaviour*; it is only the set of names.
Expected options and outcomes still come from ``decompiled/`` or a live capture (see
``scripts/capture_event.py``), never from the emulator.

    python scripts/generate_event_coverage.py
    python scripts/generate_event_coverage.py --print-untested
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ENGINE = REPO / "src" / "Sts2Emulator" / "Core" / "Run" / "RunEngine.cs"
TESTS = REPO / "src" / "Sts2Emulator.Tests"
OUT = TESTS / "Events" / "ImplementedEvents.g.cs"


def step_event_body(text: str) -> str:
    start = text.find("private int StepEvent")
    if start < 0:
        raise SystemExit("StepEvent not found in RunEngine.cs — did it get renamed?")
    rest = text[start:]
    # The next method declaration at the same indentation ends the body.
    end = rest.find("\n    private ", 1)
    return rest if end < 0 else rest[:end]


def events(text: str) -> list[str]:
    names = sorted(
        set(re.findall(r"case RunConstants\.Event(\w+):", step_event_body(text))),
    )
    if not names:
        raise SystemExit(
            "No event cases found in StepEvent — did the switch change shape?",
        )
    return names


def has_suite(name: str) -> bool:
    return (TESTS / "Events" / f"{name}Tests.cs").exists()


def render(names: list[str]) -> str:
    lines = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_event_coverage.py to update.",
        "namespace Sts2Emulator.Tests;",
        "",
        "/// <summary>",
        '/// Every event <c>RunEngine.StepEvent</c> can run, which is what "modelled" means',
        "/// for an event. Consumed by <c>EventCoverageTests</c>.",
        "/// </summary>",
        "internal static class ImplementedEvents",
        "{",
        "    public static readonly string[] Names =",
        "    [",
    ]
    lines += [f'        "{name}",' for name in names]
    lines += ["    ];", "}", ""]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--print-untested", action="store_true")
    args = parser.parse_args()

    names = events(ENGINE.read_text(encoding="utf-8"))
    if args.print_untested:
        for name in (n for n in names if not has_suite(n)):
            print(name)
        return

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(names), encoding="utf-8")
    tested = sum(1 for n in names if has_suite(n))
    print(
        f"{len(names)} events modelled, {tested} with a test suite "
        f"-> {OUT.relative_to(REPO)}",
    )


if __name__ == "__main__":
    main()
