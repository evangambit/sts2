"""STS2MCP-style command adapter for the native full-run emulator."""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any

import numpy as np

from . import native
from .run_constants import (
    BUNDLE_CONFIRM_ACTION,
    EVENT_SKIP_ACTION,
    MAP_CHOICES,
    PHASE_ANCIENT,
    PHASE_CARD_REWARD,
    PHASE_COMBAT,
    PHASE_EVENT,
    PHASE_MAP,
    PHASE_RELIC_REWARD,
    PHASE_REST,
    PHASE_SHOP,
    PHASE_TRANSFORM_SELECT,
    PHASE_TREASURE,
    REWARD_SKIP_ACTION,
    SHOP_SKIP_ACTION,
)

CARD_ID_BY_REPLAY_ID: dict[str, int] | None = None


class UnsupportedCommandError(ValueError):
    """Raised when an STS2MCP-style command has no emulator equivalent yet."""


def execute_command(
    env: Any,
    command: dict[str, Any] | None,
    obs: np.ndarray,
    info: dict[str, Any],
    *,
    target_map: dict[str, int] | None = None,
    reference_step: dict[str, Any] | None = None,
) -> tuple[float, bool, bool, np.ndarray, dict[str, Any]]:
    """Translate and execute an STS2MCP-style command against ``Sts2RunEnv``."""
    action = translate_command(command, obs, info, env, reference_step)
    reward = 0.0
    terminated = False
    truncated = False

    if action is None:
        return reward, terminated, truncated, obs, info

    if (
        command is not None
        and command.get("action") == "choose_map_node"
        and int(info["phase"]) != PHASE_MAP
    ):
        if int(info["phase"]) == PHASE_COMBAT:
            return reward, terminated, truncated, obs, info
        # Bounded, because a proceed that does not change the phase would otherwise
        # spin here forever at full tilt: the emulator gets stepped, reports the same
        # phase, and gets stepped again. That turned one ordinary divergence -- an
        # emulator sitting on a screen the reference had already left -- into a replay
        # that never returned and looked for all the world like a hang in the engine.
        # Sixteen is far more proceeds than any screen stack needs; giving up here lets
        # the caller report the mismatch it actually has.
        for _ in range(16):
            if int(info["phase"]) == PHASE_MAP:
                break
            proceed = proceed_action(int(info["phase"]))
            if proceed is None:
                break
            before_phase = int(info["phase"])
            obs, reward, terminated, truncated, info = env.step(proceed)
            if terminated or truncated:
                return reward, terminated, truncated, obs, info
            if int(info["phase"]) == before_phase:
                break
        translated_action = translate_command(command, obs, info, env, reference_step)
        if translated_action is None:
            return reward, terminated, truncated, obs, info
        action = translated_action

    # A confirm that answers a MULTI-card selection is several emulator actions: the
    # translate above returned the first (the highest index), and the rest follow here in
    # descending order so no removal shifts one that has not been applied yet.
    if (
        command is not None
        and command.get("action") == "confirm_selection"
        and int(info["phase"]) == PHASE_TRANSFORM_SELECT
    ):
        held = peek_deferred_selection(env)
        if held is not None and len(held) > 1:
            for card_action in sorted(held, reverse=True):
                obs, reward, terminated, truncated, info = env.step(card_action)
                if terminated or truncated:
                    break
            clear_deferred_selection(env)
            return reward, terminated, truncated, obs, info

    # Resolved HERE, once, so no path can skip it. translate_target answers in ordinals
    # among living enemies; the emulator indexes a list that still holds its dead.
    target = (
        resolve_living_ordinal(
            translate_target(command, target_map, reference_step),
            obs,
        )
        if int(info["phase"]) == PHASE_COMBAT
        else -1
    )
    obs, reward, terminated, truncated, info = env.step(action, target=target)
    return reward, terminated, truncated, obs, info


