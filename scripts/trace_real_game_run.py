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

import game_version
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

# How many times to re-post an action the game refused before giving up on the capture.
MAX_ACTION_ATTEMPTS = 6

# How long the snapshot has to hold still before it counts as the result of the action
# that was just posted, rather than a frame from the middle of resolving it.
POLL_INTERVAL = 0.2
SETTLE_POLLS = 3
STARTER_AGGRESSIVE_PRIORITY = (
    "bash",
    "strike",
    "defend",
)


def compact_state(state: dict[str, Any]) -> dict[str, Any]:
    """The live state, recorded in full rather than summarised.

    Deliberately maximal. A trace is a permanent artefact and re-capturing one costs a
    whole run against the live game, so the rule is **record everything, compare what we
    can today**: the replay only checks the fields the emulator can currently produce,
    and deepening that check later must never require going back to the game. An act is
    under a thousand decisions, so the cost of the detail is nothing.

    It doubles as the shape an agent observation wants, which is the other reason not to
    thin it out: deck contents, both sides' buffs and the ordered piles are exactly what
    a policy needs and what a summary drops.
    """
    player = state.get("player") or {}
    battle = state.get("battle") or {}

    def pile(*names: str) -> Any:
        """Prefer the ordered form of a pile; the unordered one loses draw order."""
        for name in names:
            if player.get(name) is not None:
                return simplify_cards(player[name])
        return []

    return {
        "state_type": state.get("state_type"),
        "run": state.get("run") or {},
        "player": {
            "character": player.get("character"),
            "hp": player.get("hp"),
            "max_hp": player.get("max_hp"),
            "block": player.get("block"),
            "energy": player.get("energy"),
            "max_energy": player.get("max_energy"),
            "gold": player.get("gold"),
            # The deck IN ORDER, not just its size: it is what card rewards, shop
            # purchases, removals and transforms all change, and a size alone cannot
            # tell a wrong card from a right one.
            "deck": simplify_cards(player.get("deck") or []),
            "deck_size": len(player.get("deck") or []),
            "relics": simplify_named_list(player.get("relics") or []),
            "potions": simplify_named_list(player.get("potions") or []),
            "max_potion_slots": player.get("max_potion_slots"),
            # The player's own buffs. The old summary recorded the enemies' status and
            # not the player's, which is half of every combat interaction.
            "status": player.get("status"),
            "hand": pile("hand_ordered", "hand"),
            "draw_pile": pile("draw_pile_ordered", "draw_pile"),
            "discard_pile": pile("discard_pile_ordered", "discard_pile"),
            "exhaust_pile": pile("exhaust_pile"),
        },
        "battle": (
            {
                "round": battle.get("round"),
                "turn": battle.get("turn"),
                "is_play_phase": battle.get("is_play_phase"),
                "enemies": battle.get("enemies"),
            }
            if battle
            else None
        ),
        "event": state.get("event") or {},
        "rewards": state.get("rewards") or {},
        "card_reward": state.get("card_reward") or {},
        "map": state.get("map") or {},
        "shop": state.get("shop") or {},
        "rest": state.get("rest") or {},
        "rest_site": state.get("rest_site") or {},
        "treasure": state.get("treasure") or {},
        "bundle_select": state.get("bundle_select") or {},
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


def wait_for_state_to_change(
    base_url: str,
    before: dict[str, Any],
    delay: float,
    timeout: float = 25.0,
    *,
    min_combat_hand: int = 1,
    require_new_state_type: bool = False,
    settle_polls: int = SETTLE_POLLS,
) -> dict[str, Any]:
    """Wait until the game has actually ACTED on what was just posted.

    Waiting for the state to be "actionable" is not enough and was the bug under two
    days of false divergences: right after an action is posted the state is still a
    combat with cards in hand, so the predicate is already true and the snapshot
    recorded is the one from BEFORE the action. Every later step then compares the
    emulator's step N against the game's step N-1.

    Comparing the whole recorded snapshot is the strictest available signal — stricter
    than any hand-picked identity, and free, because the snapshot is being taken anyway.
    Falls through on timeout so a genuinely idempotent action (one the game ignores)
    reports as a stuck step rather than hanging the capture.

    "Changed" is still not the same as "done", which was the second bug here: an action
    resolves in pieces, and the first piece is visible long before the last. A Strike
    into a Cubex Construct showed up as the Artifact being spent — a real change, and an
    actionable state — while the damage had not landed yet, so the snapshot recorded
    against that Strike was the one from the middle of the PREVIOUS card. The state must
    therefore also hold still: it is only taken once it has stopped changing for
    settle_polls consecutive reads.
    """
    if delay > 0:
        time.sleep(delay)
    deadline = time.monotonic() + timeout
    state = start_real_game_run.get_state(base_url)
    previous: dict[str, Any] | None = None
    still_for = 0
    while time.monotonic() < deadline:
        current = compact_state(state)
        still_for = still_for + 1 if current == previous else 0
        previous = current
        # Travelling mutates the map screen before it leaves it, so "the snapshot
        # changed" comes true while the state is still a map — and the next action is
        # then a second choose_map_node, which the game rejects. A move between rooms is
        # only done when the phase itself has changed.
        left_old_phase = not require_new_state_type or state.get(
            "state_type"
        ) != before.get("state_type")
        if (
            left_old_phase
            and current != before
            and still_for >= settle_polls
            and is_actionable_state(state, min_combat_hand=min_combat_hand)
        ):
            return state
        time.sleep(POLL_INTERVAL)
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


def choose_action(
    state: dict[str, Any],
    map_index: int,
    neow_option: int | None = None,
) -> dict[str, Any] | None:
    state_type = state.get("state_type")
    if state_type in COMBAT_STATES:
        return choose_combat_action(state)
    if state_type == "event":
        return choose_event_action(state, neow_option)
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


def choose_event_action(
    state: dict[str, Any],
    neow_option: int | None = None,
) -> dict[str, Any] | None:
    options = (state.get("event") or {}).get("options") or []
    proceed = first_option_index(options, is_proceed=True)
    if proceed is not None:
        return {"action": "choose_event_option", "index": proceed}
    if (state.get("event") or {}).get("event_id") == "NEOW":
        # A caller may name the blessing to take. The default policy picks the first
        # option whose text avoids a list of blocked terms -- "choose", "transform",
        # "upgrade" and so on -- which quietly makes every relic with a pickup CHOICE
        # unreachable. Lead Paperweight and Hefty Tablet are both in that set, both were
        # offered by traces already committed, and neither has ever been captured:
        # the runs took the safe option beside them instead. Their stand-in draw counts
        # in RunEngine.AdvanceRewardRngForNeowRelic cannot be checked against anything
        # until one of them is.
        if neow_option is not None:
            return {"action": "choose_event_option", "index": neow_option}
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


def recover_stranded_run(
    base_url: str,
    payload: dict[str, Any],
    before: dict[str, Any],
    state: dict[str, Any],
    delay: float,
    *,
    min_combat_hand: int,
    attempts: int = MAX_ACTION_ATTEMPTS,
) -> tuple[dict[str, Any], int]:
    """Re-drive an action the game ACCEPTED and then did nothing with.

    The retry loop above only catches an action the game refused. This is the other
    failure, and it is the one that strands a capture: the post comes back ``ok`` and the
    run goes nowhere. It happened on a shop -- ``proceed`` opened the map screen while the
    merchant room was still the run's current room, so ``state_type`` read ``map`` and the
    map really was drawn, with the right options on it. The travel vote registered in the
    game's own log and then no room ever loaded. The state settles on ``unknown``: no
    screen at all, and every later action refused, so the capture ends there holding a run
    that is still alive.

    It is a race rather than a rule -- the committed traces all travel out of their shops
    without trouble -- which is exactly why it needs handling instead of avoiding. The
    recovery is what unsticks it by hand: nudge the run with a ``proceed`` until it is
    back on a screen it can act from, then post the same action again. Nothing is recorded
    until the run has actually moved, so a recovered step looks like any other step; the
    count goes on the snapshot's note so a capture that needed several says so.
    """
    recoveries = 0
    # A finished run is not a stranded one. `game_over` is not an actionable state, so
    # without this every capture ended by posting six pointless proceeds into a dead run
    # and labelling its last step `recovered_6` -- which buries the note under noise
    # exactly where it is meant to mean something.
    while (
        not is_actionable_state(state)
        and not is_terminal_state(state)
        and recoveries < attempts
    ):
        recoveries += 1
        # Harmless when there is nothing to proceed from: the mod answers "No proceed
        # button available or enabled" and the state is read again either way.
        trace_real_game.post_action(base_url, {"action": "proceed"})
        time.sleep(max(delay, 0.5))
        state = start_real_game_run.get_state(base_url)
        if not is_actionable_state(state):
            continue

        result = trace_real_game.post_action(base_url, payload)
        if result.get("status") == "error":
            # Back on a screen, but not one this action belongs to. Leave it to the
            # caller's own handling rather than guessing a different action here.
            return state, recoveries

        state = wait_for_state_to_change(
            base_url,
            before,
            delay,
            min_combat_hand=min_combat_hand,
            require_new_state_type=payload["action"] == "choose_map_node",
        )

    return state, recoveries


def capture_run(
    base_url: str,
    seed: str,
    character: str,
    abandon_existing: bool,
    max_steps: int,
    map_index: int,
    delay: float,
    ascension: int = 0,
    scripted_actions: list[dict[str, Any]] | None = None,
    neow_option: int | None = None,
) -> dict[str, Any]:
    state = start_real_game_run.start_seeded_run(
        base_url,
        seed,
        character,
        abandon_existing,
        ascension=ascension,
    )
    state = wait_for_actionable_state(base_url)
    trace: list[dict[str, Any]] = []
    skipped = 0
    append_snapshot(trace, 0, None, None, state)

    for step in range(1, max_steps + 1):
        payload = (
            choose_action(state, map_index, neow_option)
            if scripted_actions is None
            else next_scripted_action(scripted_actions, step)
        )
        if payload is None:
            append_snapshot(trace, len(trace), None, None, state, note="no_auto_action")
            break

        before = compact_state(state)
        # A room's screen is not ready the moment it opens: a proceed posted too early
        # comes back "No proceed button available or enabled", and a rest site answers
        # "room is not open". The game DID NOT take that action, so it must not be
        # recorded as a step — the emulator would replay an action the run never made and
        # be a move ahead from then on. Retry until it lands, and record only what did.
        result = trace_real_game.post_action(base_url, payload)
        rejections = 0
        while result.get("status") == "error" and rejections < MAX_ACTION_ATTEMPTS:
            rejections += 1
            time.sleep(max(delay, 0.5))
            state = start_real_game_run.get_state(base_url)
            # A rejection here means the screen was not ready yet, not that the choice
            # was wrong, so a scripted run re-posts the same action rather than picking
            # a new one -- picking again would walk a different run.
            retry = (
                choose_action(state, map_index, neow_option)
                if scripted_actions is None
                else payload
            )
            if retry is None:
                break
            payload = retry
            before = compact_state(state)
            result = trace_real_game.post_action(base_url, payload)

        if scripted_actions is not None and result.get("status") == "error":
            # The game refused this one every time it was offered, which means the
            # trace being replayed recorded an action the game never performed --
            # exactly what the settle fix stops happening. Skip it rather than
            # recording it or giving up: the next scripted action is the one the run
            # actually made next.
            skipped += 1
            state = start_real_game_run.get_state(base_url)
            continue

        # A new turn deals a full hand, so an end_turn is not done until five cards are
        # there; every other action only has to move the state at all.
        min_hand = 5 if payload["action"] == "end_turn" else 1
        state = wait_for_state_to_change(
            base_url,
            before,
            delay,
            min_combat_hand=min_hand,
            require_new_state_type=payload["action"] == "choose_map_node",
        )

        state, recoveries = recover_stranded_run(
            base_url,
            payload,
            before,
            state,
            delay,
            min_combat_hand=min_hand,
        )

        notes = []
        if rejections:
            notes.append(f"retried_{rejections}")
        if recoveries:
            notes.append(f"recovered_{recoveries}")
        append_snapshot(
            trace,
            len(trace),
            payload,
            result,
            state,
            note="+".join(notes) if notes else None,
        )

        if result.get("status") == "error":
            append_snapshot(trace, len(trace), None, None, state, note="post_error")
            break

        if is_terminal_state(state):
            break

    return {
        "source": "sts2mcp",
        "kind": "full_run",
        "base_url": base_url,
        "seed": seed,
        # The build this came from. A trace outlives the patch that produced it, and
        # comparing one across patches is how a "regression" turns out to be a game
        # update; tests/python/test_live_fixtures.py enforces that every fixture says.
        "game": game_version.detect(),
        "character": character,
        "captured_at": datetime.now(UTC).isoformat(),
        # Actions a replayed trace listed that the game refused outright. Non-zero
        # means the trace being replayed had recorded something the game never did.
        "skipped_refused_actions": skipped,
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


def next_scripted_action(
    actions: list[dict[str, Any]], step: int
) -> dict[str, Any] | None:
    """Return the recorded action for this step, or None once the script runs out."""
    index = step - 1
    return actions[index] if index < len(actions) else None


def recorded_actions(path: Path) -> list[dict[str, Any]]:
    """Every action a captured trace recorded, in order.

    Re-posting these against the same seed walks the same run again, which is how a
    fixture taken with a buggy capture gets regenerated rather than merely annotated:
    the run is identical, only the snapshots are read correctly this time.
    """
    payload = json.loads(path.read_text(encoding="utf-8"))
    return [
        step["action"]
        for step in payload.get("trace", [])
        if step.get("action") is not None
    ]


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
    parser.add_argument(
        "--ascension",
        type=int,
        default=0,
        help=(
            "ascension to capture at (default 0). The run layer is mostly "
            "ascension-independent, and an auto-player at A8 may die on floor 3 and buy "
            "a shallow trace."
        ),
    )
    parser.add_argument(
        "--neow-option",
        type=int,
        default=None,
        help=(
            "take this Neow option index instead of letting the auto-player pick. The "
            "default policy avoids any blessing whose text mentions a choice, which "
            "makes the relics with a pickup CHOICE -- Lead Paperweight, Hefty Tablet, "
            "Scroll Boxes -- impossible to capture. Screen a seed's three options with "
            "the emulator first: they are seed-deterministic and it models them exactly."
        ),
    )
    parser.add_argument("--format", choices=["pretty", "compact"], default="pretty")
    parser.add_argument(
        "--replay-trace",
        type=Path,
        help=(
            "re-post the actions recorded in this trace instead of choosing new ones, "
            "walking the same run again so its snapshots can be recaptured"
        ),
    )
    args = parser.parse_args()

    scripted = recorded_actions(args.replay_trace) if args.replay_trace else None

    trace = capture_run(
        args.base_url,
        args.seed,
        args.character,
        args.abandon_existing,
        args.max_steps,
        args.map_index,
        args.delay,
        args.ascension,
        scripted_actions=scripted,
        neow_option=args.neow_option,
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
