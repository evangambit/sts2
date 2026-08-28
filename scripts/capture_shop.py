#!/usr/bin/env python3
r"""Capture a merchant's stock, as a committed fixture.

A shop is fourteen slots -- seven cards, three relics, three potions and the card-removal
service -- and every one of them is rolled from the run's own streams: which card, which
rarity, which relic, what it costs, which single slot is on sale. That makes it one of
the densest single readouts in the game, and until the mod grew `debug_start_shop` there
was no way to reach one without playing to it.

    python scripts/capture_shop.py
    python scripts/capture_shop.py --seed QS2GYXRKWN --ascension 8

The fixture records the whole board plus the run state it was rolled in, because that is
what decides it: the shop reads the Shops and Rewards streams, so where the run had got
to is part of the answer. Comparing it against the emulator is `ShopStockTests`.

Needs the game running with STS2MCP, and a mod new enough to have `debug_start_shop`
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

FIXTURES = REPO / "tests" / "fixtures" / "shop"
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
trace_run = _load("trace_real_game_run")


def enter_shop(base_url: str, timeout: float = 25.0) -> dict[str, Any]:
    """Jump the run into a merchant room and wait for its stock to settle.

    Raises:
        RuntimeError: if the mod rejects the action or no shop appears.

    """
    result = trace_real_game.post_action(base_url, {"action": "debug_start_shop"})
    if result.get("status") != "ok":
        raise RuntimeError(
            f"debug_start_shop failed: {result}. An older STS2MCP build has no "
            "debug_start_shop; rebuild and redeploy the mod.",
        )

    # The stock arrives a slot at a time, so "a shop is showing" is true long before the
    # shop is worth reading -- the same trap that had event captures recording a
    # half-built option list. Wait for the board to stop changing.
    deadline = time.monotonic() + timeout
    previous: list[dict[str, Any]] | None = None
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        items = stock(state)
        if state.get("state_type") == "shop" and items and items == previous:
            return state
        previous = items
        time.sleep(0.25)

    raise RuntimeError(f"No settled shop within {timeout:.0f}s")


def stock(state: dict[str, Any]) -> list[dict[str, Any]]:
    """Return the board as the player sees it: what is on offer, and for how much."""
    return [
        {
            "index": item.get("index"),
            "category": item.get("category"),
            "price": item.get("price"),
            "on_sale": item.get("on_sale"),
            "is_stocked": item.get("is_stocked"),
            # Exactly one of these is set, depending on the category.
            "id": item.get("card_id") or item.get("relic_id") or item.get("potion_id"),
            "is_upgraded": item.get("card_is_upgraded"),
        }
        for item in (state.get("shop") or {}).get("items") or []
    ]


def capture(
    base_url: str, seed: str, ascension: int, reuse_run: bool
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

    state = enter_shop(base_url)
    return {
        "_comment": (
            "Captured from the live game by scripts/capture_shop.py. Expected values "
            "here are the GAME's, never the emulator's; re-capturing re-reads ground "
            "truth and cannot rubber-stamp an emulator regression."
        ),
        "seed": seed,
        "ascension": ascension,
        # The shop rolls off the run's streams, so where the run had got to when it
        # opened is part of what decides the stock.
        "floor": (state.get("run") or {}).get("floor"),
        "game": capture_card.game_version(),
        "stock": stock(state),
        "before": trace_run.compact_state(state),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default="http://localhost:15526")
    parser.add_argument("--seed", default=DEFAULT_SEED)
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument(
        "--reuse-run",
        action="store_true",
        help="use the run already in progress instead of embarking a fresh one",
    )
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    fixture = capture(args.base_url, args.seed, args.ascension, args.reuse_run)
    out = args.out or FIXTURES / f"{args.seed}-a{args.ascension}-floor1.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
    print(f"{len(fixture['stock'])} slots -> {out.relative_to(REPO)}")


if __name__ == "__main__":
    main()