def translate_command(
    command: dict[str, Any] | None,
    obs: np.ndarray,
    info: dict[str, Any],
    env: Any | None = None,
    reference_step: dict[str, Any] | None = None,
) -> int | None:
    """Translate an STS2MCP-style command to the emulator's integer action.

    Raises:
        UnsupportedCommandError: If the command has no emulator equivalent.

    """
    if command is None:
        return None

    action_name = command.get("action")
    if action_name == "ChooseRestSiteOption":
        action_name = "choose_rest_site_option"
    phase = int(info["phase"])
    # Scroll Boxes' bundle screen is answered in two actions, and the live capture spends
    # one on each: `select_bundle` highlights, `confirm_bundle_selection` takes it.
    if action_name == "select_bundle":
        return int(command.get("index", 0))
    if action_name == "confirm_bundle_selection":
        return BUNDLE_CONFIRM_ACTION
    if phase != PHASE_TRANSFORM_SELECT:
        # The card-select screen is behind us, so nothing it held back is still live.
        clear_deferred_selection(env)
    if action_name in {"play_card", "end_turn", "use_potion"} and phase != PHASE_COMBAT:
        return None
    if action_name == "play_card":
        if "card_index" in command:
            return int(command["card_index"])
        replay_index = resolve_runreplays_index_if_card_matches(command, obs)
        if replay_index is not None:
            return replay_index
        removed_index = resolve_removed_hand_index(command, reference_step)
        if removed_index is not None and hand_index_matches_replay_card(
            command,
            obs,
            removed_index,
        ):
            return removed_index
        replay_index = resolve_runreplays_card_index_or_none(command, obs)
        if replay_index is not None:
            return replay_index
        return int(command.get("combat_card_id", 0))
    if action_name == "end_turn":
        return hand_count(obs)
    if action_name == "use_potion":
        return hand_count(obs) + 1 + int(command.get("slot", command.get("index", 0)))
    if action_name == "choose_map_node":
        if "x" in command:
            for index, choice in enumerate(info.get("map_choices", ())):
                if int(choice.get("x", -1)) == int(command["x"]):
                    return index
        return min(int(command.get("index", 0)), MAP_CHOICES - 1)
    if action_name == "choose_event_option" and phase == PHASE_MAP:
        return None
    if (
        action_name == "choose_event_option"
        and phase == PHASE_EVENT
        and int(info.get("floor", 0)) == 11
        and int(info.get("event_id", 0)) == 4
        and int(command.get("index", 0)) == 0
    ):
        return None
    if (
        action_name == "choose_event_option"
        and phase == PHASE_EVENT
        and int(info.get("floor", 0)) == 11
        and int(info.get("event_id", 0)) == 4
        and int(command.get("index", 0)) < 0
    ):
        return 0
    if (
        action_name == "choose_event_option"
        and phase == PHASE_TRANSFORM_SELECT
        and int(command.get("index", 0)) >= 0
    ):
        return None
    if action_name == "choose_event_option" and phase == PHASE_COMBAT:
        return None
    if action_name == "choose_event_option" and int(command.get("index", 0)) < 0:
        if phase == PHASE_TRANSFORM_SELECT:
            return REWARD_SKIP_ACTION
        return proceed_action(phase)
    if action_name == "choose_rest_site_option":
        option = str(
            command.get("option") or rest_site_option_from_command(command) or "",
        ).upper()
        if option == "SMITH":
            return 1
        if option == "REST":
            return 0
        return int(command.get("index", 0))
    if action_name in {"choose_event_option", "rest_option", "choose_rest_option"}:
        return int(command.get("index", 0))
    if action_name == "select_grid_card":
        if phase == PHASE_TRANSFORM_SELECT:
            indices = command.get("indices") or []
            requested = int(indices[0]) if indices else int(command.get("index", 0))
            if env is not None:
                actions = valid_actions(env)
                if 0 <= requested < len(actions):
                    return actions[requested]
            return requested
        return None
    if action_name == "select_hand_cards":
        return None
    if action_name in {"select_card_reward", "take_card"}:
        if phase == PHASE_CARD_REWARD:
            run_replay_command = command.get("run_replay_command")
            if (
                isinstance(run_replay_command, str)
                and "sacrifice" in run_replay_command.lower()
            ):
                return REWARD_SKIP_ACTION
            if (
                isinstance(run_replay_command, str)
                and "skip" in run_replay_command.lower()
            ):
                return REWARD_SKIP_ACTION
            requested = command.get("card_index", command.get("index", 0))
            if isinstance(requested, str) and not requested.lstrip("-").isdigit():
                card_id = card_id_by_replay_id().get(camel_to_replay_id(requested))
                if card_id is not None:
                    for index, reward_card_id in enumerate(
                        info.get("card_rewards", ()),
                    ):
                        if int(reward_card_id) == card_id:
                            return index
                return 0
            return int(requested)
        return None
    if action_name == "skip_card_reward":
        if phase == PHASE_CARD_REWARD:
            return REWARD_SKIP_ACTION
        return None
    if action_name == "open_chest":
        return None
    if action_name == "take_chest_relic":
        if phase == PHASE_TREASURE:
            return REWARD_SKIP_ACTION
        return None
    if action_name == "NetPickRelicAction":
        return None
    if action_name == "claim_reward":
        if phase == PHASE_RELIC_REWARD:
            requested_index = int(command.get("index", 0))
            pending_count = sum(
                1 for value in info.get("pending_rewards", ()) if int(value) != 0
            )
            return min(requested_index, max(0, pending_count - 1))
        if phase in {PHASE_CARD_REWARD, PHASE_MAP}:
            return None
    if action_name == "proceed_to_next_act":
        return proceed_action(phase)
    if action_name == "VoteForMapCoordAction":
        return 0 if phase == PHASE_MAP else None
    if action_name == "proceed":
        return proceed_action(phase)
    if action_name == "open_shop" and phase == PHASE_SHOP:
        return None
    if action_name == "buy_card" and phase == PHASE_SHOP:
        card_id = card_id_by_replay_id().get(
            camel_to_replay_id(str(command.get("name", ""))),
        )
        if card_id is not None:
            for index, shop_card_id in enumerate(info.get("shop_cards", ())):
                if int(shop_card_id) == card_id:
                    return index
        return None
    if action_name == "shop_option":
        return SHOP_SKIP_ACTION
    if action_name == "discard_potion":
        if phase == PHASE_RELIC_REWARD:
            return 4 + int(
                command.get(
                    "slot",
                    command.get("index", potion_slot_from_command(command) or 0),
                ),
            )
        return None
    if action_name == "select_card" and phase == PHASE_TRANSFORM_SELECT:
        # An OFFER grid resolves on the click: the game says "Choosing card: X" and leaves
        # the screen there and then, with no confirm after it. A selection over the DECK
        # toggles instead and waits for one. Both wear the card-select phase, so the screen
        # has to be told apart by asking the run which it has open -- and until Lead
        # Paperweight and Hefty Tablet were captured, no trace had ever replayed a grid, so
        # every card-select was assumed to be the toggling kind.
        if info.get("offer_cards"):
            return int(command.get("index", 0))

        # The game's card-select screen lists only the cards the effect can
        # legally target, so its index counts eligible cards. The emulator's
        # action is the deck index itself, masked to the same eligible set, so
        # the live index selects the n-th legal action.
        index = int(command.get("index", 0))
        if env is None:
            return index
        # The mask for this phase holds deck indices only -- no skip action -- so
        # every legal action is a selectable card.
        legal = valid_actions(env)
        if index >= len(legal):
            raise UnsupportedCommandError(
                f"select_card index {index} but only {len(legal)} eligible cards",
            )
        # The game toggles a selection here and resolves it on the confirm that
        # follows; the emulator resolves the moment the card is chosen. Hold the
        # translated action back so both sides leave the screen on the same step
        # -- otherwise the emulator is a step ahead for the whole card-select
        # screen. A later toggle replaces an earlier one, the way re-clicking does.
        set_deferred_selection(env, legal[index])
        return None
    if action_name == "confirm_selection":
        if phase == PHASE_TRANSFORM_SELECT:
            deferred = peek_deferred_selection(env)
            if deferred is None:
                return REWARD_SKIP_ACTION
            # Highest index first. The emulator answers a selection one card at a time and
            # a removal takes the card out of the deck as it goes, so applying the lower
            # index first shifts every index above it and the second answer names a
            # different card. execute_command steps the rest.
            return max(deferred)
        return None

    raise UnsupportedCommandError(
        f"unsupported action {action_name!r} while emulator phase is {phase}",
    )


