#!/usr/bin/env python3
"""Generate the list of encounters ``CombatFactory`` can build.

The ``ActOneEncounter`` enum in ``Core/CombatFactory.cs`` is the only registry of
modelled encounters there is: an encounter exists exactly when it has a member there
and a roster in ``CreateEncounter``. Scraping it into ``ImplementedEncounters.g.cs``
lets ``CombatCoverageTests`` fail the build when an encounter is added without a test,
which a hand-maintained list could never do.

Nothing here is ground truth about encounter *behaviour*; it is only the set of names.
Expected rosters, HP and move cycles still come from ``decompiled/`` or a live capture,
never from the emulator.

    python scripts/generate_combat_coverage.py
    python scripts/generate_combat_coverage.py --print-untested
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
FACTORY = REPO / "src" / "Sts2Emulator" / "Core" / "CombatFactory.cs"
TESTS = REPO / "src" / "Sts2Emulator.Tests"
OUT = TESTS / "Combats" / "ImplementedEncounters.g.cs"


def encounters() -> list[str]:
    text = FACTORY.read_text(encoding="utf-8")
    body = re.search(
        r"(?:private|internal) enum ActOneEncounter\s*\{(.*?)\n    \}", text, re.S
    )
    if not body:
        raise SystemExit("ActOneEncounter enum not found — did it get renamed?")

    names = []
    for line in body.group(1).splitlines():
        line = line.strip()
        if not line or line.startswith("//"):
            continue
        name = line.split("=")[0].strip().rstrip(",")
        if name:
            names.append(name)

    # An enum member without a roster would build an empty combat, so it is not
    # "modelled" and should not be demanded of the test suite either.
    rostered = set(re.findall(r"ActOneEncounter\.(\w+)\s*=>", text))
    rostered |= set(re.findall(r"case ActOneEncounter\.(\w+)", text))
    missing = [n for n in names if n not in rostered]
    if missing:
        raise SystemExit(f"Enum members with no roster in CreateEncounter: {missing}")
    return names


def has_suite(name: str) -> bool:
    return (TESTS / "Combats" / f"{name}Tests.cs").exists()


def render(names: list[str]) -> str:
    lines = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_combat_coverage.py to update.",
        "namespace Sts2Emulator.Tests;",
        "",
        "/// <summary>",
        '/// Every encounter <c>CombatFactory</c> can build, which is what "modelled" means',
        "/// for a combat. Consumed by <c>CombatCoverageTests</c>.",
        "/// </summary>",
        "internal static class ImplementedEncounters",
        "{",
        "    public static readonly string[] Names =",
        "    [",
    ]
    lines += [f'        "{name}",' for name in sorted(names)]
    lines += ["    ];", "}", ""]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--print-untested", action="store_true")
    args = parser.parse_args()

    names = encounters()
    if args.print_untested:
        for name in sorted(n for n in names if not has_suite(n)):
            print(name)
        return

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(names), encoding="utf-8")
    tested = sum(1 for n in names if has_suite(n))
    print(
        f"{len(names)} encounters modelled, {tested} with a test suite -> {OUT.relative_to(REPO)}"
    )


if __name__ == "__main__":
    main()
