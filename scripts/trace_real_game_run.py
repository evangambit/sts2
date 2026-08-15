"""Capture full-run state transitions from a running STS2MCP instance."""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import start_real_game_run
import trace_real_game

COMBAT_STATES = {"monster", "elite", "boss"}
ACTIONABLE_STATES = {
    *COMBAT_STATES,
    "card_select",
    "card_reward",
    "event",
    "bundle_select",
    "map",
    "rest",
    "rest_site",
    "rewards",
    "shop",
    "treasure",
}
DEFAULT_BASE_URL = "http://localhost:15526"
STARTER_AGGRESSIVE_PRIORITY = (
    "bash",
    "strike",
    "defend",
)


def compact_state(state: dict[str, Any]) -> dict[str, Any]:
    player = state.get("player") or {}
    run = state.get("run") or {}
    battle = state.get("battle") or {}
    rewards = state.get("rewards") or {}
    card_reward = state.get("card_reward") or {}
    event = state.get("event") or {}
    map_state = state.get("map") or {}
    shop = state.get("shop") or {}
    rest = state.get("rest") or {}
    rest_site = state.get("rest_site") or {}
    treasure = state.get("treasure") or {}
    bundle_select = state.get("bundle_select") or {}

    return {
        "state_type": state.get("state_type"),
        "run": run,
        "player": {
            "character": player.get("character"),
            "hp": player.get("hp"),
            "max_hp": player.get("max_hp"),
            "block": player.get("block"),
            "energy": player.get("energy"),
            "gold": player.get("gold"),
            "deck_size": len(player.get("deck") or []),
            "relics": simplify_named_list(player.get("relics") or []),
            "potions": simplify_named_list(player.get("potions") or []),
            "hand": simplify_cards(player.get("hand") or []),
        },
        "battle": (
            {
                "round": battle.get("round"),
                "turn": battle.get("turn"),
                "is_play_phase": battle.get("is_play_phase"),
                "enemies": [
                    {
                        "entity_id": enemy.get("entity_id"),
                        "name": enemy.get("name"),
                        "hp": enemy.get("hp"),
                        "max_hp": enemy.get("max_hp"),
                        "block": enemy.get("block"),
                        "intents": enemy.get("intents"),
                        "status": enemy.get("status"),
                    }
                    for enemy in battle.get("enemies") or []
                ],
            }
            if battle
            else None
        ),
        "event": event,
        "rewards": rewards,
        "card_reward": card_reward,
        "map": map_state,
        "shop": shop,
        "rest": rest,
        "rest_site": rest_site,
        "treasure": treasure,
        "bundle_select": bundle_select,
        "menu_screen": state.get("menu_screen"),
        "options": state.get("options"),
    }


def simplify_named_list(items: list[Any]) -> list[dict[str, Any]]:
    simplified = []
    for item in items:
        if isinstance(item, dict):
            simplified.append(
                {
                    "id": item.get("id"),
                    "name": item.get("name"),
                    "slot": item.get("slot"),
                    "counter": item.get("counter"),
                },
            )
        else:
            simplified.append({"value": item})
    return simplified


def simplify_cards(cards: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "index": card.get("index"),
            "id": card.get("id"),
            "name": card.get("name"),
            "type": card.get("type"),
            "cost": card.get("cost"),
            "description": card.get("description"),
            "can_play": card.get("can_play"),
            "target_type": card.get("target_type"),
            "is_upgraded": card.get("is_upgraded"),
        }
        for card in cards
    ]


def wait_after_action(base_url: str, delay: float) -> dict[str, Any]:
    return wait_after_action_with_min_hand(base_url, delay, min_combat_hand=1)


def wait_after_action_with_min_hand(
    base_url: str,
    delay: float,
    *,
    min_combat_hand: int,
) -> dict[str, Any]:
    if delay > 0:
        time.sleep(delay)
    state = start_real_game_run.get_state(base_url)
    deadline = time.monotonic() + 15.0
    while time.monotonic() < deadline:
        if is_actionable_state(state, min_combat_hand=min_combat_hand):
            return state
        time.sleep(0.25)
        state = start_real_game_run.get_state(base_url)
    return state