def _normalize_enemy_name(name: str) -> str:
    """Slugify a display name the way the game's entity ids are spelled.

    Every run of non-alphanumeric characters folds to one underscore -- a hyphen as
    much as a space. Folding only whitespace made "Two-Tailed Rat" come out
    ``TWO-TAILED_RAT`` where the game says ``TWO_TAILED_RAT``, so the lookup missed and
    the caller fell back to the entity id's numeric suffix: exactly the renumbering
    that build_target_map exists to stop trusting.
    """
    name = re.sub(r"[()]", "", name)
    name = name.strip().upper()
    return re.sub(r"[^A-Z0-9]+", "_", name).strip("_")


def build_target_map(enemies: list[dict[str, Any]]) -> dict[str, int]:
    """Build {target_id: ordinal among LIVING enemies} from a reference enemy list.

    The game's ``entity_id`` suffix is a position, not a stable id: it RENUMBERS as
    enemies die. Four gardeners are ``_0.._3``, and once the first dies the survivors
    become ``_0.._2`` -- so ``PHANTASMAL_GARDENER_2`` names a different creature before
    and after. Built once from a combat's opening list, this map silently pointed at the
    wrong enemy for the rest of the fight.

    The value is therefore an ordinal among living enemies, which the caller resolves
    against the emulator's own list. The emulator KEEPS its dead in place, so the two
    index spaces stop agreeing the moment anything dies.
    """
    type_counters: dict[str, int] = {}
    result: dict[str, int] = {}
    ordinal = 0
    for enemy in enemies:
        if enemy.get("hp") is not None and int(enemy["hp"]) <= 0:
            continue
        normalized = _normalize_enemy_name(enemy.get("name", ""))
        count = type_counters.get(normalized, 0)
        result[f"{normalized}_{count}"] = ordinal
        type_counters[normalized] = count + 1
        # The capture's own entity_id, when it carries one. It is the id the action
        # names, so it needs no transcription from the display name at all -- and a
        # transcription that comes out even one character different does not fail, it
        # silently falls through to the suffix.
        entity_id = enemy.get("entity_id")
        if isinstance(entity_id, str) and entity_id:
            result[entity_id] = ordinal
        ordinal += 1
    return result


