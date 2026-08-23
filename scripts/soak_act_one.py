#!/usr/bin/env python3
"""Play whole Ironclad Act 1 runs and report what the emulator actually does.

This is not a test and not an agent. It is the first thing that exercises the run
end to end -- every phase, in the order a real run meets them -- with a policy that
only ever picks a legal action. What it is looking for is the class of defect no
per-element suite can see: a phase that refuses every action, a run that neither
ends nor advances, a state the action mask leaves empty.

The policy is deliberately dumb. A good agent would reach the boss more often; a
dumb one visits more of the state space per run, which is what a soak wants.

A uniformly random policy dies around floor three and never sees the back half of
the act, so `--policy greedy` plays every card it can afford before ending the
turn. That is still not an agent, but it reaches the boss often enough to exercise
it -- and unlike handing the run extra HP, it changes nothing about the run, so
anything it finds is a real defect rather than an artefact of a doctored state.

    python scripts/soak_act_one.py --runs 200
    python scripts/soak_act_one.py --runs 50 --policy greedy --verbose
"""

from __future__ import annotations

import argparse
import collections
import random
import sys
import traceback
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

import numpy as np

from sts2_gym import native, run_env
from sts2_gym.run_env import Sts2RunEnv

# Events whose own IsAllowed refuses act index 0. None of them should ever appear in a
# run the emulator generates, and a soak is the only thing that checks that end to end.
ACT_TWO_EVENTS = {
    "CrystalSphere": 27,
    "DollRoom": 28,
    "FakeMerchant": 29,
    "PotionCourier": 30,
    "RanwidTheElder": 31,
    "RelicTrader": 32,
    "StoneOfAllTime": 35,
    "Symbiote": 36,
    "WelcomeToWongos": 41,
}

PHASE_NAMES = {
    run_env.PHASE_COMBAT: "combat",
    run_env.PHASE_CARD_REWARD: "card_reward",
    run_env.PHASE_MAP: "map",
    run_env.PHASE_REST: "rest",
    run_env.PHASE_SHOP: "shop",
    run_env.PHASE_RELIC_REWARD: "reward",
    run_env.PHASE_COMPLETE: "complete",
    run_env.PHASE_EVENT: "event",
    run_env.PHASE_ANCIENT: "neow",
    run_env.PHASE_TRANSFORM_SELECT: "card_select",
    run_env.PHASE_TREASURE: "treasure",
}


class RunOutcome:
    """What one run did, and why it stopped."""

    def __init__(self, seed: str):
        self.seed = seed
        self.steps = 0
        self.floor = 0
        self.hp = 0
        self.max_hp = 0
        self.gold = 0
        self.deck_size = 0
        self.relics = 0
        self.ending = "?"
        self.phases: collections.Counter[str] = collections.Counter()
        self.events: set[int] = set()
        self.error: str | None = None


def choose(phase: int, legal: list[int], rng: random.Random, greedy: bool) -> int:
    """Pick a legal action.

    The greedy policy differs from random in exactly one place: in combat it plays a
    card rather than ending the turn whenever it can. End turn is the highest legal
    action in combat -- the hand occupies the indices below it -- so preferring
    anything else is enough, and it is what carries a run past floor three.
    """
    if greedy and phase == run_env.PHASE_COMBAT and len(legal) > 1:
        return rng.choice(legal[:-1])
    return rng.choice(legal)


