#!/usr/bin/env python3
r"""Capture what the *game* does at one event, as a committed fixture.

Events are the layer with no per-element tests at all, and the reason was tooling: a
card can be staged into hand and an encounter can be started on demand, but until the
mod grew `debug_start_event` there was no way to reach a chosen event, so its expected
behaviour could only come from whatever a run happened to wander into.

    python scripts/capture_event.py --event SelfHelpBook
    python scripts/capture_event.py --event SelfHelpBook --choose 0
    python scripts/capture_event.py --event Wellspring --floor 6 --gold 200

Two things are worth capturing and they are different. Without `--choose` the fixture
records only what the event *offers* -- which options, in what order, which are locked
-- because that is state-dependent and is where an emulator most easily drifts: most
events hide or lock options the run cannot afford. With `--choose` it also records what
that option *did* to hp, gold, deck and relics.

The run state the event was entered in is recorded alongside, because it decides both:
`EventModel` seeds its own Rng from the run seed, the current floor and a hash of the
event's id, so the same event at a different floor is a different roll.

Needs the game running with STS2MCP, and a mod new enough to have `debug_start_event`
(see FORK_NOTES.md in the STS2MCP fork).
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import time
from pathlib import Path
from types import ModuleType
from typing import Any

REPO = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO / "scripts"))

import start_real_game_run  # noqa: E402
import trace_real_game  # noqa: E402

FIXTURES = REPO / "tests" / "fixtures" / "events"
DEFAULT_SEED = "ABCDEF"


def _load(name: str) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, REPO / "scripts" / f"{name}.py")
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


capture_card = _load("capture_card")
capture_sweep = _load("capture_sweep")


def enter_event(base_url: str, event: str, timeout: float = 20.0) -> dict[str, Any]:
    """Jump the run into the named event's room and wait for it to open.

    Raises:
        RuntimeError: if the mod rejects the action or the event never opens.

    """
    # What is on screen before the jump, so the wait can insist on seeing something
    # else. Without this a stale option list that simply has not changed yet reads as
    # settled, and the capture commits the previous event's options under this event's
    # name -- which is how Tea Master came back holding Tablet of Truth's.
    #
    # Tolerant of a failing read on purpose: at least one event (Fake Merchant) makes
    # the mod's state endpoint throw, and a batch that cannot read the state it is
    # leaving must still be able to jump out of it. Losing the comparison costs this one
    # capture its staleness check; refusing to post would strand every capture after it.
    try:
        was_showing = offered_options(start_real_game_run.get_state(base_url))
    except Exception:  # noqa: BLE001 - any read failure means "nothing to compare with"
        was_showing = None

    result = trace_real_game.post_action(
        base_url,
        {"action": "debug_start_event", "event": event},
    )
    if result.get("status") != "ok":
        raise RuntimeError(
            f"debug_start_event failed: {result}. An older STS2MCP build has no "
            "debug_start_event; rebuild and redeploy the mod.",
        )

    # Two traps here, both of them "the state is already what I am waiting for".
    # Waiting for "an event with options" comes true immediately, because a fresh run is
    # already sitting on Neow -- so the event has to be matched by name. And matching by
    # name alone still lands mid-transition: the id flips to the new event while the
    # option list is still the old one, which is how a Self-Help Book capture came back
    # holding Neow's three relics. So the readout also has to hold still.
    deadline = time.monotonic() + timeout
    previous: list[Any] | None = None
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        opened = state.get("event") or {}
        options = offered_options(state)
        settled = options and options == previous
        previous = options
        if (
            state.get("state_type") == "event"
            and same_event(opened.get("event_id"), event)
            and settled
            and (was_showing is None or options != was_showing)
        ):
            return state
        time.sleep(0.25)

    raise RuntimeError(f"{event} never opened within {timeout:.0f}s")


def same_event(event_id: str | None, requested: str) -> bool:
    """Whether the mod's SCREAMING_SNAKE id names the requested class."""
    return bool(event_id) and str(event_id).replace("_", "").casefold() == (
        requested.replace("_", "").casefold()
    )