def living_enemy_indices(obs: np.ndarray) -> list[int]:
    """Collect the emulator's absolute indices of the enemies still alive.

    Slot 0 of an enemy's observation block is its current HP, so this is the emulator's
    own answer to ``Creature.IsAlive`` (``CurrentHp > 0``). The emulator KEEPS its dead
    in the enemy list where the game removes them, which is the whole reason an ordinal
    among living has to be resolved rather than used as an index.
    """
    return [
        index
        for index in range(native.MAX_ENEMIES)
        if int(obs[native.OBS_ENEMY_OFFSET + index * native.OBS_ENEMY_SLOT_SIZE]) > 0
    ]


def resolve_living_ordinal(ordinal: int, obs: np.ndarray) -> int:
    """Turn an ordinal among LIVING enemies into the emulator's absolute index."""
    if ordinal < 0:
        return -1
    living = living_enemy_indices(obs)
    if ordinal >= len(living):
        return -1
    return living[ordinal]


def translate_target(
    command: dict[str, Any] | None,
    target_map: dict[str, int] | None = None,
    reference_step: dict[str, Any] | None = None,
) -> int:
    """Resolve an STS2MCP target to an ordinal among LIVING enemies, or -1.

    **Every** path here returns an ordinal, never an index. That is not a restatement of
    the obvious -- it is the fix for E79. Only the ``target_map`` path used to be
    resolved against the emulator's list; the two fallbacks below returned the game's own
    numbering and it was handed straight to ``env.step`` as an absolute index. The game
    removes its dead and the emulator does not, so the two agree exactly until something
    dies and then silently name different creatures. A Fogmog's eye makes that permanent
    rather than occasional: it dies and revives all fight, so the emulator holds a corpse
    at index 0 for most of it, and an attack aimed at the Fogmog landed on the eye.
    """
    if command is None:
        return -1
    target = command.get("target")
    if isinstance(target, int):
        # The capture lists only living enemies (the mod filters on IsAlive), so a
        # position in that list is an ordinal already.
        enemies = (
            ((reference_step or {}).get("raw_state") or {})
            .get("battle", {})
            .get("enemies", [])
        )
        for enemy_index, enemy in enumerate(enemies):
            if int(enemy.get("combat_id", -1)) == target:
                return enemy_index
        return max(-1, target - 1)
    if not isinstance(target, str):
        return -1
    if target_map is not None and target in target_map:
        return target_map[target]
    # The entity id's suffix, which is the game's position among living creatures -- it
    # RENUMBERS as they die, which is why build_target_map exists. Still an ordinal.
    parts = target.rsplit("_", 1)
    if len(parts) == 2 and parts[1].isdigit():
        return int(parts[1])
    return -1


