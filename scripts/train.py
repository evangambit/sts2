"""Train MaskablePPO on Sts2CombatEnv or Sts2RunEnv."""

import argparse
import sys
from pathlib import Path
from typing import cast

# Allow running from project root: python scripts/train.py
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sb3_contrib import MaskablePPO
from sb3_contrib.common.wrappers import ActionMasker
from stable_baselines3.common.env_checker import check_env
from stable_baselines3.common.vec_env import DummyVecEnv

from sts2_gym import Sts2CombatEnv, Sts2RunEnv

MaskableEnv = Sts2CombatEnv | Sts2RunEnv

# ── helper ────────────────────────────────────────────────────────────────────


def make_env(rank: int, use_run_env: bool):
    def _init():
        base_env: MaskableEnv = (
            Sts2RunEnv(seed=rank) if use_run_env else Sts2CombatEnv(seed=rank)
        )
        return ActionMasker(base_env, lambda e: cast("MaskableEnv", e).action_masks())

    return _init


# ── main ──────────────────────────────────────────────────────────────────────


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--timesteps", type=int, default=1_000_000)
    parser.add_argument("--n-envs", type=int, default=4)
    parser.add_argument("--save-path", type=str, default="checkpoints/maskable_ppo")
    parser.add_argument(
        "--check",
        action="store_true",
        help="Run env sanity check then exit",
    )
    parser.add_argument(
        "--run-env",
        action="store_true",
        help="Train/check the simplified full-run Sts2RunEnv instead of single combats.",
    )
    args = parser.parse_args()

    if args.check:
        env: MaskableEnv = Sts2RunEnv(seed=0) if args.run_env else Sts2CombatEnv(seed=0)
        check_env(env, warn=True, skip_render_check=True)
        print("Env check passed.")
        env.close()
        return

    Path(args.save_path).parent.mkdir(exist_ok=True, parents=True)

    vec_env = DummyVecEnv([make_env(i, args.run_env) for i in range(args.n_envs)])

    model = MaskablePPO(
        "MlpPolicy",
        vec_env,
        verbose=1,
        tensorboard_log=None,
        n_steps=256,
        batch_size=64,
        n_epochs=4,
        gamma=0.99,
        learning_rate=3e-4,
    )

    model.learn(total_timesteps=args.timesteps)
    model.save(args.save_path)
    print(f"Model saved to {args.save_path}")

    vec_env.close()


if __name__ == "__main__":
    main()
