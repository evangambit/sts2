"""Start a seeded Slay the Spire 2 run through STS2MCP."""

from __future__ import annotations

import argparse
import json
import sys
import time
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

DEFAULT_BASE_URL = "http://localhost:15526"


def request_json(
    base_url: str,
    method: str,
    path: str,
    payload: dict[str, Any] | None = None,
    timeout: float = 10.0,
) -> dict[str, Any]:
    body = None if payload is None else json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json"} if body is not None else {}
    request = Request(f"{base_url}{path}", data=body, headers=headers, method=method)
    try:
        with urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(
            f"{method} {path} failed with HTTP {exc.code}: {detail}",
        ) from exc
    except URLError as exc:
        raise RuntimeError(
            f"Could not reach STS2MCP at {base_url}: {exc.reason}",
        ) from exc


def get_state(base_url: str) -> dict[str, Any]:
    return request_json(base_url, "GET", "/api/v1/singleplayer")


def post_menu(base_url: str, option: str, seed: str | None = None) -> dict[str, Any]:
    payload: dict[str, Any] = {"action": "menu_select", "option": option}
    if seed is not None:
        payload["seed"] = seed
    result = request_json(base_url, "POST", "/api/v1/singleplayer", payload)
    if result.get("status") == "error":
        raise RuntimeError(f"menu_select {option!r} failed: {result.get('error')}")
    return result