def offered_options(state: dict[str, Any]) -> list[dict[str, Any]]:
    """Return the options as the game presents them, in the order the agent sees."""
    return [
        {
            "index": option.get("index"),
            "title": option.get("title"),
            "description": option.get("description"),
            "is_locked": option.get("is_locked"),
            "is_proceed": option.get("is_proceed"),
            "keywords": [k.get("name") for k in option.get("keywords") or []],
        }
        for option in (state.get("event") or {}).get("options") or []
    ]


def choose(base_url: str, index: int) -> dict[str, Any]:
    """Take one option and return the state it produced.

    Raises:
        RuntimeError: if the game rejects the choice.

    """
    result = trace_real_game.post_action(
        base_url,
        {"action": "choose_event_option", "index": index},
    )
    if result.get("status") != "ok":
        raise RuntimeError(f"choose_event_option failed: {result}")

    return trace_real_game.wait_for_state(base_url, 0.5)


def capture(
    base_url: str,
    event: str,
    seed: str,
    ascension: int,
    option: int | None,
    reuse_run: bool,
) -> dict[str, Any]:
    if not reuse_run:
        capture_card.wait_for_menu_options(base_url)
        capture_sweep.abandon_any_run(base_url)
        start_real_game_run.start_seeded_run(
            base_url,
            seed,
            "IRONCLAD",
            abandon_existing=False,
            ascension=ascension,
        )

    before_state = enter_event(base_url, event)
    before = trace_real_game.summarize_state(before_state)
    options = offered_options(before_state)

    fixture: dict[str, Any] = {
        "_comment": (
            "Captured from the live game by scripts/capture_event.py. Expected values "
            "here are the GAME's, never the emulator's; re-capturing re-reads ground "
            "truth and cannot rubber-stamp an emulator regression."
        ),
        "event": event,
        # The mod's own id for what opened. Recorded so a capture that landed on the
        # wrong event is obvious in the fixture rather than only in its contents.
        "event_id": (before_state.get("event") or {}).get("event_id"),
        "seed": seed,
        "ascension": ascension,
        # The event seeds its own Rng from the run seed, this floor and its own id, so
        # the floor is part of what the capture pins.
        "floor": (before_state.get("run") or {}).get("floor"),
        "game": capture_card.game_version(),
        "options": options,
        "before": before,
    }

    if option is not None:
        if option >= len(options):
            raise RuntimeError(
                f"{event} offers {len(options)} options; cannot choose {option}",
            )
        if options[option].get("is_locked"):
            raise RuntimeError(
                f"{event} option {option} ({options[option].get('title')!r}) is locked "
                "in this run state; stage the run so it is affordable first.",
            )
        after_state = choose(base_url, option)
        fixture["chosen"] = option
        fixture["chosen_title"] = options[option].get("title")
        fixture["after"] = trace_real_game.summarize_state(after_state)
        fixture["after_state_type"] = after_state.get("state_type")

    return fixture


def default_out(event: str, option: int | None) -> Path:
    suffix = f"-opt{option}" if option is not None else "-options"
    return FIXTURES / f"{event}{suffix}.json"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--event",
        required=True,
        help="event class name, e.g. SelfHelpBook",
    )
    parser.add_argument("--base-url", default="http://localhost:15526")
    parser.add_argument("--seed", default=DEFAULT_SEED)
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument(
        "--choose",
        type=int,
        default=None,
        help="also take this option and record what it did",
    )
    parser.add_argument(
        "--reuse-run",
        action="store_true",
        help="use the run already in progress instead of embarking a fresh one",
    )
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    fixture = capture(
        args.base_url,
        args.event,
        args.seed,
        args.ascension,
        args.choose,
        args.reuse_run,
    )
    out = args.out or default_out(args.event, args.choose)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
    print(f"{args.event}: {len(fixture['options'])} options -> {out.relative_to(REPO)}")


if __name__ == "__main__":
    main()
