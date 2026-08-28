#!/usr/bin/env python3
"""Stress the agent-facing interfaces and report what an agent would actually meet.

Not a trainer. The point is to find design problems in the OBSERVATION, the ACTION
encoding and the throughput before a training run encodes them -- and to leave a number
that later emulator work can be measured against.

An agent trained today would be of dubious value anyway: the audits still list open
flags, and a policy is perfectly happy to exploit an emulator bug. What is worth having
now is the shape of the interface it would use.

    uv run python scripts/agent_probe.py
    uv run python scripts/agent_probe.py --runs 40 --section actions
"""

from __future__ import annotations

import argparse
import ctypes
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "src"))

from sts2_gym import Sts2RunEnv, native
from sts2_gym import run_constants as rc

PHASE_NAMES = {
    getattr(rc, name): name.removeprefix("PHASE_").lower()
    for name in dir(rc)
    if name.startswith("PHASE_")
}


def rollout(env, rng, limit: int):
    """Play randomly but legally.

    Yields:
        (phase, mask, obs, info) as they stand BEFORE each step is taken.

    """
    obs, info = env.reset()
    for _ in range(limit):
        mask = env.action_masks()
        legal = np.flatnonzero(mask)
        if legal.size == 0:
            return
        yield int(info["phase"]), mask, obs, info
        obs, _reward, terminated, truncated, info = env.step(int(rng.choice(legal)))
        if terminated or truncated:
            return


def section_spaces(args, rng) -> None:
    print("\n── spaces ──")
    env = Sts2RunEnv(seed="PROBE00001", max_floors=64, max_episode_steps=4000)
    obs, _info = env.reset()
    print(f"  observation : {obs.shape[0]} ints, dtype {obs.dtype}")
    print(f"  actions     : {native.RUN_MAX_ACTIONS} discrete, masked")
    print(f"  handle pool : {256} concurrent runs (Sts2Run_Clone shares it)")

    lo, hi = int(obs.min()), int(obs.max())
    print(f"  obs range at reset: [{lo}, {hi}]")
    print(
        "  NOTE card ids reach the network as RAW INTEGERS in these slots. A network "
        "reading them as magnitudes learns that card 473 is 'more' than card 472; they "
        "want an embedding, and the observation cannot say so on its own.",
    )
    env.close()


def section_observation(args, rng) -> None:
    print("\n── observation use ──")
    used = np.zeros(native.RUN_OBS_SIZE, dtype=bool)
    for trial in range(args.runs):
        env = Sts2RunEnv(seed=f"PROBE{trial:05d}", max_floors=64, max_episode_steps=4000)
        for _phase, _mask, obs, _info in rollout(env, rng, args.steps):
            used |= obs != 0
        env.close()

    live = int(used.sum())
    print(f"  slots ever non-zero: {live} / {native.RUN_OBS_SIZE} ({100 * live // native.RUN_OBS_SIZE}%)")
    blocks = {
        "scalars": (0, rc.RUN_SCALAR_OBS_SIZE if hasattr(rc, "RUN_SCALAR_OBS_SIZE") else 20),
        "deck": (rc.DECK_OBS_OFFSET, rc.RELIC_OBS_OFFSET)
        if hasattr(rc, "DECK_OBS_OFFSET")
        else None,
    }
    for name, span in blocks.items():
        if span is None:
            continue
        start, end = span
        seg = used[start:end]
        print(f"  {name:<9}: {int(seg.sum())}/{len(seg)} slots used")
    print(
        "  A block that is never non-zero is either dead weight in every forward pass "
        "or a screen the random policy never reaches -- worth telling apart before "
        "sizing a network around it.",
    )