def wait_after_map_choice(base_url: str, delay: float) -> dict[str, Any]:
    if delay > 0:
        time.sleep(delay)
    state = start_real_game_run.get_state(base_url)
    deadline = time.monotonic() + 20.0
    while time.monotonic() < deadline:
        if state.get("state_type") != "map" and is_actionable_state(
            state,
            min_combat_hand=5,
        ):
            return state
        time.sleep(0.25)
        state = start_real_game_run.get_state(base_url)
    return state


def wait_for_actionable_state(
    base_url: str,
    timeout: float = 30.0,
    *,
    min_combat_hand: int = 1,
) -> dict[str, Any]:
    state = start_real_game_run.get_state(base_url)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if is_actionable_state(state, min_combat_hand=min_combat_hand):
            return state
        time.sleep(0.25)
        state = start_real_game_run.get_state(base_url)
    return state


def is_actionable_state(state: dict[str, Any], *, min_combat_hand: int = 1) -> bool:
    state_type = state.get("state_type")
    if state_type in COMBAT_STATES:
        battle = state.get("battle") or {}
        hand = (state.get("player") or {}).get("hand") or []
        return (
            battle.get("turn") == "player"
            and battle.get("is_play_phase") is True
            and len(hand) >= min_combat_hand
        )
    if state_type == "event":
        return bool((state.get("event") or {}).get("options"))
    if state_type == "rewards":
        return bool(state.get("rewards"))
    if state_type == "treasure":
        return bool(state.get("treasure"))
    if state_type == "card_reward":
        return bool((state.get("card_reward") or {}).get("cards"))
    if state_type == "bundle_select":
        bundle_select = state.get("bundle_select") or {}
        return bool(bundle_select.get("can_confirm") or bundle_select.get("bundles"))
    if state_type == "shop":
        shop = state.get("shop") or {}
        return bool(
            state.get("options") or shop.get("items") or shop.get("can_proceed"),
        )
    if state_type == "rest_site":
        rest_site = state.get("rest_site") or {}
        return bool(rest_site.get("options")) or rest_site.get("can_proceed") is True
    if state_type == "rest":
        return bool(state.get("options") or state.get(state_type))
    return state_type in {"card_select", "map"}


def choose_action(state: dict[str, Any], map_index: int) -> dict[str, Any] | None:
    state_type = state.get("state_type")
    if state_type in COMBAT_STATES:
        return choose_combat_action(state)
    if state_type == "event":
        return choose_event_action(state)
    if state_type == "rewards":
        return choose_reward_action(state)
    if state_type == "map":
        return choose_map_action(state, map_index)
    if state_type == "shop":
        return choose_shop_action(state)
    if state_type == "treasure":
        return {"action": "proceed"}
    if state_type in {"rest", "rest_site"}:
        return choose_rest_action(state)
    if state_type == "card_select":
        return choose_card_select_action(state)
    if state_type == "bundle_select":
        return choose_bundle_select_action(state)
    if state_type == "card_reward":
        return choose_card_reward_action(state)
    return None


def choose_combat_action(state: dict[str, Any]) -> dict[str, Any]:
    player = state.get("player") or {}
    hand = player.get("hand") or []
    playable = [card for card in hand if card.get("can_play") is True]
    if not playable:
        return {"action": "end_turn"}

    incoming_damage = incoming_attack_damage(state)
    current_block = int(player.get("block") or 0)
    hp = int(player.get("hp") or 0)
    if incoming_damage > current_block:
        lethal_attack = best_lethal_attack(state, playable)
        if lethal_attack is not None:
            return card_action(state, lethal_attack)

        block_card = best_block_card(playable)
        if block_card is not None and (
            incoming_damage - current_block >= 6
            or hp <= incoming_damage - current_block + 10
        ):
            return card_action(state, block_card)

    for wanted in STARTER_AGGRESSIVE_PRIORITY:
        for card in playable:
            card_text = f"{card.get('id') or ''} {card.get('name') or ''}".lower()
            if wanted in card_text:
                return card_action(state, card)
    return {"action": "end_turn"}