def post_action(base_url: str, payload: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for attempt in range(5):
        result = request_json(base_url, "POST", "/api/v1/singleplayer", payload)
        if result.get("status") != "error":
            break
        if attempt == 4:
            break
        time.sleep(0.5)
    if result.get("status") == "error":
        raise RuntimeError(f"{payload.get('action')} failed: {result.get('error')}")
    return result


def current_run_seed(base_url: str) -> str | None:
    compendium = request_json(base_url, "GET", "/api/v1/compendium")
    current_run = compendium.get("current_run") or {}
    seed = current_run.get("seed")
    return seed if isinstance(seed, str) else None


def option_names(state: dict[str, Any]) -> set[str]:
    options = state.get("options") or []
    names: set[str] = set()
    for option in options:
        if isinstance(option, str):
            names.add(option)
        elif isinstance(option, dict) and isinstance(option.get("name"), str):
            names.add(option["name"])
    return names


def wait_for_menu(
    base_url: str,
    menu_screen: str,
    timeout: float = 10.0,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        if (
            state.get("state_type") == "menu"
            and state.get("menu_screen") == menu_screen
            and "options" in state
        ):
            return state
        time.sleep(0.25)
        state = get_state(base_url)
    raise RuntimeError(f"Timed out waiting for menu screen {menu_screen!r}")


def wait_for_run(base_url: str, seed: str, timeout: float = 30.0) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        if state.get("state_type") != "menu" and current_run_seed(base_url) == seed:
            return state
        time.sleep(0.5)
        state = get_state(base_url)
    observed = current_run_seed(base_url)
    raise RuntimeError(
        f"Timed out waiting for seeded run {seed!r}; observed {observed!r}",
    )


def wait_for_state_type(
    base_url: str,
    state_types: set[str],
    timeout: float = 30.0,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        if state.get("state_type") in state_types:
            return state
        time.sleep(0.5)
        state = get_state(base_url)
    raise RuntimeError(f"Timed out waiting for state type in {sorted(state_types)}")


def wait_for_combat_ready(base_url: str, timeout: float = 20.0) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        if (
            state.get("state_type") in {"monster", "elite", "boss"}
            and len((state.get("player") or {}).get("hand") or []) >= 5
        ):
            return state
        time.sleep(0.5)
        state = get_state(base_url)
    raise RuntimeError("Timed out waiting for combat opening hand")


def wait_for_event_options(base_url: str, timeout: float = 10.0) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        if state.get("state_type") == "event" and (state.get("event") or {}).get(
            "options",
        ):
            return state
        time.sleep(0.25)
        state = get_state(base_url)
    raise RuntimeError("Timed out waiting for event options")


def abandon_existing_run(base_url: str) -> None:
    state = get_state(base_url)
    if state.get("state_type") != "menu" or state.get("menu_screen") != "main":
        post_action(base_url, {"action": "return_to_main_menu"})
    main = wait_for_menu(base_url, "main", timeout=30.0)
    if "abandon_run" not in option_names(main):
        return

    post_menu(base_url, "abandon_run")
    popup = wait_for_menu(base_url, "popup")
    if "yes" not in option_names(popup):
        raise RuntimeError("Abandon confirmation popup did not expose a 'yes' option")
    post_menu(base_url, "yes")
    wait_for_menu(base_url, "main")


def start_seeded_run(
    base_url: str,
    seed: str,
    character: str,
    abandon_existing: bool,
) -> dict[str, Any]:
    if abandon_existing:
        abandon_existing_run(base_url)
        state = wait_for_menu(base_url, "main")
    else:
        state = wait_for_menu(base_url, "main")
    if "singleplayer" not in option_names(state):
        if not abandon_existing:
            raise RuntimeError(
                "Main menu has no singleplayer option, likely because a run exists. "
                "Use --abandon-existing to replace it.",
            )
        abandon_existing_run(base_url)
        state = wait_for_menu(base_url, "main")

    post_menu(base_url, "singleplayer")
    wait_for_menu(base_url, "singleplayer")
    post_menu(base_url, "standard")
    wait_for_menu(base_url, "character_select")
    post_menu(base_url, character)
    post_menu(base_url, "confirm", seed=seed)
    return wait_for_run(base_url, seed)


def enter_first_combat(
    base_url: str,
    neow_option: int,
    map_index: int,
) -> dict[str, Any]:
    state = wait_for_state_type(
        base_url,
        {"event", "rewards", "map", "monster", "elite", "boss"},
    )
    if state.get("state_type") == "event":
        state = wait_for_event_options(base_url)
        if neow_option < 0:
            neow_option = choose_neow_option(state)
        post_action(
            base_url,
            {"action": "choose_event_option", "index": neow_option},
        )
        state = wait_for_state_type(base_url, {"event", "rewards", "map"})
        if state.get("state_type") == "event":
            options = (state.get("event") or {}).get("options") or []
            proceed = next(
                (
                    option.get("index")
                    for option in options
                    if isinstance(option, dict) and option.get("is_proceed") is True
                ),
                None,
            )
            if proceed is None:
                raise RuntimeError(
                    "Neow did not expose a proceed option after selection",
                )
            post_action(base_url, {"action": "choose_event_option", "index": proceed})
            state = wait_for_state_type(base_url, {"rewards", "map"})

    while state.get("state_type") == "rewards":
        rewards = state.get("rewards") or {}
        if rewards.get("can_proceed"):
            post_action(base_url, {"action": "proceed"})
        else:
            post_action(base_url, {"action": "skip_card_reward"})
        state = wait_for_state_type(base_url, {"rewards", "map"})

    if state.get("state_type") == "map":
        post_action(base_url, {"action": "choose_map_node", "index": map_index})
        state = wait_for_state_type(base_url, {"monster", "elite", "boss"})

    return wait_for_combat_ready(base_url)


def choose_neow_option(state: dict[str, Any]) -> int:
    options = (state.get("event") or {}).get("options") or []
    blocked_terms = (
        "add",
        "arcane",
        "brand",
        "choose",
        "create",
        "gold",
        "greed",
        "transform",
        "upgrade",
        "potion",
        "receive",
        "deck",
        "heal",
        "hp",
        "lose",
        "max hp",
        "reward",
    )
    for option in options:
        if not isinstance(option, dict) or option.get("is_locked"):
            continue
        text = f"{option.get('title') or ''} {option.get('description') or ''}".lower()
        if not any(term in text for term in blocked_terms):
            index = option.get("index")
            if isinstance(index, int):
                return index

    raise RuntimeError("Neow did not expose a safe unlocked option")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("seed", help="STS2 seed to use for the new standard run")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--character", default="IRONCLAD")
    parser.add_argument(
        "--enter-first-combat",
        action="store_true",
        help="Choose a Neow option, proceed, and enter a first-floor combat",
    )
    parser.add_argument(
        "--neow-option",
        type=int,
        default=-1,
        help="Neow option index; -1 auto-selects a non-deck-changing option when possible",
    )
    parser.add_argument("--map-index", type=int, default=0)
    parser.add_argument(
        "--abandon-existing",
        action="store_true",
        help="Abandon an existing run if it blocks starting a new one",
    )
    parser.add_argument("--format", choices=["pretty", "compact"], default="pretty")
    args = parser.parse_args()

    state = start_seeded_run(
        args.base_url,
        args.seed,
        args.character,
        args.abandon_existing,
    )
    if args.enter_first_combat:
        state = enter_first_combat(args.base_url, args.neow_option, args.map_index)
    indent = None if args.format == "compact" else 2
    print(
        json.dumps(
            {
                "source": "sts2mcp",
                "base_url": args.base_url,
                "seed": current_run_seed(args.base_url),
                "state": state,
            },
            indent=indent,
        ),
    )


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"start_real_game_run.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