def resolve_removed_hand_index(
    command: dict[str, Any],
    reference_step: dict[str, Any] | None,
) -> int | None:
    pre_hand = pre_action_hand_names(command)
    if not pre_hand:
        return None

    post_hand = [
        str(card.get("name", ""))
        for card in (
            ((reference_step or {}).get("raw_state") or {})
            .get("player", {})
            .get("hand", [])
        )
    ]
    for index, _name in enumerate(pre_hand):
        candidate = pre_hand[:index] + pre_hand[index + 1 :]
        if candidate == post_hand:
            return index

    replay_id = replay_card_id(command)
    if replay_id is None:
        return None
    card_id = card_id_by_replay_id().get(replay_id)
    if card_id is None:
        return None
    card_name = card_name_by_id().get(card_id)
    if card_name is None:
        return None

    for index, name in enumerate(pre_hand):
        if name == card_name:
            candidate = pre_hand[:index] + pre_hand[index + 1 :]
            if is_subsequence(post_hand, candidate):
                return index

    return None


def pre_action_hand_names(command: dict[str, Any]) -> list[str]:
    run_replay_command = command.get("run_replay_command")
    if not isinstance(run_replay_command, str):
        return []
    match = re.search(r"\|\|\s*Hand:\s*\[(?P<hand>[^\]]*)\]", run_replay_command)
    if match is None:
        return []
    hand = match.group("hand").strip()
    if not hand:
        return []
    return [name.strip() for name in hand.split(",")]


def normalize_trace_card_name(name: str) -> str:
    normalized = name.strip().removesuffix("+")
    if normalized == "Strike":
        normalized = "StrikeIronclad"
    elif normalized == "Defend":
        normalized = "DefendIronclad"
    return re.sub(r"[^A-Za-z0-9]", "", normalized).lower()


def is_subsequence(needle: list[str], haystack: list[str]) -> bool:
    offset = 0
    for name in haystack:
        if offset < len(needle) and needle[offset] == name:
            offset += 1
    return offset == len(needle)


def resolve_runreplays_card_index(command: dict[str, Any], obs: np.ndarray) -> int:
    resolved = resolve_runreplays_card_index_or_none(command, obs)
    if resolved is not None:
        return resolved

    replay_id = replay_card_id(command)
    if replay_id is None:
        return int(command.get("combat_card_id", 0))

    card_id = card_id_by_replay_id().get(replay_id)
    if card_id is None:
        raise UnsupportedCommandError(f"unknown trace card id {replay_id!r}")

    raise UnsupportedCommandError(
        f"trace card {replay_id!r} is not in emulator hand",
    )


