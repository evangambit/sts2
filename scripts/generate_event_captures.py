#!/usr/bin/env python3
"""Generate the C# event-outcome capture tests from the committed live fixtures.

Expected values here come from the **game**. `tests/fixtures/events/` holds 74
captures of a real run choosing a real option -- the player's HP, gold, deck and
relics before and after -- and until now nothing asserted against them. Every
event test in the suite derived its expectations from the decompiled source
instead. That is a legitimate ground truth for the *rules*, and it caught real
bugs, but it cannot catch a transcription error that is self-consistent between
the reading and the test. These captures can, because only the game side moves.

Re-capturing a fixture and letting the assertions follow re-reads ground truth;
it cannot rubber-stamp an emulator regression, which is the same property
`scripts/generate_capture_tests.py` documents.

Two things this deliberately does NOT assert:

* The offered cards behind a `card_select` screen. The capture does not record
  them, so only the screen itself is checked.
* The page shape of a capture taken mid-transition. An event option is `async`;
  where it awaits before calling `SetEventState`, the snapshot can land after the
  effect but before the new page renders, and the capture then shows the old
  options with `was_chosen` set. Asserting that would force the emulator to model
  a rendering race. Such captures are detected by `was_chosen`, never by name,
  and their player-state deltas are still asserted in full.

    python scripts/generate_event_captures.py
    python scripts/generate_event_captures.py --check
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
FIXTURES = REPO / "tests" / "fixtures" / "events"
ENGINE = REPO / "src" / "Sts2Emulator" / "Core" / "Run" / "RunEngine.cs"
GENERATED = REPO / "src" / "Sts2Emulator" / "Generated"
OUT = REPO / "src" / "Sts2Emulator.Tests" / "Events" / "EventCaptures.g.cs"

# The game's own state_type for the screen the option lands on.
PHASE = {
    "event": "Event",
    "card_select": "TransformSelect",
    "rewards": "RelicReward",
    "crystal_sphere": "CrystalSphere",
    "battle": "Combat",
    # An event that starts a fight without leaving the event -- Battleworn Dummy's three
    # settings, The Lantern Key's knight. The mod reports the room type, and for these the
    # room is still a Monster room even though no map node was taken.
    "monster": "Combat",
    "map": "Map",
    "shop": "Shop",
}


def modelled_events() -> set[str]:
    """Return the event names `RunEngine.StepEvent` can actually run.

    Raises:
        SystemExit: if StepEvent cannot be found.

    """
    text = ENGINE.read_text(encoding="utf-8")
    start = text.find("private int StepEvent")
    if start < 0:
        raise SystemExit("StepEvent not found in RunEngine.cs — did it get renamed?")
    return set(re.findall(r"case RunConstants\.Event(\w+)", text[start:]))


def entry_ids(filename: str, pattern: str) -> dict[str, int]:
    """Map a model's ModelId.Entry -- which is the game's own id -- to our id.

    Raises:
        SystemExit: if nothing parses out of the generated file.

    """
    text = (GENERATED / filename).read_text(encoding="utf-8")
    found = {entry: int(i) for i, entry in re.findall(pattern, text)}
    if not found:
        raise SystemExit(f"No definitions parsed out of {filename}")
    return found


def deck_of(player: dict) -> list[tuple[str, bool]]:
    return [(c["id"], bool(c.get("is_upgraded"))) for c in (player.get("deck") or [])]


def relics_of(player: dict) -> list[str]:
    return [r["id"] for r in (player.get("relics") or [])]


def mid_transition(after: dict) -> bool:
    """Report whether the snapshot caught the old page still on screen.

    The game marks the option the player just picked with `was_chosen`. A page
    that has actually advanced shows the NEW options, none of which can be the
    one just chosen -- so `was_chosen` in the after-state means the await had not
    completed when the capture was taken.
    """
    options = (after.get("event") or {}).get("options") or []
    return any(option.get("was_chosen") for option in options)


def is_finished(after: dict) -> bool:
    """Report whether the event is over and showing only its result text."""
    options = (after.get("event") or {}).get("options") or []
    return len(options) == 1 and bool(options[0].get("is_proceed"))


def cards_literal(deck: list[tuple[str, bool]], cards: dict[str, int]) -> str:
    return ", ".join(f"({cards[entry]}, {str(up).lower()})" for entry, up in deck)


def render_case(
    name: str,
    option: int,
    fixture: dict,
    cards: dict[str, int],
    relics: dict[str, int],
) -> str:
    before, after = fixture["before"]["player"], fixture["after"]["player"]
    state = fixture.get("after_state_type") or fixture["after"].get("state_type")
    phase = PHASE.get(state)
    if phase is None:
        raise SystemExit(f"{name}-opt{option}: unmapped after_state_type {state!r}")

    title = (fixture.get("chosen_title") or f"option {option}").replace('"', "'")
    game = fixture.get("game", {})
    lines = [
        "    /// <summary>",
        (
            f"    /// {name}, {title!r} -- captured from "
            f"{game.get('release', '?')} (build {game.get('steam_buildid', '?')})."
        ),
        "    /// </summary>",
        "    [Fact]",
        f"    public void {name}_Option{option}()",
        "    {",
        f"        var engine = Open(RunConstants.Event{name});",
        "",
        "        // The capture's own starting state. If this drifts, the after-state",
        "        // comparison below is measuring two different runs.",
        f"        AssertPlayer(engine, {before['hp']}, {before['max_hp']}, {before['gold']},",
        f"            [{cards_literal(deck_of(before), cards)}],",
        f"            [{', '.join(str(relics[r]) for r in relics_of(before))}]);",
        "",
        f"        Assert.Equal(0, engine.Step({option}, -1, out _, out _, out _));",
        "",
        f"        AssertPlayer(engine, {after['hp']}, {after['max_hp']}, {after['gold']},",
        f"            [{cards_literal(deck_of(after), cards)}],",
        f"            [{', '.join(str(relics[r]) for r in relics_of(after))}]);",
        f"        Assert.Equal(RunPhase.{phase}, engine.State.Phase);",
    ]

    if state == "event" and not mid_transition(fixture["after"]):
        if is_finished(fixture["after"]):
            lines.append(
                "        Assert.Equal(RunConstants.EventResultPending, engine.State.EventId);",
            )
        else:
            options = (fixture["after"].get("event") or {}).get("options") or []
            titles = ", ".join(repr(o["title"]) for o in options)
            lines += [
                f"        // The game answered with a page of {len(options)}: {titles}.",
                f"        Assert.Equal({len(options)}, OfferedCount(engine));",
                "        Assert.NotEqual(RunConstants.EventResultPending, engine.State.EventId);",
            ]
    elif state == "event":
        lines.append(
            "        // Page shape not asserted: this capture landed mid-transition.",
        )

    lines.append("    }")
    return "\n".join(lines)


def render(cases: list[str], skipped: list[str]) -> str:
    note = "\n".join(
        f"/// <item><description>{s}</description></item>" for s in skipped
    )
    return f"""// <auto-generated>
