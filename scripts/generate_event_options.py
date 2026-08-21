#!/usr/bin/env python3
"""Generate how many options each event offers, from the game's own event models.

An event's option list is a fixed-size array in ``GenerateInitialOptions`` -- an option
the run cannot take is a *different* EventOption in the same slot with a null action, so
the count does not move with run state. That makes the count pure data, and pure data is
worth extracting rather than hand-copying: the emulator's action mask otherwise falls
back to offering a fixed 0..3 for any event it has no bespoke case for, which lets an
agent choose a third option at an event that only has two.

Events whose options are built into a ``List<EventOption>`` are state-dependent and are
deliberately left out: their count cannot be read off the source, so they need a bespoke
case in ``WriteEventActionMask`` and are listed here only so the omission is visible.

Counts are cross-checked against the live captures in ``tests/fixtures/events`` when
those exist -- the game is the arbiter, the decompile is just the cheaper reader.

    python scripts/generate_event_options.py
    python scripts/generate_event_options.py --check
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
EVENTS = REPO / "decompiled" / "MegaCrit.Sts2.Core.Models.Events"
CONSTANTS = REPO / "src" / "Sts2Emulator" / "Core" / "Run" / "RunConstants.cs"
FIXTURES = REPO / "tests" / "fixtures" / "events"
OUT = REPO / "src" / "Sts2Emulator" / "Generated" / "EventOptions.g.cs"


def event_ids() -> dict[str, int]:
    text = CONSTANTS.read_text(encoding="utf-8")
    ids = {
        name: int(value)
        for name, value in re.findall(r"public const int Event(\w+) = (-?\d+);", text)
    }
    return {name: value for name, value in ids.items() if value > 0}


def option_shape(path: Path) -> tuple[int | None, bool]:
    """Return (fixed option count or None, whether any option has a locked variant)."""
    text = path.read_text(encoding="utf-8")
    method = re.search(r"GenerateInitialOptions\(\).*?\n\t\}", text, re.DOTALL)
    if method is None:
        return None, False
    body = method.group(0)
    sizes = re.findall(r"EventOption\[(\d+)\]", body)
    # A List<EventOption> is assembled conditionally, so its length is run state, not
    # a property of the event.
    if "List<EventOption>" in body or not sizes:
        return None, "_LOCKED" in body
    return int(sizes[0]), "_LOCKED" in body


def live_counts() -> dict[str, int]:
    counts = {}
    for path in sorted(FIXTURES.glob("*-options.json")):
        fixture = json.loads(path.read_text(encoding="utf-8"))
        counts[fixture["event"]] = len(fixture["options"])
    return counts


def collect() -> tuple[dict[str, tuple[int, int, bool]], list[str], list[str]]:
    ids = event_ids()
    live = live_counts()
    fixed: dict[str, tuple[int, int, bool]] = {}
    dynamic: list[str] = []
    disagreements: list[str] = []
    for name, event_id in sorted(ids.items()):
        path = EVENTS / f"{name}.cs"
        if not path.exists():
            continue
        count, has_locked = option_shape(path)
        if count is None:
            dynamic.append(name)
            continue
        if name in live and live[name] != count:
            disagreements.append(
                f"{name}: source says {count} options, the game showed {live[name]}",
            )
            continue
        fixed[name] = (event_id, count, has_locked)
    return fixed, dynamic, disagreements


def render(fixed: dict[str, tuple[int, int, bool]], dynamic: list[str]) -> str:
    lines = [
        "// AUTO-GENERATED — do not edit. Re-run scripts/generate_event_options.py to update.",
        "namespace Sts2Emulator.GeneratedData;",
        "",
        "/// <summary>",
        "/// How many options each event offers, read off the fixed-size EventOption array",
        "/// in the game's own GenerateInitialOptions. An option the run cannot take still",
        "/// occupies its slot -- the game swaps in a locked variant rather than dropping it",
        "/// -- so this count does not move with run state.",
        "///",
        "/// Whether an option is *takeable* is a separate question, and one this table says",
        "/// nothing about: that lives in RunEngine.WriteEventActionMask, per event.",
        "///",
        "/// Events missing here build their options into a List&lt;EventOption&gt;, so their",
        "/// count is run state and cannot be read off the source:",
        "/// " + ", ".join(dynamic) + ".",
        "/// </summary>",
        "internal static class EventOptions",
        "{",
        "    /// <summary>Option count by event id, or 0 when the event is not in the table.</summary>",
        "    public static int CountFor(int eventId) =>",
        "        eventId switch",
        "        {",
    ]
    for name, (event_id, count, has_locked) in sorted(
        fixed.items(),
        key=lambda item: item[1][0],
    ):
        gated = "  // has a locked variant" if has_locked else ""
        lines.append(f"            {event_id} => {count},{gated}   // {name}")
    lines += [
        "            _ => 0,",
        "        };",
        "}",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    fixed, dynamic, disagreements = collect()
    if disagreements:
        raise SystemExit(
            "Decompiled option counts disagree with the live captures:\n  "
            + "\n  ".join(disagreements),
        )

    if args.check:
        print(f"{len(fixed)} fixed-size events, {len(dynamic)} dynamic: {dynamic}")
        return

    OUT.write_text(render(fixed, dynamic), encoding="utf-8")
    print(
        f"{len(fixed)} events with a fixed option count "
        f"({len(dynamic)} dynamic, left to bespoke cases) -> {OUT.relative_to(REPO)}",
    )


if __name__ == "__main__":
    main()