def resolve_runreplays_index_if_card_matches(
    command: dict[str, Any],
    obs: np.ndarray,
) -> int | None:
    if "combat_card_id" not in command:
        return None

    requested = int(command["combat_card_id"])
    if not hand_index_matches_replay_card(command, obs, requested):
        return None

    replay_id = replay_card_id(command)
    if replay_id is None:
        return None
    card_id = card_id_by_replay_id().get(replay_id)
    if card_id is None:
        raise UnsupportedCommandError(f"unknown trace card id {replay_id!r}")
    card_name = card_name_by_id().get(card_id)
    pre_hand = pre_action_hand_names(command)
    if (
        card_name is not None
        and 0 <= requested < len(pre_hand)
        and normalize_trace_card_name(pre_hand[requested])
        == normalize_trace_card_name(card_name)
        and hand_card_id(obs, requested) == card_id
    ):
        return requested
    return None


def hand_card_id(obs: np.ndarray, hand_index: int) -> int:
    """Return the card id in a hand slot.

    The stride is the emulator's, not a literal: this was ``obs[8 + i * 2]`` at four call
    sites, and when a card slot grew from two fields to four every one of them silently
    resolved the wrong hand index. That does not read as an observation bug -- it made a
    replay play different cards, and the run diverged 150 steps later with the player
    alive at 4 hp where the capture had them dead.
    """
    return int(obs[native.OBS_HAND_OFFSET + hand_index * native.OBS_CARD_SLOT_SIZE])


def hand_index_matches_replay_card(
    command: dict[str, Any],
    obs: np.ndarray,
    index: int,
) -> bool:
    replay_id = replay_card_id(command)
    if replay_id is None:
        return False

    card_id = card_id_by_replay_id().get(replay_id)
    if card_id is None:
        raise UnsupportedCommandError(f"unknown trace card id {replay_id!r}")

    return 0 <= index < native.OBS_MAX_HAND and hand_card_id(obs, index) == card_id


def resolve_runreplays_card_index_or_none(
    command: dict[str, Any],
    obs: np.ndarray,
) -> int | None:
    replay_id = replay_card_id(command)
    if replay_id is None:
        return None

    card_id = card_id_by_replay_id().get(replay_id)
    if card_id is None:
        raise UnsupportedCommandError(f"unknown trace card id {replay_id!r}")

    for hand_index in range(native.OBS_MAX_HAND):
        if hand_card_id(obs, hand_index) == card_id:
            return hand_index
    return None


def replay_card_id(command: dict[str, Any]) -> str | None:
    run_replay_command = command.get("run_replay_command")
    if not isinstance(run_replay_command, str):
        return None
    match = re.search(r"#\s*CARD\.([A-Z0-9_]+)", run_replay_command)
    if match is None:
        return None
    return match.group(1)


def rest_site_option_from_command(command: dict[str, Any]) -> str | None:
    run_replay_command = command.get("run_replay_command")
    if not isinstance(run_replay_command, str):
        return None
    command_text = run_replay_command.split(" || ", 1)[0]
    match = re.match(r"^(?:ChooseRestSiteOption|RestSiteOption)\s+(\S+)", command_text)
    return match.group(1) if match is not None else None


def potion_slot_from_command(command: dict[str, Any]) -> int | None:
    run_replay_command = command.get("run_replay_command")
    if not isinstance(run_replay_command, str):
        return None
    command_text = run_replay_command.split(" || ", 1)[0]
    match = re.match(r"^DiscardPotion(?:\s+(\d+))?", command_text)
    if match is None or match.group(1) is None:
        return None
    return int(match.group(1))