def card_action(state: dict[str, Any], card: dict[str, Any]) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "action": "play_card",
        "card_index": card.get("index", 0),
    }
    if "Enemy" in str(card.get("target_type") or ""):
        target = best_target_enemy_id(state, damage=card_damage(card))
        if target is not None:
            payload["target"] = target
    return payload


def incoming_attack_damage(state: dict[str, Any]) -> int:
    total = 0
    for enemy in (state.get("battle") or {}).get("enemies") or []:
        if not isinstance(enemy, dict) or (enemy.get("hp") or 0) <= 0:
            continue
        for intent in enemy.get("intents") or []:
            if not isinstance(intent, dict) or intent.get("type") != "Attack":
                continue
            total += first_int(intent.get("label")) or first_int(
                intent.get("description"),
            )
    return total


def best_lethal_attack(
    state: dict[str, Any],
    playable: list[dict[str, Any]],
) -> dict[str, Any] | None:
    enemies = [
        enemy
        for enemy in (state.get("battle") or {}).get("enemies") or []
        if isinstance(enemy, dict) and (enemy.get("hp") or 0) > 0
    ]
    if len(enemies) != 1:
        return None
    enemy_hp = int(enemies[0].get("hp") or 0)
    lethal_attacks = [
        card
        for card in playable
        if "Enemy" in str(card.get("target_type") or "")
        and card_damage(card) >= enemy_hp + int(enemies[0].get("block") or 0)
    ]
    if not lethal_attacks:
        return None
    return max(lethal_attacks, key=card_damage)


def best_block_card(playable: list[dict[str, Any]]) -> dict[str, Any] | None:
    block_cards = [card for card in playable if card_block(card) > 0]
    if not block_cards:
        return None
    return max(block_cards, key=card_block)


def best_target_enemy_id(state: dict[str, Any], *, damage: int) -> str | None:
    enemies = [
        enemy
        for enemy in (state.get("battle") or {}).get("enemies") or []
        if isinstance(enemy, dict) and (enemy.get("hp") or 0) > 0
    ]
    if not enemies:
        return None
    killable = [
        enemy
        for enemy in enemies
        if int(enemy.get("hp") or 0) + int(enemy.get("block") or 0) <= damage
    ]
    target = min(killable or enemies, key=lambda enemy: int(enemy.get("hp") or 0))
    entity_id = target.get("entity_id")
    return str(entity_id) if entity_id is not None else None


def card_damage(card: dict[str, Any]) -> int:
    text = f"{card.get('id') or ''} {card.get('name') or ''} {card.get('description') or ''}"
    lowered = text.lower()
    if "bash" in lowered:
        return 10 if card.get("is_upgraded") else 8
    if "strike" in lowered:
        return 9 if card.get("is_upgraded") else 6
    damage_match = re.search(r"deal\s+(\d+)\s+damage", lowered)
    return int(damage_match.group(1)) if damage_match else 0


def card_block(card: dict[str, Any]) -> int:
    text = f"{card.get('id') or ''} {card.get('name') or ''} {card.get('description') or ''}"
    lowered = text.lower()
    if "defend" in lowered:
        return 8 if card.get("is_upgraded") else 5
    block_match = re.search(r"gain\s+(\d+)\s+block", lowered)
    return int(block_match.group(1)) if block_match else 0


def first_int(value: Any) -> int:
    match = re.search(r"\d+", str(value or ""))
    return int(match.group(0)) if match else 0


def first_living_enemy_id(state: dict[str, Any]) -> str | None:
    for enemy in (state.get("battle") or {}).get("enemies") or []:
        if (enemy.get("hp") or 0) > 0:
            return enemy.get("entity_id")
    return None