def play(
    seed: str,
    rng: random.Random,
    max_steps: int,
    verbose: bool,
    greedy: bool,
    boost: bool = False,
    extra_hp: int = 200,
) -> RunOutcome:
    outcome = RunOutcome(seed)
    env = Sts2RunEnv(seed=seed)
    try:
        # The run's seed is the STRING one the constructor took; gymnasium's reset(seed=)
        # only accepts an int and would seed nothing useful here.
        _, info = env.reset()
        if boost:
            # reset() opens the run, so the handle is live -- but it is Optional on the
            # env, and a boost applied to a closed run would silently do nothing.
            handle = env._run_handle
            assert handle is not None, "reset() left the run handle closed"
            native.run_debug_set_hp(
                handle,
                info["player_hp"] + extra_hp,
                info["player_max_hp"] + extra_hp,
            )
            native.run_debug_upgrade_deck(handle)
            info = env._info()
        stuck = 0
        last_signature = None

        for _ in range(max_steps):
            phase = info["phase"]
            outcome.phases[PHASE_NAMES.get(phase, f"phase-{phase}")] += 1
            if phase == run_env.PHASE_EVENT:
                outcome.events.add(info["event_id"])

            if phase == run_env.PHASE_COMPLETE:
                outcome.ending = "complete"
                break

            legal = [int(i) for i in np.flatnonzero(env.action_masks())]
            if not legal:
                outcome.ending = "no legal action"
                break

            # A run that keeps returning to the same state with the same options is
            # not progressing, whatever it reports.
            signature = (phase, info["floor"], info["player_hp"], tuple(legal))
            stuck = stuck + 1 if signature == last_signature else 0
            last_signature = signature
            if stuck > 200:
                outcome.ending = "looping"
                break

            action = choose(phase, legal, rng, greedy)
            _, _, terminated, truncated, info = env.step(action)
            outcome.steps += 1

            if verbose:
                print(
                    f"  {PHASE_NAMES.get(phase, phase):12s} "
                    f"floor {info['floor']:2d} hp {info['player_hp']:3d} "
                    f"action {action}",
                )

            if terminated:
                outcome.ending = "dead" if info["player_hp"] <= 0 else "complete"
                break
            if truncated:
                outcome.ending = "truncated"
                break
        else:
            outcome.ending = "step limit"

        outcome.floor = info["floor"]
        outcome.hp = info["player_hp"]
        outcome.max_hp = info["player_max_hp"]
        outcome.gold = info["gold"]
        outcome.deck_size = info["deck_size"]
        outcome.relics = len([r for r in info["relics"] if r])
    except Exception:  # noqa: BLE001 - a soak reports crashes, it does not raise them
        outcome.ending = "crashed"
        outcome.error = traceback.format_exc()
    finally:
        env.close()

    return outcome


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runs", type=int, default=100)
    parser.add_argument(
        "--seed", type=int, default=0, help="seed for the policy itself"
    )
    parser.add_argument("--max-steps", type=int, default=4000)
    parser.add_argument(
        "--boost",
        action="store_true",
        help="soak-only: extra hp and an upgraded deck, so runs reach the boss",
    )
    parser.add_argument("--extra-hp", type=int, default=200)
    parser.add_argument(
        "--policy",
        choices=("random", "greedy"),
        default="random",
        help="greedy plays a card rather than ending the turn whenever it can",
    )
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    rng = random.Random(args.seed)  # noqa: S311 - a soak policy, not a secret
    outcomes = []
    for i in range(args.runs):
        seed = f"SOAK{i:05d}"
        if args.verbose:
            print(f"--- {seed}")
        outcomes.append(
            play(
                seed,
                rng,
                args.max_steps,
                args.verbose,
                args.policy == "greedy",
                args.boost,
                args.extra_hp,
            ),
        )

    endings = collections.Counter(o.ending for o in outcomes)
    phases: collections.Counter[str] = collections.Counter()
    events: set[int] = set()
    for o in outcomes:
        phases.update(o.phases)
        events |= o.events

    print(f"\n{args.runs} runs")
    print("  endings:")
    for ending, count in endings.most_common():
        print(f"    {ending:16s} {count:5d}")

    reached = [o.floor for o in outcomes]
    print(
        f"  floors reached: min {min(reached)} median {sorted(reached)[len(reached) // 2]} max {max(reached)}"
    )
    print(f"  distinct events seen: {len(events)}")
    # An Act 2 event turning up here means the act gate leaked, which no per-event
    # suite would notice: it is a property of which events a RUN offers.
    banned = {name: eid for name, eid in ACT_TWO_EVENTS.items() if eid in events}
    if banned:
        print(f"  !! act 2 events seen in act 1: {sorted(banned)}")
    print("  phases visited:")
    for phase, count in phases.most_common():
        print(f"    {phase:14s} {count:7d}")

    bad = [
        o
        for o in outcomes
        if o.ending in ("crashed", "no legal action", "looping", "step limit")
    ]
    if bad:
        print(f"\n{len(bad)} run(s) ended badly:")
        for o in bad[:5]:
            print(
                f"  {o.seed}: {o.ending} at floor {o.floor}, hp {o.hp}, after {o.steps} steps"
            )
            if o.error:
                print("    " + o.error.strip().splitlines()[-1])
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
