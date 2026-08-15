"""Evaluate a simple policy over seeded combat or simplified run episodes."""

import argparse
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sts2_gym import Sts2CombatEnv, Sts2RunEnv, native
from sts2_gym.env import ENCOUNTER_IDS
from sts2_gym.run_env import PHASE_COMBAT, RUN_OBS_SIZE

HAND_ID_INDICES = range(8, 28, 2)
STARTER_AGGRESSIVE_PRIORITY = (30, 472, 13, 358, 508, 519, 18, 433, 137)
EvaluatedEnv = Sts2CombatEnv | Sts2RunEnv


def choose_action(env: EvaluatedEnv, policy: str) -> int:
    valid = np.flatnonzero(env.action_masks())
    obs = env._obs()
    if len(obs) == RUN_OBS_SIZE and int(obs[native.OBS_SIZE]) != PHASE_COMBAT:
        return int(valid[0])

    if policy == "end-turn":
        return int(valid[-1])
    if policy == "starter-aggressive":
        valid_set = {int(action) for action in valid}
        hand_ids = [int(obs[i]) for i in HAND_ID_INDICES]
        for card_id in STARTER_AGGRESSIVE_PRIORITY:
            for action, hand_id in enumerate(hand_ids):
                if action in valid_set and hand_id == card_id:
                    return action
        return int(valid[-1])
    return int(valid[0])


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--episodes", type=int, default=100)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument(
        "--policy",
        choices=("first-valid", "end-turn", "starter-aggressive"),
        default="first-valid",
    )
    parser.add_argument(
        "--encounter",
        choices=sorted(ENCOUNTER_IDS),
        help="Force every episode to use one encounter instead of seeded sampling.",
    )
    parser.add_argument(
        "--run-env",
        action="store_true",
        help="Evaluate simplified full-run episodes instead of single combats.",
    )
    parser.add_argument(
        "--max-episode-steps",
        type=int,
        help="Override the environment step cap for each evaluated episode.",
    )
    args = parser.parse_args()

    if args.run_env and args.encounter is not None:
        parser.error("--encounter cannot be combined with --run-env")

    wins = 0
    lengths: list[int] = []
    returns: list[float] = []
    by_encounter: dict[str, dict[str, float]] = defaultdict(
        lambda: {"episodes": 0, "wins": 0, "length": 0.0, "return": 0.0},
    )

    for episode in range(args.episodes):
        env: EvaluatedEnv = (
            Sts2RunEnv(
                seed=args.seed + episode,
                max_episode_steps=(
                    args.max_episode_steps
                    if args.max_episode_steps is not None
                    else 1000
                ),
            )
            if args.run_env
            else Sts2CombatEnv(
                seed=args.seed + episode,
                encounter=args.encounter,
                max_episode_steps=args.max_episode_steps or 50,
            )
        )
        try:
            _, reset_info = env.reset()
            encounter = (
                "run" if args.run_env else str(reset_info.get("encounter", "unknown"))
            )
            total_reward = 0.0
            steps = 0
            info = {"player_won": False}

            while True:
                _, reward, terminated, truncated, info = env.step(
                    choose_action(env, args.policy),
                )
                total_reward += reward
                steps += 1
                if terminated or truncated:
                    break

            wins += int(info.get("player_won", False))
            lengths.append(steps)
            returns.append(total_reward)
            bucket = by_encounter[encounter]
            bucket["episodes"] += 1
            bucket["wins"] += int(info.get("player_won", False))
            bucket["length"] += steps
            bucket["return"] += total_reward
        finally:
            env.close()

    print(f"episodes={args.episodes}")
    print(f"policy={args.policy}")
    if args.encounter is not None:
        print(f"encounter={args.encounter}")
    print(f"win_rate={wins / args.episodes:.3f}")
    print(f"avg_length={sum(lengths) / len(lengths):.2f}")
    print(f"avg_return={sum(returns) / len(returns):.3f}")
    print("by_encounter:")
    for encounter, stats in sorted(by_encounter.items()):
        episodes = int(stats["episodes"])
        print(
            f"  {encounter}: episodes={episodes} "
            f"win_rate={stats['wins'] / episodes:.3f} "
            f"avg_length={stats['length'] / episodes:.2f} "
            f"avg_return={stats['return'] / episodes:.3f}",
        )


if __name__ == "__main__":
    main()
