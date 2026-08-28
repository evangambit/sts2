"""Emit a deterministic emulator trace for comparing against real-game traces."""

import argparse
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sts2_gym import Sts2CombatEnv, native


def summarize_observation(obs: np.ndarray) -> dict:
    card = native.OBS_CARD_SLOT_SIZE
    hand = [
        {
            "index": i,
            "id": int(obs[native.OBS_HAND_OFFSET + i * card]),
            "upgraded": bool(obs[native.OBS_HAND_OFFSET + i * card + 1]),
            "enchantment": int(obs[native.OBS_HAND_OFFSET + i * card + 2]),
            "enchant_amount": int(obs[native.OBS_HAND_OFFSET + i * card + 3]),
        }
        for i in range(native.OBS_MAX_HAND)
        if int(obs[native.OBS_HAND_OFFSET + i * card]) != 0
    ]
    player_buffs = [
        {
            "id": int(obs[native.OBS_PLAYER_BUFF_OFFSET + i * 2]),
            "amount": int(obs[native.OBS_PLAYER_BUFF_OFFSET + i * 2 + 1]),
        }
        for i in range(native.OBS_MAX_PLAYER_BUFFS)
        if int(obs[native.OBS_PLAYER_BUFF_OFFSET + i * 2]) != 0
    ]
    # Defect's ring, in order. Each slot is (type + 1, passive, evoke) with 0 for empty,
    # and the two values already carry Focus -- a Dark orb's evoke is what it has banked
    # and a Glass orb's is what it has left, neither of which is derivable from the type.
    orbs = []
    for orb_index in range(native.OBS_MAX_ORBS):
        base = native.OBS_ORB_OFFSET + orb_index * native.OBS_ORB_SLOT_SIZE
        kind = int(obs[base])
        if kind == 0:
            continue
        orbs.append(
            {
                "index": orb_index,
                "type": kind - 1,
                "passive": int(obs[base + 1]),
                "evoke": int(obs[base + 2]),
            },
        )
    enemies = []
    for enemy_index in range(native.MAX_ENEMIES):
        base = native.OBS_ENEMY_OFFSET + enemy_index * native.OBS_ENEMY_SLOT_SIZE
        hp = int(obs[base])
        max_hp = int(obs[base + 1])
        if hp == 0 and max_hp == 0:
            continue
        buffs = [
            {"id": int(obs[base + 5 + i * 2]), "amount": int(obs[base + 6 + i * 2])}
            for i in range(native.OBS_MAX_ENEMY_BUFFS)
            if int(obs[base + 5 + i * 2]) != 0
        ]
        enemies.append(
            {
                "index": enemy_index,
                "hp": hp,
                "max_hp": max_hp,
                "block": int(obs[base + 2]),
                "intent_type": int(obs[base + 3]),
                "intent_magnitude": int(obs[base + 4]),
                "secondary_intent_type": int(
                    obs[native.OBS_SECONDARY_INTENT_OFFSET + enemy_index * 2]
                ),
                "secondary_intent_magnitude": int(
                    obs[native.OBS_SECONDARY_INTENT_OFFSET + enemy_index * 2 + 1]
                ),
                "status": buffs,
            },
        )
    return {
        "player": {
            "hp": int(obs[0]),
            "max_hp": int(obs[1]),
            "block": int(obs[2]),
            "energy": int(obs[3]),
            "max_energy": int(obs[4]),
            "draw_pile_count": int(obs[5]),
            "discard_pile_count": int(obs[6]),
            "exhaust_pile_count": int(obs[7]),
            "hand": hand,
            "status": player_buffs,
            "orb_capacity": int(obs[native.OBS_ORB_CAPACITY_OFFSET]),
            "orbs": orbs,
        },
        "enemies": enemies,
    }


def valid_actions(env: Sts2CombatEnv) -> list[int]:
    return [int(i) for i in np.flatnonzero(env.action_masks())]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--encounter", type=str, default=None)
    parser.add_argument("--actions", type=int, nargs="*", default=[])
    parser.add_argument("--max-steps", type=int, default=50)
    args = parser.parse_args()

    env = Sts2CombatEnv(
        seed=args.seed,
        max_episode_steps=args.max_steps,
        encounter=args.encounter,
    )
    try:
        obs, info = env.reset()
        trace = [
            {
                "step": 0,
                "action": None,
                "reward": 0.0,
                "terminated": False,
                "truncated": False,
                "valid_actions": valid_actions(env),
                "observation": obs.tolist(),
                "summary": summarize_observation(obs),
                "info": info,
            },
        ]

        for step, action in enumerate(args.actions, start=1):
            obs, reward, terminated, truncated, info = env.step(action)
            trace.append(
                {
                    "step": step,
                    "action": action,
                    "reward": reward,
                    "terminated": terminated,
                    "truncated": truncated,
                    "valid_actions": (
                        valid_actions(env) if not (terminated or truncated) else []
                    ),
                    "observation": obs.tolist(),
                    "summary": summarize_observation(obs),
                    "info": info,
                },
            )
            if terminated or truncated:
                break

        print(
            json.dumps(
                {"seed": args.seed, "encounter": args.encounter, "trace": trace},
                indent=2,
            ),
        )
    finally:
        env.close()


if __name__ == "__main__":
    main()