def choose_event_action(state: dict[str, Any]) -> dict[str, Any] | None:
    options = (state.get("event") or {}).get("options") or []
    proceed = first_option_index(options, is_proceed=True)
    if proceed is not None:
        return {"action": "choose_event_option", "index": proceed}
    if (state.get("event") or {}).get("event_id") == "NEOW":
        try:
            return {
                "action": "choose_event_option",
                "index": start_real_game_run.choose_neow_option(state),
            }
        except RuntimeError:
            safe = first_neow_fallback_index(options)
            if safe is not None:
                return {"action": "choose_event_option", "index": safe}
    safe = first_non_selection_event_index(options)
    if safe is not None:
        return {"action": "choose_event_option", "index": safe}
    safe = first_unlocked_option_index(options)
    if safe is None:
        return None
    return {"action": "choose_event_option", "index": safe}


def first_neow_fallback_index(options: list[Any]) -> int | None:
    blocked_terms = (
        "choose",
        "remove",
        "select",
        "transform",
        "upgrade",
    )
    for option in options:
        if not isinstance(option, dict) or option.get("is_locked"):
            continue
        text = f"{option.get('title') or ''} {option.get('description') or ''}".lower()
        if any(term in text for term in blocked_terms):
            continue
        index = option.get("index")
        if isinstance(index, int):
            return index
    return first_unlocked_option_index(options)


def first_non_selection_event_index(options: list[Any]) -> int | None:
    blocked_terms = (
        "choose",
        "remove",
        "select",
        "transform",
        "upgrade",
    )
    for option in options:
        if not isinstance(option, dict) or option.get("is_locked"):
            continue
        text = f"{option.get('title') or ''} {option.get('description') or ''}".lower()
        if any(term in text for term in blocked_terms):
            continue
        index = option.get("index")
        if isinstance(index, int):
            return index
    return None


def choose_reward_action(state: dict[str, Any]) -> dict[str, Any]:
    rewards = state.get("rewards") or {}
    potion_slots_full = are_potion_slots_full(state)
    for item in rewards.get("items") or []:
        if not isinstance(item, dict) or item.get("type") == "card":
            continue
        if item.get("type") == "potion" and potion_slots_full:
            continue
        index = item.get("index")
        if isinstance(index, int):
            return {"action": "claim_reward", "index": index}
    for item in rewards.get("items") or []:
        if not isinstance(item, dict) or item.get("type") != "card":
            continue
        index = item.get("index")
        if isinstance(index, int):
            return {"action": "claim_reward", "index": index}
    if rewards.get("can_proceed"):
        return {"action": "proceed"}
    for item in rewards.get("items") or []:
        if isinstance(item, dict) and item.get("type") == "potion":
            index = item.get("index")
            if isinstance(index, int):
                return {"action": "claim_reward", "index": index}
    return {"action": "skip_card_reward"}


def are_potion_slots_full(state: dict[str, Any]) -> bool:
    player = state.get("player") or {}
    potions = player.get("potions") or []
    max_slots = player.get("max_potion_slots")
    return isinstance(max_slots, int) and len(potions) >= max_slots


def choose_card_reward_action(state: dict[str, Any]) -> dict[str, Any]:
    cards = (state.get("card_reward") or {}).get("cards") or []
    priority = ("pommel", "strike", "bash", "attack", "")
    for wanted in priority:
        for card in cards:
            if not isinstance(card, dict):
                continue
            card_text = (
                f"{card.get('id') or ''} {card.get('name') or ''} "
                f"{card.get('type') or ''}"
            ).lower()
            if wanted in card_text:
                index = card.get("index")
                if isinstance(index, int):
                    return {"action": "select_card_reward", "card_index": index}
    return {"action": "skip_card_reward"}


def choose_map_action(state: dict[str, Any], map_index: int) -> dict[str, Any]:
    options = (state.get("map") or {}).get("next_options") or []
    if not options:
        return {"action": "choose_map_node", "index": 0}
    if not isinstance(options, list):
        options = [options]
    best = max(
        enumerate(options),
        key=lambda item: map_option_score(state, item[1], fallback_index=item[0]),
    )
    option = best[1]
    if isinstance(option, dict):
        option_index = option.get("index", best[0])
    else:
        option_index = min(map_index, len(options) - 1)
    return {"action": "choose_map_node", "index": option_index}


