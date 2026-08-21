"""Replay a retained STS2MCP full-run trace against Sts2RunEnv."""

from __future__ import annotations

import argparse
import functools
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

import compare_traces

from sts2_gym import Sts2RunEnv, native
from sts2_gym.commands import (
    UnsupportedCommandError,
    build_target_map,
    execute_command,
    translate_command,
    valid_actions,
)
from sts2_gym.run_env import (
    NODE_BOSS,
    NODE_ELITE,
    NODE_NORMAL,
    PHASE_ANCIENT,
    PHASE_CARD_REWARD,
    PHASE_COMBAT,
    PHASE_COMPLETE,
    PHASE_EVENT,
    PHASE_MAP,
    PHASE_RELIC_REWARD,
    PHASE_REST,
    PHASE_SHOP,
    PHASE_TRANSFORM_SELECT,
    PHASE_TREASURE,
)

COMBAT_STATES = {"monster", "elite", "boss"}
DEFAULT_BOUNDARY_FIELDS = [
    "state_type",
    "run.floor",
    "player.hp",
    "player.max_hp",
    "player.gold",
]
PHASE_STATE_TYPES = {
    PHASE_CARD_REWARD: "card_reward",
    PHASE_COMPLETE: "game_over",
    PHASE_EVENT: "event",
    PHASE_MAP: "map",
    PHASE_ANCIENT: "event",
    PHASE_RELIC_REWARD: "rewards",
    PHASE_REST: "rest_site",
    PHASE_SHOP: "shop",
    PHASE_TREASURE: "treasure",
    PHASE_TRANSFORM_SELECT: "card_select",
}
COMBAT_NODE_STATE_TYPES = {
    NODE_NORMAL: "monster",
    NODE_ELITE: "elite",
    NODE_BOSS: "boss",
}

translate_action = translate_command


@dataclass(frozen=True)
class ReplayResult:
    payload: dict[str, Any]
    unsupported_action: str | None = None


