#!/usr/bin/env python3
r"""Capture a Crystal Sphere board and what divining it paid out.

The sphere is the one event with a screen of its own: an 11x11 grid of fog with fifteen
things buried under it, three or six divinations to spend, and a tool that clears either
one cell or nine. Everything about it -- where each item sits, what the uncovered ones
turn into -- is drawn from the event's own stream, so it is worth pinning the same way a
shop's stock is.

    python scripts/capture_crystal_sphere.py --option 1 --clicks 2,5 5,2 5,5 5,8 8,5 3,3
    python scripts/capture_crystal_sphere.py --option 0 --clicks 9,4 1,4 7,7
    python scripts/capture_crystal_sphere.py --option 1 --clicks 1,3:small 2,3:small

A click is ``x,y`` for the big tool or ``x,y:small`` for the small one. The fixture
records the board as it opened, the item footprints the game exposed after each click,
and the reward screen the last divination led to -- which is where the potion, relic and
card draws land, and the part that cannot be read off the grid.

Needs the game running with STS2MCP, and a mod new enough to have `debug_start_event`
and the `crystal_sphere_*` actions (see FORK_NOTES.md in the STS2MCP fork).
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import operator
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
capture_event = _load("capture_event")
trace_run = _load("trace_real_game_run")


def parse_click(text: str) -> tuple[int, int, str]:
    """``x,y`` or ``x,y:small``.

    Raises:
        argparse.ArgumentTypeError: if the click is not two integers and an optional tool.

    """
    cell, _, tool = text.partition(":")
    tool = tool or "big"
    if tool not in ("big", "small"):
        raise argparse.ArgumentTypeError(f"{text!r}: tool must be 'big' or 'small'")
    try:
        x_text, y_text = cell.split(",")
        return int(x_text), int(y_text), tool
    except ValueError:
        raise argparse.ArgumentTypeError(
            f"{text!r}: expected x,y or x,y:small",
        ) from None


def board(state: dict[str, Any]) -> dict[str, Any]:
    """Return the grid as the game shows it: cleared cells, and what has surfaced."""
    sphere = state.get("crystal_sphere") or {}
    return {
        "cleared": sorted(
            [cell["x"], cell["y"]]
            for cell in sphere.get("cells") or []
            if not cell["is_hidden"]
        ),
        # Items are listed as soon as any cell of theirs is clear, footprint and all --
        # so a partly-uncovered item pins its placement without having to be won.
        "items": sorted(
            (
                {
                    "item_type": item["item_type"],
                    "x": item["x"],
                    "y": item["y"],
                    "width": item["width"],
                    "height": item["height"],
                }
                for item in sphere.get("revealed_items") or []
            ),
            key=operator.itemgetter("x", "y"),
        ),
        "tool": sphere.get("tool"),
        "divinations_left": sphere.get("divinations_left_text"),
    }


def rewards(state: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        {
            "index": item.get("index"),
            "type": item.get("type"),
            "gold_amount": item.get("gold_amount"),
            "description": item.get("description"),
        }
        for item in (state.get("rewards") or {}).get("items") or []
    ]


def card_offers(base_url: str, state: dict[str, Any]) -> list[list[str]]:
    """Open every card reward on the screen and record what it offers, taking none.

    The reward screen calls a card offer "Add a card to your deck" and nothing more, so
    the three cards behind it -- which are the part that pins where the roll landed in the
    stream -- only exist once the offer is opened. Each is opened and then skipped, which
    leaves the deck as it was.

    Raises:
        RuntimeError: if the game refuses to open or skip an offer.

    """
    offers: list[list[str]] = []
    while True:
        card_items = [item for item in rewards(state) if item["type"] == "card"]
        if len(card_items) <= len(offers):
            return offers

        result = trace_real_game.post_action(
            base_url,
            {"action": "claim_reward", "index": card_items[len(offers)]["index"]},
        )
        if result.get("status") != "ok":
            raise RuntimeError(f"claim_reward failed: {result}")

        state = settle(base_url)
        cards = (state.get("card_reward") or {}).get("cards") or []
        offers.append([card["id"] for card in cards])

        result = trace_real_game.post_action(base_url, {"action": "skip_card_reward"})
        if result.get("status") != "ok":
            raise RuntimeError(f"skip_card_reward failed: {result}")

        state = settle(base_url)


def click(base_url: str, x: int, y: int, tool: str) -> dict[str, Any]:
    """Set the tool, spend a divination, and wait for the board to settle.

    Raises:
        RuntimeError: if the game rejects either action.

    """
    result = trace_real_game.post_action(
        base_url,
        {"action": "crystal_sphere_set_tool", "tool": tool},
    )
    if result.get("status") != "ok":
        raise RuntimeError(f"crystal_sphere_set_tool {tool} failed: {result}")

    result = trace_real_game.post_action(
        base_url,
        {"action": "crystal_sphere_click_cell", "x": x, "y": y},
    )
    if result.get("status") != "ok":
        raise RuntimeError(f"crystal_sphere_click_cell {x},{y} failed: {result}")

    # The fog clears on an animation and the last divination hands off to the reward
    # screen, so read until the board stops moving rather than after a fixed pause.
    return settle(base_url)


def settle(
    base_url: str,
    settle_reads: int = 3,
    timeout: float = 20.0,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    previous: str | None = None
    still_for = 0
    state = start_real_game_run.get_state(base_url)
    while time.monotonic() < deadline:
        current = json.dumps(
            [
                state.get("state_type"),
                board(state),
                rewards(state),
                [
                    card["id"]
                    for card in (state.get("card_reward") or {}).get("cards") or []
                ],
            ],
            sort_keys=True,
        )
        still_for = still_for + 1 if current == previous else 0
        previous = current
        if still_for >= settle_reads:
            return state
        time.sleep(0.25)
        state = start_real_game_run.get_state(base_url)

    return state


def capture(
    base_url: str,
    seed: str,
    ascension: int,
    option: int,
    clicks: list[tuple[int, int, str]],
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

    before_state = capture_event.enter_event(base_url, "CrystalSphere")
    options = capture_event.offered_options(before_state)
    opened = capture_event.choose(base_url, option)
    if opened.get("state_type") != "crystal_sphere":
        raise RuntimeError(
            f"option {option} led to {opened.get('state_type')!r}, not the minigame",
        )

    steps = []
    state = opened
    for x, y, tool in clicks:
        state = click(base_url, x, y, tool)
        steps.append(
            {
                "x": x,
                "y": y,
                "tool": tool,
                "state_type": state.get("state_type"),
                "board": (
                    board(state)
                    if state.get("state_type") == "crystal_sphere"
                    else None
                ),
            },
        )

    return {
        "_comment": (
            "Captured from the live game by scripts/capture_crystal_sphere.py. Expected "
            "values here are the GAME's, never the emulator's; re-capturing re-reads "
            "ground truth and cannot rubber-stamp an emulator regression."
        ),
        "event": "CrystalSphere",
        "seed": seed,
        "ascension": ascension,
        "option": option,
        "option_title": options[option].get("title"),
        "floor": (before_state.get("run") or {}).get("floor"),
        "game": capture_card.game_version(),
        "before": trace_run.compact_state(before_state),
        "opening_board": board(opened),
        "clicks": steps,
        "after_state_type": state.get("state_type"),
        "rewards": rewards(state),
        # Opened and skipped, so the deck below is still the one the sphere left behind.
        "card_offers": card_offers(base_url, state),
        "after": trace_run.compact_state(state),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default="http://localhost:15526")
    parser.add_argument("--seed", default=DEFAULT_SEED)
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument("--option", type=int, required=True, choices=(0, 1))
    parser.add_argument("--clicks", nargs="+", type=parse_click, required=True)
    parser.add_argument("--name", help="fixture name, defaulting to the option number")
    parser.add_argument(
        "--reuse-run",
        action="store_true",
        help="use the run already in progress instead of embarking a fresh one",
    )
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    fixture = capture(
        args.base_url,
        args.seed,
        args.ascension,
        args.option,
        args.clicks,
        args.reuse_run,
    )
    name = args.name or f"opt{args.option}"
    out = args.out or FIXTURES / f"CrystalSphere-sphere-{name}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
    print(
        f"{len(fixture['clicks'])} divinations, "
        f"{len(fixture['rewards'])} rewards -> {out.relative_to(REPO)}",
    )


if __name__ == "__main__":
    main()