def map_option_score(
    state: dict[str, Any],
    option: Any,
    *,
    fallback_index: int,
) -> tuple[int, int]:
    if not isinstance(option, dict):
        return (0, -fallback_index)
    player = state.get("player") or {}
    hp = int(player.get("hp") or 0)
    max_hp = max(1, int(player.get("max_hp") or 1))
    low_hp = hp <= int(max_hp * 0.55)
    score = node_type_score(str(option.get("type") or ""), low_hp=low_hp)
    for lead in option.get("leads_to") or []:
        if isinstance(lead, dict):
            score += node_type_score(str(lead.get("type") or ""), low_hp=low_hp) // 3
    return (score, -fallback_index)


def node_type_score(node_type: str, *, low_hp: bool) -> int:
    normalized = node_type.lower()
    if normalized == "restsite":
        return 120 if low_hp else 50
    if normalized == "shop":
        return 70
    if normalized == "treasure":
        return 55
    if normalized == "unknown":
        return 35
    if normalized == "monster":
        return 25 if low_hp else 45
    if normalized == "elite":
        return -80 if low_hp else -10
    if normalized == "boss":
        return 0
    return 0


def choose_card_select_action(state: dict[str, Any]) -> dict[str, Any]:
    card_select = state.get("card_select") or {}
    if card_select.get("can_confirm"):
        return {"action": "confirm_selection"}
    cards = card_select.get("cards") or []
    prompt = str(card_select.get("prompt") or "").lower()
    priority = (
        ("strike", "defend", "bash", "")
        if "remove" in prompt
        else ("bash", "strike", "defend", "")
    )
    for wanted in priority:
        for card in cards:
            if not isinstance(card, dict):
                continue
            card_text = f"{card.get('id') or ''} {card.get('name') or ''}".lower()
            if wanted in card_text:
                index = card.get("index")
                if isinstance(index, int):
                    return {"action": "select_card", "index": index}
    return {"action": "confirm_selection"}


def choose_bundle_select_action(state: dict[str, Any]) -> dict[str, Any] | None:
    bundle_select = state.get("bundle_select") or {}
    if bundle_select.get("can_confirm"):
        return {"action": "confirm_bundle_selection"}
    bundles = bundle_select.get("bundles") or []
    if not bundles:
        return None
    first = bundles[0]
    index = first.get("index", 0) if isinstance(first, dict) else 0
    return {"action": "select_bundle", "index": index}


def choose_shop_action(state: dict[str, Any]) -> dict[str, Any] | None:
    shop = state.get("shop") or {}
    if "items" in shop:
        return {"action": "proceed"}
    options = state.get("options") or []
    leave = first_named_option(options, ("leave", "proceed", "skip"))
    if leave is None:
        return None
    return {"action": "shop_option", "index": leave}


def choose_rest_action(state: dict[str, Any]) -> dict[str, Any] | None:
    rest_site = state.get("rest_site") or {}
    if "options" in rest_site:
        options = rest_site.get("options") or []
        if not options:
            return {"action": "proceed"}
        rest = first_named_option(options, ("rest", "sleep", "heal", "proceed"))
        if rest is None:
            rest = first_unlocked_option_index(options)
        if rest is None:
            return None
        return {"action": "choose_rest_option", "index": rest}

    options = state.get("options") or []
    rest = first_named_option(options, ("rest", "sleep", "heal", "proceed"))
    if rest is None:
        rest = first_unlocked_option_index(options)
    if rest is None:
        return None
    return {"action": "rest_option", "index": rest}


def first_option_index(
    options: list[Any],
    *,
    is_proceed: bool | None = None,
) -> int | None:
    for option in options:
        if not isinstance(option, dict):
            continue
        if option.get("is_locked"):
            continue
        if is_proceed is not None and option.get("is_proceed") is not is_proceed:
            continue
        index = option.get("index")
        if isinstance(index, int):
            return index
    return None