def load_payload(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    compare_traces.load_trace_from_payload(payload, str(path))
    return payload


def boundary_indices(trace: list[dict[str, Any]]) -> list[int]:
    indices: list[int] = []
    previous_summary: dict[str, Any] | None = None
    for index, step in enumerate(trace):
        current_summary = compare_traces.summary(step)
        if index == 0 or is_boundary_transition(previous_summary, current_summary):
            indices.append(index)
        previous_summary = current_summary
    return indices


def is_boundary_transition(
    previous_summary: dict[str, Any] | None,
    current_summary: dict[str, Any],
) -> bool:
    if previous_summary is None:
        return True

    previous_floor = compare_traces.get_path(previous_summary, "run.floor")
    current_floor = compare_traces.get_path(current_summary, "run.floor")
    if previous_floor != current_floor:
        return True

    previous_state = previous_summary.get("state_type")
    current_state = current_summary.get("state_type")
    return (previous_state in COMBAT_STATES) != (current_state in COMBAT_STATES)


# Compared at EVERY step, not just at phase boundaries, and reported as "the first step
# where this field parted company". Five boundary fields hid the real story once already:
# a Neow bonus the emulator never offered showed up 20 steps later as a floor-2 combat
# that would not end, and the first four fields agreed the whole way there. The deck is
# absent because the mod does not report it — see the note in HANDOFF.md.
DEFAULT_PER_STEP_FIELDS = [
    "state_type",
    "player.hp",
    "player.gold",
    "player.hand",
    "battle.enemies",
]


def normalise_for_compare(field: str, value: Any) -> Any:
    """Reduce a field to what both sides can actually express.

    The live game names cards and the emulator numbers them, and the live enemy list
    carries display names and intent prose. Comparing the parts that mean the same thing
    on both sides is what makes a wide comparison usable rather than all-noise.
    """
    if field == "player.hand":
        return [card.get("id") if isinstance(card, dict) else card for card in value or []]
    if field == "battle.enemies":
        return [
            (enemy.get("hp"), enemy.get("block"))
            for enemy in value or []
            if isinstance(enemy, dict)
        ]
    return value


def first_divergences(
    reference: list[dict[str, Any]],
    emulator: list[dict[str, Any]],
    fields: list[str],
) -> list[str]:
    """The first step at which each field diverges, one line per field."""
    first: dict[str, str] = {}
    slugs = card_slug_to_id()
    for index in range(min(len(reference), len(emulator))):
        ref = compare_traces.summary(reference[index])
        emu = compare_traces.summary(emulator[index])
        for field in fields:
            if field in first:
                continue
            ref_value = normalise_for_compare(field, compare_traces.get_path(ref, field))
            emu_value = normalise_for_compare(field, compare_traces.get_path(emu, field))
            if field == "player.hand":
                ref_value = [slugs.get(card, card) for card in ref_value]
            if ref_value != emu_value:
                first[field] = (
                    f"first divergence in {field} at step {index}: "
                    f"reference={ref_value!r} emulator={emu_value!r}"
                )
    return [first[field] for field in fields if field in first]


@functools.cache
def card_slug_to_id() -> dict[str, int]:
    """The game's card entry ids mapped to ours, so hands can be compared."""
    text = (
        Path(__file__).resolve().parents[1]
        / "src"
        / "Sts2Emulator"
        / "Generated"
        / "Cards.g.cs"
    ).read_text()
    return {
        match.group(2): int(match.group(1))
        for match in re.finditer(r'Id: (\d+), Name: "[^"]*", Entry: "([A-Z0-9_]*)"', text)
    }


def compare_boundary_snapshots(
    reference: list[dict[str, Any]],
    emulator: list[dict[str, Any]],
    fields: list[str],
) -> list[str]:
    diffs: list[str] = []
    for index in boundary_indices(reference):
        if index >= len(emulator):
            if reference_boundary_matches_terminal_emulator(reference[index], emulator):
                break
            diffs.append(
                f"step {index}: emulator trace ended before reference boundary",
            )
            break

        reference_summary = compare_traces.summary(reference[index])
        emulator_summary = compare_traces.summary(emulator[index])
        if index == 0 and reference_summary.get("state_type") == "unknown":
            continue
        # The reference game shows a 'rewards' screen (pre-claim) right after combat
        # while the emulator auto-credits gold and shows 'card_reward' immediately.
        # Skip state_type and gold comparisons at this transition to avoid false diffs.
        ref_state = reference_summary.get("state_type")
        emu_state = emulator_summary.get("state_type")
        skip_fields = set()
        if index == 1 and ref_state == "event" and emu_state == "map":
            skip_fields = {"state_type"}
        if ref_state == "rewards" and emu_state == "card_reward":
            skip_fields = {"state_type", "player.gold"}
        for field in fields:
            if field in skip_fields:
                continue
            reference_value = compare_traces.get_path(reference_summary, field)
            emulator_value = compare_traces.get_path(emulator_summary, field)
            if reference_value != emulator_value:
                diffs.append(
                    f"step {index} field {field}: "
                    f"reference={reference_value!r} emulator={emulator_value!r}",
                )

        if reference_summary.get("state_type") in COMBAT_STATES:
            combat_diffs = compare_combat_boundary(
                index,
                reference_summary,
                emulator_summary,
            )
            if combat_diffs and index > 0:
                previous_emulator_summary = compare_traces.summary(emulator[index - 1])
                same_combat_entry = previous_emulator_summary.get(
                    "state_type",
                ) in COMBAT_STATES and compare_traces.get_path(
                    previous_emulator_summary,
                    "run.floor",
                ) == compare_traces.get_path(
                    reference_summary,
                    "run.floor",
                )
                if same_combat_entry:
                    previous_combat_diffs = compare_combat_boundary(
                        index,
                        reference_summary,
                        previous_emulator_summary,
                    )
                    if not previous_combat_diffs:
                        combat_diffs = []
            diffs.extend(combat_diffs)
    return diffs


def reference_boundary_matches_terminal_emulator(
    reference_step: dict[str, Any],
    emulator: list[dict[str, Any]],
) -> bool:
    if not emulator:
        return False

    reference_summary = compare_traces.summary(reference_step)
    emulator_summary = compare_traces.summary(emulator[-1])
    if reference_summary.get("state_type") != "game_over":
        return False
    if emulator_summary.get("state_type") != "game_over":
        return False

    return compare_traces.get_path(
        reference_summary,
        "run.floor",
    ) == compare_traces.get_path(
        emulator_summary,
        "run.floor",
    ) and compare_traces.get_path(
        reference_summary,
        "player.hp",
    ) == compare_traces.get_path(
        emulator_summary,
        "player.hp",
    )


def compare_combat_boundary(
    index: int,
    reference_summary: dict[str, Any],
    emulator_summary: dict[str, Any],
) -> list[str]:
    diffs: list[str] = []
    reference_enemies = (
        compare_traces.get_path(reference_summary, "battle.enemies") or []
    )
    emulator_enemies = compare_traces.get_path(emulator_summary, "battle.enemies") or []
    if len(reference_enemies) != len(emulator_enemies):
        return [
            (
                f"step {index} enemy count: "
                f"reference={len(reference_enemies)} emulator={len(emulator_enemies)}"
            ),
        ]

    for enemy_index, (reference_enemy, emulator_enemy) in enumerate(
        zip(reference_enemies, emulator_enemies),
    ):
        diffs.extend(
            (
                f"step {index} enemy {enemy_index} {field}: "
                f"reference={reference_enemy.get(field)!r} "
                f"emulator={emulator_enemy.get(field)!r}"
            )
            for field in ("hp", "max_hp", "block")
            if reference_enemy.get(field) != emulator_enemy.get(field)
        )
    return diffs


def replay_trace(
    reference_payload: dict[str, Any],
    *,
    emulator_seed: int | str,
    max_steps: int | None = None,
) -> ReplayResult:
    reference = compare_traces.load_trace_from_payload(reference_payload)
    env = Sts2RunEnv(seed=emulator_seed)
    try:
        obs, info = env.reset()
        emulator_trace = [
            make_step(0, None, 0.0, False, False, obs, info, valid_actions(env)),
        ]

        replay_steps = reference[1:]
        if max_steps is not None:
            replay_steps = replay_steps[:max_steps]

        current_target_map: dict[str, int] = {}
        prev_ref_state: str | None = None

        for reference_step in replay_steps:
            payload = reference_step.get("action")
            ref_summary = compare_traces.summary(reference_step)
            ref_state = ref_summary.get("state_type")

            # Build target map when entering a new combat from a reference battle enemy list.
            if ref_state in COMBAT_STATES and prev_ref_state not in COMBAT_STATES:
                ref_enemies = (
                    compare_traces.get_path(ref_summary, "battle.enemies") or []
                )
                current_target_map = build_target_map(ref_enemies)
            elif ref_state not in COMBAT_STATES:
                current_target_map = {}
            prev_ref_state = ref_state

            try:
                action = translate_command(payload, obs, info, env, reference_step)
            except UnsupportedCommandError as exc:
                reference_floor = compare_traces.get_path(ref_summary, "run.floor")
                return ReplayResult(
                    {
                        "source": "emulator",
                        "seed": emulator_seed,
                        "trace": emulator_trace,
                    },
                    (
                        f"step {reference_step.get('step', len(emulator_trace))}: "
                        f"{exc}; reference state_type="
                        f"{ref_summary.get('state_type')!r} "
                        f"floor={reference_floor!r}"
                    ),
                )

            try:
                if action is None:
                    reward = 0.0
                    terminated = False
                    truncated = False
                else:
                    reward, terminated, truncated, obs, info = execute_command(
                        env,
                        payload,
                        obs,
                        info,
                        target_map=current_target_map,
                        reference_step=reference_step,
                    )
            except UnsupportedCommandError as exc:
                reference_floor = compare_traces.get_path(ref_summary, "run.floor")
                return ReplayResult(
                    {
                        "source": "emulator",
                        "seed": emulator_seed,
                        "trace": emulator_trace,
                    },
                    (
                        f"step {reference_step.get('step', len(emulator_trace))}: "
                        f"{exc}; reference state_type="
                        f"{ref_summary.get('state_type')!r} "
                        f"floor={reference_floor!r}"
                    ),
                )
            emulator_trace.append(
                make_step(
                    int(reference_step.get("step") or len(emulator_trace)),
                    payload,
                    float(reward),
                    bool(terminated),
                    bool(truncated),
                    obs,
                    info,
                    [] if terminated or truncated else valid_actions(env),
                ),
            )
            if terminated or truncated:
                break

        return ReplayResult(
            {"source": "emulator", "seed": emulator_seed, "trace": emulator_trace},
        )
    finally:
        env.close()


def make_step(
    step: int,
    action: dict[str, Any] | None,
    reward: float,
    terminated: bool,
    truncated: bool,
    obs: np.ndarray,
    info: dict[str, Any],
    actions: list[int],
) -> dict[str, Any]:
    return {
        "step": step,
        "action": action,
        "reward": reward,
        "terminated": terminated,
        "truncated": truncated,
        "valid_actions": actions,
        "observation": obs.tolist(),
        "summary": summarize_env(obs, info),
        "info": info,
    }


def summarize_env(obs: np.ndarray, info: dict[str, Any]) -> dict[str, Any]:
    phase = int(info["phase"])
    state_type = (
        COMBAT_NODE_STATE_TYPES.get(int(info["current_node_type"]), "monster")
        if phase == PHASE_COMBAT
        else PHASE_STATE_TYPES.get(phase, "unknown")
    )
    return {
        "state_type": state_type,
        "run": {
            "act": 1 if info["act"] == "overgrowth" else 2,
            "floor": int(info["floor"]),
        },
        "player": summarize_player(obs, info, in_combat=phase == PHASE_COMBAT),
        "battle": summarize_battle(obs) if phase == PHASE_COMBAT else None,
        "card_reward": (
            summarize_card_reward(info) if phase == PHASE_CARD_REWARD else {}
        ),
        "event": summarize_event(info) if phase in {PHASE_EVENT, PHASE_ANCIENT} else {},
        "map": summarize_map(info) if phase == PHASE_MAP else {},
        "rewards": summarize_relic_reward(info) if phase == PHASE_RELIC_REWARD else {},
        "rest_site": {} if phase != PHASE_REST else {"can_proceed": False},
        "shop": summarize_shop(info) if phase == PHASE_SHOP else {},
    }


def summarize_player(
    obs: np.ndarray,
    info: dict[str, Any],
    *,
    in_combat: bool,
) -> dict[str, Any]:
    return {
        "hp": int(info["player_hp"]),
        "max_hp": int(info["player_max_hp"]),
        "block": int(obs[2]) if in_combat else 0,
        "energy": int(obs[3]) if in_combat else None,
        "gold": int(info["gold"]),
        "deck_size": int(info["deck_size"]),
        "relics": [{"id": int(relic_id)} for relic_id in info["relics"]],
        "potions": [
            {"id": int(potion_id)}
            for potion_id in info["potions"]
            if int(potion_id) != 0
        ],
        "hand": summarize_hand(obs) if in_combat else [],
    }


def summarize_hand(obs: np.ndarray) -> list[dict[str, Any]]:
    return [
        {"index": hand_index, "id": int(obs[8 + hand_index * 2])}
        for hand_index in range(10)
        if int(obs[8 + hand_index * 2]) != 0
    ]


def summarize_battle(obs: np.ndarray) -> dict[str, Any]:
    enemies = []
    for enemy_index in range(native.MAX_ENEMIES):
        base = 54 + enemy_index * 15
        hp = int(obs[base])
        max_hp = int(obs[base + 1])
        if hp == 0 and max_hp == 0:
            continue
        enemies.append(
            {
                "index": enemy_index,
                "hp": hp,
                "max_hp": max_hp,
                "block": int(obs[base + 2]),
                "intent_type": int(obs[base + 3]),
                "intent_mag": int(obs[base + 4]),
            },
        )
    return {"enemies": enemies}


def summarize_card_reward(info: dict[str, Any]) -> dict[str, Any]:
    cards = [
        {
            "index": index,
            "id": int(card_id),
            "is_upgraded": bool(info["card_reward_upgraded"][index]),
        }
        for index, card_id in enumerate(info["card_rewards"])
        if int(card_id) != 0
    ]
    return {"cards": cards}


def summarize_event(info: dict[str, Any]) -> dict[str, Any]:
    if int(info["phase"]) == PHASE_ANCIENT:
        return {
            "event_id": "NEOW",
            "options": [
                {"index": index, "relic_id": int(relic_id)}
                for index, relic_id in enumerate(info["neow_options"])
                if int(relic_id) != 0
            ],
        }
    return {"event_id": int(info["event_id"])}


def summarize_map(info: dict[str, Any]) -> dict[str, Any]:
    return {"next_options": list(info["map_choices"])}


def summarize_relic_reward(info: dict[str, Any]) -> dict[str, Any]:
    gold, potion_id, relic_id, card_pending = [
        int(value) for value in info["pending_rewards"]
    ]
    items = []
    if gold != 0:
        items.append({"index": len(items), "type": "gold", "gold_amount": gold})
    if potion_id != 0:
        items.append({"index": len(items), "type": "potion", "potion_id": potion_id})
    if relic_id != 0:
        items.append({"index": len(items), "type": "relic", "id": relic_id})
    # card_pending is a COUNT — Kaleidoscope offers two card rewards on one screen.
    for _ in range(card_pending):
        items.append({"index": len(items), "type": "card"})
    return {"items": items}


def summarize_shop(info: dict[str, Any]) -> dict[str, Any]:
    return {
        "cards": list(info["shop_cards"]),
        "relics": list(info["shop_relics"]),
        "potions": list(info["shop_potions"]),
        "costs": list(info["shop_costs"]),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("trace", type=Path)
    parser.add_argument("--emulator-seed", type=str, default=None)
    parser.add_argument("--max-steps", type=int)
    parser.add_argument("--field", action="append", default=[])
    parser.add_argument("--max-diffs", type=int, default=20)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    reference_payload = load_payload(args.trace)
    emulator_seed = args.emulator_seed
    if emulator_seed is None:
        emulator_seed = reference_payload.get("seed", "0")
    result = replay_trace(
        reference_payload,
        emulator_seed=emulator_seed,
        max_steps=args.max_steps,
    )
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(result.payload, indent=2) + "\n",
            encoding="utf-8",
        )

    diffs = compare_boundary_snapshots(
        compare_traces.load_trace_from_payload(reference_payload),
        compare_traces.load_trace_from_payload(result.payload),
        [*DEFAULT_BOUNDARY_FIELDS, *args.field],
    )
    early = first_divergences(
        compare_traces.load_trace_from_payload(reference_payload),
        compare_traces.load_trace_from_payload(result.payload),
        DEFAULT_PER_STEP_FIELDS,
    )
    if early:
        print("Per-step field divergences (earliest first):")
        for line in early:
            print(f"  {line}")

    if diffs:
        print(f"Full-run boundary mismatch: {len(diffs)} difference(s)")
        for diff in diffs[: args.max_diffs]:
            print(diff)
    if result.unsupported_action is not None:
        print(f"Replay stopped: {result.unsupported_action}")
        raise SystemExit(1)
    if diffs or early:
        raise SystemExit(1)

    print("Full-run snapshots match on every configured field.")


if __name__ == "__main__":
    main()
