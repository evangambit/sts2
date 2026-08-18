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


def wait_for_run(
    base_url: str,
    seed: str,
    timeout: float = 30.0,
    rooms_entered_before: int | None = None,
) -> dict[str, Any]:
    """Wait until the run exists AND has finished entering its first room.

    Both halves matter. `NGame.StartNewSingleplayerRun` is async: it generates the run,
    writes `current_run.save`, and only then awaits `RunManager.EnterAct ->
    EnterRoomInternal`, which preloads the room's assets. The mod reports a non-menu
    state as soon as the run state exists — in the middle of that tail.

    Acting on that early report is what produced the "internal error!" popup that was
    blamed on the game for months. Every crash log shows the same lines in order:
    `Embarking ... Seed: X`, `Wrote ... current_run.save`, `Preloading 'Event Room'
    assets...`, `[Startup] Time to main menu`, then the NRE — i.e. the harness
    saved-and-quit mid-preload and the in-flight task NREd on the state it had just had
    pulled out from under it. A successful embark logs the preload's `Complete` line
    *before* the quit.

    So wait for the game's own completion signal: `rooms_entered` counts `RoomEntered`
    events, which `EnterRoomInternal` fires as its very last statement. Pass the count
    read before embarking and this returns only once it has advanced. The `room_is_ready`
    fallback below is a proxy for older mod builds that do not report the counter — it
    narrows the window but cannot close it, because the event model answers before its
    assets finish loading.

    The seed comparison is canonical: the game folds a chosen seed with
    SeedHelper.CanonicalizeSeed before storing it, so asking for "abcdef" or a seed
    containing I or O and comparing raw strings would never match.

    Raises:
        RuntimeError: the run never appeared, or never finished entering its room.

    """
    want = canonical_seed(seed)
    deadline = time.monotonic() + timeout
    state = get_state(base_url)
    while time.monotonic() < deadline:
        entered = state.get("rooms_entered")
        room_done = (
            entered > rooms_entered_before
            if isinstance(entered, int) and rooms_entered_before is not None
            else room_is_ready(state)
        )
        if (
            state.get("state_type") != "menu"
            and room_done
            and canonical_seed(current_run_seed(base_url) or "") == want
        ):
            return state
        time.sleep(0.5)
        state = get_state(base_url)
    observed = current_run_seed(base_url)
    raise RuntimeError(
        f"Timed out waiting for seeded run {seed!r}; observed {observed!r}",
    )


def canonical_seed(seed: str) -> str:
    """Fold a seed the way SeedHelper.CanonicalizeSeed does before the game stores it."""
    return seed.upper().replace("O", "0").replace("I", "1").strip()


def room_is_ready(state: dict[str, Any]) -> bool:
    """Report whether the first room has finished entering and can be acted on.

    An event (the run always opens on the Neow ancient) only lists its options after
    `room.Enter` completes, which is exactly the await we must not interrupt. Other
    room types expose their own payload on the same schedule.
    """
    state_type = state.get("state_type")
    if state_type == "event":
        return bool((state.get("event") or {}).get("options"))
    if state_type in {"monster", "elite", "boss"}:
        return bool((state.get("player") or {}).get("hand"))
    if state_type == "map":
        return bool(state.get("map"))
    return state_type not in {None, "menu"}


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


def settle(base_url: str, min_delay: float = 0.4, timeout: float = 5.0) -> None:
    """Wait for the game to stop changing before sending the next menu action.

    The lobby/character-select screens tear down and rebuild UI nodes between
    steps. Firing the next action into that window is what produces the
    "internal error!" popup — the game NREs in NRunMusicController.UpdateTrack or
    touches an already-disposed node (ObjectDisposedException in NTopBarHp).
    Two identical consecutive reads is a cheap proxy for "settled".
    """
    time.sleep(min_delay)
    deadline = time.monotonic() + timeout
    previous = None
    while time.monotonic() < deadline:
        state = get_state(base_url)
        marker = (
            state.get("state_type"),
            state.get("menu_screen"),
            tuple(sorted(option_names(state))),
        )
        if marker == previous:
            return
        previous = marker
        time.sleep(0.2)


def back_out_to_main_menu(base_url: str, max_hops: int = 6) -> None:
    """Walk 'back' from a submenu up to the main menu.

    Distinct from return_to_main_menu, which is a save-and-quit for a run in
    progress and errors with "No run in progress" when we're merely sitting on a
    submenu (e.g. a character_select screen left behind by an aborted attempt).
    """
    for _ in range(max_hops):
        state = get_state(base_url)
        if state.get("state_type") == "menu" and state.get("menu_screen") == "main":
            return
        if "back" not in option_names(state):
            raise RuntimeError(
                f"Menu screen {state.get('menu_screen')!r} has no 'back' option; "
                "cannot reach the main menu automatically.",
            )
        post_menu(base_url, "back")
        settle(base_url)
    raise RuntimeError(f"Could not reach the main menu within {max_hops} 'back' hops")


def abandon_existing_run(base_url: str) -> None:
    state = get_state(base_url)
    if state.get("state_type") != "menu":
        # Actually inside a run — save-and-quit out to the menu.
        post_action(base_url, {"action": "return_to_main_menu"})
    elif state.get("menu_screen") != "main":
        back_out_to_main_menu(base_url)
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
    mode: str = "custom",
    ascension: int | None = None,
) -> dict[str, Any]:
    """Start a run on a chosen seed.

    Defaults to ``custom`` mode because **standard mode rejects a seed outright**
    ("Seed should not be changed in standard mode!") — only the custom-run screen's
    Lobby.SetSeed accepts one. Custom mode reports as ``character_select`` with
    ``custom_run: true``. Pass mode="standard" only for a seedless run.
    """
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
    post_menu(base_url, mode)
    wait_for_menu(base_url, "character_select")
    # Settle between each of these: they run back-to-back against a lobby screen
    # that is still rebuilding its UI, which is what triggers the crash popup.
    settle(base_url)
    post_menu(base_url, character)
    settle(base_url)
    if ascension is not None:
        # menu_select has no extra params, so the mod carries the ascension level
        # in the seed field (see McpMod.CustomRun.cs).
        post_menu(base_url, "ascension", seed=str(ascension))
        settle(base_url)
    # Read the completed-room-entry count BEFORE confirming: the embark we are about
    # to fire is what advances it, and waiting for that is what keeps the next caller
    # from tearing the run down mid-entry.
    rooms_entered_before = get_state(base_url).get("rooms_entered")
    post_menu(base_url, "confirm", seed=seed)
    return wait_for_run(
        base_url,
        seed,
        rooms_entered_before=(
            rooms_entered_before if isinstance(rooms_entered_before, int) else None
        ),
    )


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
    parser.add_argument(
        "--ascension",
        type=int,
        default=None,
        help="ascension level to set on the custom-run screen; the emulator models A8, "
        "so pass 8 for any capture that will be compared against it",
    )
    parser.add_argument("--format", choices=["pretty", "compact"], default="pretty")
    args = parser.parse_args()

    state = start_seeded_run(
        args.base_url,
        args.seed,
        args.character,
        args.abandon_existing,
        ascension=args.ascension,
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