def first_unlocked_option_index(options: list[Any]) -> int | None:
    return first_option_index(options)


def first_named_option(options: list[Any], names: tuple[str, ...]) -> int | None:
    for option in options:
        if isinstance(option, str):
            text = option.lower()
            if any(name in text for name in names):
                return options.index(option)
            continue
        if not isinstance(option, dict) or option.get("is_locked"):
            continue
        text = (
            f"{option.get('name') or ''} "
            f"{option.get('title') or ''} "
            f"{option.get('description') or ''}"
        ).lower()
        if any(name in text for name in names):
            index = option.get("index")
            if isinstance(index, int):
                return index
    return None


def capture_run(
    base_url: str,
    seed: str,
    character: str,
    abandon_existing: bool,
    max_steps: int,
    map_index: int,
    delay: float,
) -> dict[str, Any]:
    state = start_real_game_run.start_seeded_run(
        base_url,
        seed,
        character,
        abandon_existing,
    )
    state = wait_for_actionable_state(base_url)
    trace: list[dict[str, Any]] = []
    append_snapshot(trace, 0, None, None, state)

    for step in range(1, max_steps + 1):
        payload = choose_action(state, map_index)
        if payload is None:
            append_snapshot(trace, step, None, None, state, note="no_auto_action")
            break

        previous_state_type = state.get("state_type")
        result = trace_real_game.post_action(base_url, payload)
        if payload["action"] == "choose_map_node":
            state = wait_after_map_choice(base_url, delay)
        else:
            min_hand = 5 if payload["action"] == "end_turn" else 1
            state = wait_after_action_with_min_hand(
                base_url,
                delay,
                min_combat_hand=min_hand,
            )
        append_snapshot(trace, step, payload, result, state)

        tolerated_retry_error = (
            previous_state_type == "shop" and payload.get("action") == "proceed"
        ) or (
            previous_state_type == "rest_site"
            and payload.get("action") == "choose_rest_option"
            and "not open" in str(result.get("error") or "").lower()
        )
        if result.get("status") == "error" and tolerated_retry_error:
            time.sleep(max(delay, 1.0))
            state = start_real_game_run.get_state(base_url)
            continue

        if result.get("status") == "error":
            append_snapshot(trace, step + 1, None, None, state, note="post_error")
            break

        if is_terminal_state(state):
            break

    return {
        "source": "sts2mcp",
        "kind": "full_run",
        "base_url": base_url,
        "seed": seed,
        "character": character,
        "captured_at": datetime.now(UTC).isoformat(),
        "trace": trace,
    }


def append_snapshot(
    trace: list[dict[str, Any]],
    step: int,
    action: dict[str, Any] | None,
    post_result: dict[str, Any] | None,
    state: dict[str, Any],
    note: str | None = None,
) -> None:
    trace.append(
        {
            "step": step,
            "action": action,
            "post_result": post_result,
            "summary": compact_state(state),
            "raw_state": state,
            "note": note,
        },
    )


def is_terminal_state(state: dict[str, Any]) -> bool:
    state_type = state.get("state_type")
    if state_type in {"menu", "game_over"}:
        return True
    run = state.get("run") or {}
    return bool(run.get("is_victory") or run.get("is_defeat") or run.get("is_complete"))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("seed", help="STS2 seed to capture")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--character", default="IRONCLAD")
    parser.add_argument("--max-steps", type=int, default=250)
    parser.add_argument("--map-index", type=int, default=0)
    parser.add_argument("--delay", type=float, default=0.25)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--abandon-existing", action="store_true")
    parser.add_argument("--format", choices=["pretty", "compact"], default="pretty")
    args = parser.parse_args()

    trace = capture_run(
        args.base_url,
        args.seed,
        args.character,
        args.abandon_existing,
        args.max_steps,
        args.map_index,
        args.delay,
    )
    text = json.dumps(trace, indent=None if args.format == "compact" else 2)
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text + "\n", encoding="utf-8")
    else:
        print(text)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"trace_real_game_run.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
