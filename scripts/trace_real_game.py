"""Emit a deterministic trace from a running Slay the Spire 2 + STS2MCP instance."""

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


def post_action(base_url: str, payload: dict[str, Any]) -> dict[str, Any]:
    return request_json(base_url, "POST", "/api/v1/singleplayer", payload)


def summarize_state(state: dict[str, Any]) -> dict[str, Any]:
    player = state.get("player") or {}
    battle = state.get("battle") or {}
    enemies = []
    for enemy in battle.get("enemies") or []:
        intents = enemy.get("intents") or []
        enemies.append(
            {
                "entity_id": enemy.get("entity_id"),
                "name": enemy.get("name"),
                "hp": enemy.get("hp"),
                "max_hp": enemy.get("max_hp"),
                "block": enemy.get("block"),
                "intents": [
                    {
                        "type": intent.get("type"),
                        "label": intent.get("label"),
                        "title": intent.get("title"),
                    }
                    for intent in intents
                ],
                "status": normalize_status(enemy.get("status") or []),
            },
        )

    # The player's side of the board. Osty is the only occupant today, and without this a
    # Necrobinder capture cannot see the character's central mechanic at all -- the mod
    # reports it, and a summary that drops it is the same blindness one layer up.
    allies = [
        {
            "entity_id": ally.get("entity_id"),
            "name": ally.get("name"),
            "hp": ally.get("hp"),
            "max_hp": ally.get("max_hp"),
            "block": ally.get("block"),
            "status": normalize_status(ally.get("status") or []),
        }
        for ally in battle.get("allies") or []
    ]

    hand = [
        {
            "index": card.get("index"),
            "id": card.get("id"),
            "name": card.get("name"),
            "type": card.get("type"),
            "cost": card.get("cost"),
            "can_play": card.get("can_play"),
            "unplayable_reason": card.get("unplayable_reason"),
            "target_type": card.get("target_type"),
            "is_upgraded": card.get("is_upgraded"),
        }
        for card in player.get("hand") or []
    ]

    return {
        "state_type": state.get("state_type"),
        "run": state.get("run"),
        "player": {
            "character": player.get("character"),
            "hp": player.get("hp"),
            "max_hp": player.get("max_hp"),
            "block": player.get("block"),
            "energy": player.get("energy"),
            "max_energy": player.get("max_energy"),
            "draw_pile_count": player.get("draw_pile_count"),
            "discard_pile_count": player.get("discard_pile_count"),
            "exhaust_pile_count": player.get("exhaust_pile_count"),
            "gold": player.get("gold"),
            # The Regent's resource. The mod reports it only for a character whose star
            # counter is always shown or when there are stars to show, so `None` here means
            # the character has none rather than that the field was missed. Same lesson as
            # `allies` above: a summary that drops a character's central mechanic is the
            # same blindness one layer up, and four Regent captures had already been taken
            # and passed before anyone noticed the number was not in them.
            "stars": player.get("stars"),
            "hand": hand,
            "status": normalize_status(player.get("status") or []),
        },
        "battle": (
            {
                "round": battle.get("round"),
                "turn": battle.get("turn"),
                "is_play_phase": battle.get("is_play_phase"),
            }
            if battle
            else None
        ),
        "enemies": enemies,
        "allies": allies,
        "menu_screen": state.get("menu_screen"),
        "menu_options": state.get("options"),
    }


def normalize_status(statuses: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "id": status.get("id"),
            "name": status.get("name"),
            "amount": status.get("amount"),
            "type": status.get("type"),
        }
        for status in statuses
    ]


def first_living_enemy_id(state: dict[str, Any]) -> str | None:
    for enemy in (state.get("battle") or {}).get("enemies") or []:
        if (enemy.get("hp") or 0) > 0:
            return enemy.get("entity_id")
    return None


def action_payload_from_index(state: dict[str, Any], action: int) -> dict[str, Any]:
    state_type = state.get("state_type")
    if state_type == "card_select":
        return {"action": "select_card", "index": action}
    if state_type not in {"monster", "elite", "boss"}:
        raise ValueError(
            f"Integer action mapping is only supported in combat, got {state_type!r}",
        )

    hand = (state.get("player") or {}).get("hand") or []
    if action == len(hand):
        return {"action": "end_turn"}
    if 0 <= action < len(hand):
        card = hand[action]
        payload: dict[str, Any] = {"action": "play_card", "card_index": action}
        target_type = str(card.get("target_type") or "")
        if "Enemy" in target_type:
            target = first_living_enemy_id(state)
            if target is not None:
                payload["target"] = target
        return payload

    potion_slot = action - len(hand) - 1
    return {"action": "use_potion", "slot": potion_slot}


def is_playable_state(state: dict[str, Any]) -> bool:
    if state.get("state_type") not in {"monster", "elite", "boss"}:
        return True
    battle = state.get("battle") or {}
    return battle.get("turn") == "player" and battle.get("is_play_phase") is True


def wait_for_state(
    base_url: str,
    delay: float,
    wait_for_play_phase: bool = False,
) -> dict[str, Any]:
    if delay > 0:
        time.sleep(delay)
    state = get_state(base_url)
    if not wait_for_play_phase:
        return state

    deadline = time.monotonic() + 10.0
    while not is_playable_state(state) and time.monotonic() < deadline:
        time.sleep(0.25)
        state = get_state(base_url)
    return state


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--actions", type=int, nargs="*", default=[])
    parser.add_argument(
        "--post-actions",
        default=None,
        help="JSON list of raw STS2MCP POST payloads",
    )
    parser.add_argument(
        "--delay",
        type=float,
        default=0.25,
        help="Seconds to wait after each POST",
    )
    parser.add_argument("--format", choices=["pretty", "compact"], default="pretty")
    args = parser.parse_args()

    raw_post_actions: list[dict[str, Any]] = []
    if args.post_actions:
        decoded = json.loads(args.post_actions)
        if not isinstance(decoded, list) or not all(
            isinstance(item, dict) for item in decoded
        ):
            raise SystemExit("--post-actions must be a JSON list of objects")
        raw_post_actions = decoded

    state = get_state(args.base_url)
    trace = [
        {
            "step": 0,
            "action": None,
            "post": None,
            "summary": summarize_state(state),
            "raw_state": state,
        },
    ]

    action_steps: list[int | dict[str, Any]] = [*args.actions, *raw_post_actions]
    for step, action in enumerate(action_steps, start=1):
        payload = (
            action
            if isinstance(action, dict)
            else action_payload_from_index(state, action)
        )
        post_result = post_action(args.base_url, payload)
        state = wait_for_state(
            args.base_url,
            args.delay,
            wait_for_play_phase=payload.get("action") == "end_turn",
        )
        trace.append(
            {
                "step": step,
                "action": action,
                "post": payload,
                "post_result": post_result,
                "summary": summarize_state(state),
                "raw_state": state,
            },
        )

    indent = None if args.format == "compact" else 2
    print(
        json.dumps(
            {"source": "sts2mcp", "base_url": args.base_url, "trace": trace},
            indent=indent,
        ),
    )


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"trace_real_game.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