//     Generated by scripts/generate_event_captures.py. Do not edit by hand.
// </auto-generated>
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Event outcomes compared against captures of the live game.
///
/// <para>
/// Every other event suite derives its expectations from the decompiled source.
/// That is ground truth for the rules, but a transcription error that is
/// self-consistent between the reading and the test survives it. These
/// assertions come from a real run of the real game and cannot.
/// </para>
/// <para>
/// Regenerate with <c>python scripts/generate_event_captures.py</c> after
/// re-capturing a fixture.
/// </para>
{note}
/// </summary>
public class EventCaptures
{{
    /// <summary>
    /// The state every capture was taken in: seed ABCDEF, A8, floor 1.
    ///
    /// <para>
    /// This enters through <c>BeginEvent</c> rather than assigning <c>EventId</c>,
    /// because entering an event runs its <c>CalculateVars</c> and several events draw
    /// from their own stream there. Assigning the id leaves those draws unspent and
    /// every later draw in the event lands one position early.
    /// </para>
    /// </summary>
    private static RunEngine Open(int eventId)
    {{
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        RunNonCombatEffects.BeginEvent(engine.State, eventId);
        return engine;
    }}

    private static int OfferedCount(RunEngine engine)
    {{
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Count(index => mask[index] != 0);
    }}

    private static void AssertPlayer(
        RunEngine engine,
        int hp,
        int maxHp,
        int gold,
        (int Card, bool Upgraded)[] deck,
        int[] relics
    )
    {{
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(maxHp, engine.State.PlayerMaxHp);
        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(
            deck,
            engine.State.Deck.Select(card => (card.DefId, card.Upgraded)).ToArray()
        );
        Assert.Equal(relics, engine.State.Relics.Select(relic => relic.DefId).ToArray());
    }}

{chr(10).join(cases)}
}}
"""


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    cards = entry_ids(
        "Cards.g.cs",
        r'new CardDef\(Id: (\d+), Name: "[^"]*", Entry: "([^"]*)"',
    )
    relics = entry_ids(
        "Relics.g.cs",
        r'new RelicDef\(Id: (\d+), Name: "[^"]*", Entry: "([^"]*)"',
    )
    known = modelled_events()

    cases: list[str] = []
    covered: set[str] = set()
    skipped: list[str] = []
    unmodelled: list[str] = []

    for path in sorted(FIXTURES.glob("*-opt[0-9].json")):
        name, _, tail = path.stem.partition("-opt")
        option = int(tail)
        if name not in known:
            unmodelled.append(path.stem)
            continue
        fixture = json.loads(path.read_text(encoding="utf-8"))
        if "after" not in fixture:
            continue
        if mid_transition(fixture["after"]):
            skipped.append(f"{path.stem}: page shape not asserted (mid-transition)")
        cases.append(render_case(name, option, fixture, cards, relics))
        covered.add(name)

    if unmodelled:
        raise SystemExit(
            "Captures exist for events StepEvent cannot run:\n  "
            + "\n  ".join(unmodelled),
        )

    if args.check:
        print(f"{len(cases)} capture tests over {len(covered)} events")
        for line in skipped:
            print(f"  skipped page shape: {line}")
        return

    OUT.write_text(render(cases, skipped), encoding="utf-8")
    print(
        f"{len(cases)} capture tests over {len(covered)} events "
        f"-> {OUT.relative_to(REPO)}",
    )


if __name__ == "__main__":
    main()