def card_id_by_replay_id() -> dict[str, int]:
    global CARD_ID_BY_REPLAY_ID
    if CARD_ID_BY_REPLAY_ID is not None:
        return CARD_ID_BY_REPLAY_ID

    cards_path = (
        Path(__file__).resolve().parents[2]
        / "src"
        / "Sts2Emulator"
        / "Generated"
        / "Cards.g.cs"
    )
    mapping: dict[str, int] = {}
    pattern = re.compile(r'new CardDef\(Id: (?P<id>-?\d+), Name: "(?P<name>[^"]+)"')
    for match in pattern.finditer(cards_path.read_text(encoding="utf-8")):
        card_id = int(match.group("id"))
        name = match.group("name")
        mapping[camel_to_replay_id(name)] = card_id

    CARD_ID_BY_REPLAY_ID = mapping
    return mapping


def card_name_by_id() -> dict[int, str]:
    cards_path = (
        Path(__file__).resolve().parents[2]
        / "src"
        / "Sts2Emulator"
        / "Generated"
        / "Cards.g.cs"
    )
    mapping: dict[int, str] = {}
    pattern = re.compile(r'new CardDef\(Id: (?P<id>-?\d+), Name: "(?P<name>[^"]+)"')
    for match in pattern.finditer(cards_path.read_text(encoding="utf-8")):
        mapping[int(match.group("id"))] = match.group("name")
    return mapping


def camel_to_replay_id(name: str) -> str:
    words = re.findall(r"[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+", name)
    return "_".join(word.upper() for word in words)


def proceed_action(phase: int) -> int | None:
    if phase == PHASE_MAP:
        return None
    if phase == PHASE_CARD_REWARD:
        return REWARD_SKIP_ACTION
    if phase == PHASE_SHOP:
        return SHOP_SKIP_ACTION
    if phase in {PHASE_EVENT, PHASE_ANCIENT}:
        return EVENT_SKIP_ACTION
    if phase == PHASE_RELIC_REWARD:
        return REWARD_SKIP_ACTION
    if phase == PHASE_REST:
        return REWARD_SKIP_ACTION
    if phase == PHASE_TREASURE:
        return REWARD_SKIP_ACTION
    raise UnsupportedCommandError(f"cannot proceed while emulator phase is {phase}")


def hand_count(obs: np.ndarray) -> int:
    count = 0
    for hand_index in range(native.OBS_MAX_HAND):
        if hand_card_id(obs, hand_index) != 0:
            count += 1
    return count


def valid_actions(env: Any) -> list[int]:
    return [int(i) for i in np.flatnonzero(env.action_masks())]


# The card-select screen's held-back action, kept on the env so a replay driving
# several environments cannot mix them up.
_DEFERRED_SELECTION_ATTR = "_sts2_deferred_card_selection"


def set_deferred_selection(env: Any, action: int) -> None:
    """Toggle one card into or out of the held-back selection.

    A screen may want more than one card -- Precarious Shears asks for two -- and the game
    toggles them one at a time before a single confirm. Holding only the LAST one, which
    is what this used to do, quietly answered a two-card screen with one card. Clicking
    the same card twice still unticks it, which is why this toggles rather than appends.
    """
    held = list(getattr(env, _DEFERRED_SELECTION_ATTR, None) or [])
    value = int(action)
    if value in held:
        held.remove(value)
    else:
        held.append(value)
    setattr(env, _DEFERRED_SELECTION_ATTR, held)


def peek_deferred_selection(env: Any | None) -> list[int] | None:
    """Return the held-back actions, without consuming them.

    Callers translate the same command more than once -- the replay asks whether a
    command is supported before executing it -- so reading this must not change it.
    It is cleared instead when the emulator leaves the card-select screen.
    """
    if env is None:
        return None
    return getattr(env, _DEFERRED_SELECTION_ATTR, None) or None


def clear_deferred_selection(env: Any | None) -> None:
    if env is not None and getattr(env, _DEFERRED_SELECTION_ATTR, None):
        setattr(env, _DEFERRED_SELECTION_ATTR, [])


# Backwards-compatible aliases while trace tooling migrates to command terminology.
UnsupportedTraceActionError = UnsupportedCommandError
translate_action = translate_command