def section_actions(args, rng) -> None:
    print("\n── action encoding ──")
    per_phase: dict[int, Counter] = defaultdict(Counter)
    widths: dict[int, list[int]] = defaultdict(list)
    for trial in range(args.runs):
        env = Sts2RunEnv(seed=f"PROBE{trial:05d}", max_floors=64, max_episode_steps=4000)
        for phase, mask, _obs, _info in rollout(env, rng, args.steps):
            legal = np.flatnonzero(mask)
            widths[phase].append(legal.size)
            per_phase[phase].update(legal.tolist())
        env.close()

    print(f"  {'phase':<16}{'seen':>7}{'legal min':>11}{'max':>6}{'mean':>7}   distinct indices")
    for phase in sorted(widths):
        w = widths[phase]
        name = PHASE_NAMES.get(phase, f"#{phase}")
        print(
            f"  {name:<16}{len(w):>7}{min(w):>11}{max(w):>6}{sum(w) / len(w):>7.1f}"
            f"   {len(per_phase[phase])}",
        )

    overlap = Counter()
    for counter in per_phase.values():
        for index in counter:
            overlap[index] += 1
    shared = sum(1 for n in overlap.values() if n > 1)
    print(
        f"\n  {shared} action indices are legal in MORE THAN ONE phase, meaning the same "
        "output neuron means different things depending on the screen. That is workable "
        "-- the mask keeps it legal -- but the network only has the observation to tell "
        "the phases apart, so the phase had better be prominent in it.",
    )


def section_throughput(args, rng) -> None:
    """Per-step cost, BY PHASE.

    A single aggregate is misleading in both directions. Timing `run_step` with a fixed
    action mostly measures REJECTED actions -- the mask says no, it returns immediately,
    and you get 2us and a wrong idea of how fast the simulator is. Timing a loop that
    resets folds whole-run generation into the average instead. What an agent actually
    pays is a per-phase mixture, and the phases differ by more than an order of
    magnitude: a combat action resolves cards and a whole enemy turn, while entering an
    act generates its map.
    """
    print("\n── throughput (per step, by phase) ──")
    cost: dict[int, list[float]] = defaultdict(list)
    for trial in range(max(4, args.runs // 3)):
        env = Sts2RunEnv(seed=f"PERF{trial:06d}", max_floors=64, max_episode_steps=4000)
        _obs, info = env.reset()
        for _ in range(args.steps):
            legal = np.flatnonzero(env.action_masks())
            if legal.size == 0:
                break
            phase = int(info["phase"])
            action = int(rng.choice(legal))
            start = time.perf_counter()
            _obs, _r, terminated, truncated, info = env.step(action)
            cost[phase].append(time.perf_counter() - start)
            if terminated or truncated:
                break
        env.close()

    print(f"  {'phase':<18}{'steps':>8}{'mean us':>10}{'median':>9}{'p95':>9}")
    total_n = 0
    total_t = 0.0
    for phase in sorted(cost, key=lambda p: -sum(cost[p])):
        micros = np.array(cost[phase]) * 1e6
        total_n += micros.size
        total_t += float(micros.sum())
        print(
            f"  {PHASE_NAMES.get(phase, phase):<18}{micros.size:>8}{micros.mean():>10.0f}"
            f"{np.median(micros):>9.0f}{np.percentile(micros, 95):>9.0f}",
        )
    mean = total_t / max(1, total_n)
    print(f"\n  overall: {mean:.0f} us/step -> {1e6 / mean:,.0f} steps/s (one env)")

    env = Sts2RunEnv(seed="PROBE00001", max_floors=64, max_episode_steps=4000)
    env.reset()
    handle = env._run_handle
    buf = (ctypes.c_int * native.RUN_OBS_SIZE)()
    start = time.perf_counter()
    for i in range(args.steps):
        clone = native.run_clone(handle, 1, i, buf)
        if clone >= 0:
            native.run_destroy(clone)
    elapsed = time.perf_counter() - start
    print(f"  clone + destroy: {args.steps / elapsed:,.0f} /s (resampling hidden state)")
    print(
        "\n  PLAN.md's AlphaZero target is 1e5-1e6 transitions/s/core. A tree search "
        "spends its budget on clone+step, and CLONE is the cheap half -- the step is "
        "where the gap is. The handle pool caps CONCURRENT runs at 256, which is fine "
        "for clone-simulate-destroy and not fine for holding a node per handle.",
    )
    env.close()


SECTIONS = {
    "spaces": section_spaces,
    "observation": section_observation,
    "actions": section_actions,
    "throughput": section_throughput,
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--steps", type=int, default=2000)
    parser.add_argument("--section", choices=[*SECTIONS, "all"], default="all")
    args = parser.parse_args()

    rng = np.random.default_rng(0)
    for name, fn in SECTIONS.items():
        if args.section in (name, "all"):
            fn(args, rng)


if __name__ == "__main__":
    main()
