#!/usr/bin/env python3
"""A throwaway determinized MCTS, to stress the search path before committing to it.

Not an agent. The tree is shallow, the value function is a stand-in, and nothing is
trained. The point is to walk the search interface hard enough to find out three things
that a design document cannot answer:

  invariants  does `run_clone` behave the way a tree search needs? A clone must be
              INDEPENDENT (stepping it leaves the original alone), FAITHFUL without
              resampling (same actions, same outcome), and genuinely RESAMPLED with it
              (the hidden future differs while what the agent has already seen does not).

  search      what a simulation actually costs, once it is clone + replay-to-node +
              rollout rather than a single step. This is the number that decides whether
              tree search is viable at the emulator's current speed.

  pressure    the native handle pool holds 256 runs. Clone-simulate-destroy stays far
              under that; a design holding a handle per tree node does not. Worth
              measuring rather than assuming.

    uv run python scripts/mcts_probe.py
    uv run python scripts/mcts_probe.py --sims 200 --section search
"""

from __future__ import annotations

import argparse
import ctypes
import math
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "src"))

from sts2_gym import Sts2RunEnv, native

OBS = native.RUN_OBS_SIZE
ACTIONS = native.RUN_MAX_ACTIONS


class Handle:
    """A native run handle with a scratch observation buffer, freed on exit."""

    def __init__(self, handle: int):
        self.handle = handle
        self.obs = (ctypes.c_int * OBS)()
        self.reward = (ctypes.c_float * 1)()
        self.terminal = (ctypes.c_int * 1)()
        self.truncated = (ctypes.c_int * 1)()

    def clone(self, resample_seed: int | None) -> Handle:
        child = native.run_clone(
            self.handle,
            1 if resample_seed is not None else 0,
            resample_seed or 0,
            self.obs,
        )
        if child < 0:
            raise RuntimeError("clone failed — the handle pool is full")
        return Handle(child)

    def legal(self) -> np.ndarray:
        return np.flatnonzero(np.array(native.run_action_mask(self.handle, ACTIONS), dtype=bool))

    def step(self, action: int) -> tuple[float, bool]:
        native.run_step(
            self.handle, action, -1, self.obs, self.reward, self.terminal, self.truncated,
        )
        return float(self.reward[0]), bool(self.terminal[0] or self.truncated[0])

    def snapshot(self) -> np.ndarray:
        return np.array(native.run_info(self.handle)[:8], dtype=np.int64)

    def close(self) -> None:
        native.run_destroy(self.handle)


def section_invariants(args) -> None:
    print("\n── clone invariants ──")
    env = Sts2RunEnv(seed="MCTS000001", max_floors=64, max_episode_steps=4000)
    env.reset()
    root = Handle(env._run_handle)

    # Walk a little way in so there is real state to copy.
    for _ in range(12):
        legal = root.legal()
        if legal.size == 0:
            break
        root.step(int(legal[0]))
    before = root.snapshot()

    # INDEPENDENT: stepping a clone must not move the original.
    child = root.clone(None)
    for _ in range(6):
        legal = child.legal()
        if legal.size == 0:
            break
        child.step(int(legal[0]))
    after = root.snapshot()
    child.close()
    print(f"  independent : {'ok' if np.array_equal(before, after) else 'FAIL — the parent moved'}")

    # FAITHFUL: two un-resampled clones, same actions, same result.
    a, b = root.clone(None), root.clone(None)
    for _ in range(10):
        legal = a.legal()
        if legal.size == 0:
            break
        action = int(legal[0])
        a.step(action)
        b.step(action)
    same = np.array_equal(a.snapshot(), b.snapshot())
    a.close()
    b.close()
    print(f"  faithful    : {'ok' if same else 'FAIL — identical clones diverged'}")

    # RESAMPLED: different seeds should give different futures, at least sometimes.
    futures = set()
    for seed in range(24):
        world = root.clone(seed)
        for _ in range(14):
            legal = world.legal()
            if legal.size == 0:
                break
            world.step(int(legal[0]))
        futures.add(tuple(world.snapshot().tolist()))
        world.close()
    print(
        f"  resampled   : {len(futures)} distinct futures from 24 determinizations"
        f" {'ok' if len(futures) > 1 else '-- FAIL, resampling changed nothing'}",
    )
    env.close()


class Node:
    __slots__ = ("actions", "children", "value", "visits")

    def __init__(self, actions: np.ndarray):
        self.actions = actions
        self.children: dict[int, Node] = {}
        self.visits = 0
        self.value = 0.0


def uct(node: Node, child: Node, c: float) -> float:
    if child.visits == 0:
        return math.inf
    return child.value / child.visits + c * math.sqrt(math.log(node.visits) / child.visits)


def simulate(root_handle: Handle, root: Node, seed: int, depth: int, rollout: int, rng) -> int:
    """One determinization: clone, walk the tree, roll out, back up. Returns steps taken."""
    world = root_handle.clone(seed)
    steps = 0
    try:
        path = [root]
        node = root
        # Selection + expansion, replaying into the cloned world as we descend.
        for _ in range(depth):
            legal = world.legal()
            if legal.size == 0:
                break
            untried = [a for a in legal.tolist() if a not in node.children]
            if untried:
                action = int(rng.choice(untried))
                _reward, done = world.step(action)
                steps += 1
                child = Node(legal)
                node.children[action] = child
                path.append(child)
                if done:
                    break
                node = child
                break
            action = max(node.children, key=lambda a: uct(node, node.children[a], args_c))
            _reward, done = world.step(action)
            steps += 1
            node = node.children[action]
            path.append(node)
            if done:
                break

        # Rollout: random legal play to a horizon, scored by how far it got.
        total = 0.0
        for _ in range(rollout):
            legal = world.legal()
            if legal.size == 0:
                break
            reward, done = world.step(int(rng.choice(legal)))
            steps += 1
            total += reward
            if done:
                break

        info = world.snapshot()
        value = float(info[1]) + total  # floor reached, plus shaped combat reward
        for visited in path:
            visited.visits += 1
            visited.value += value
    finally:
        world.close()
    return steps


args_c = 1.4


def section_search(args) -> None:
    print("\n── search cost ──")
    rng = np.random.default_rng(0)
    env = Sts2RunEnv(seed="MCTS000001", max_floors=64, max_episode_steps=4000)
    env.reset()
    root_handle = Handle(env._run_handle)
    for _ in range(10):
        legal = root_handle.legal()
        if legal.size == 0:
            break
        root_handle.step(int(legal[0]))

    root = Node(root_handle.legal())
    start = time.perf_counter()
    total_steps = 0
    for sim in range(args.sims):
        total_steps += simulate(root_handle, root, sim, args.depth, args.rollout, rng)
    elapsed = time.perf_counter() - start

    print(f"  {args.sims} simulations in {elapsed:.2f}s")
    print(f"  {args.sims / elapsed:>10,.0f} simulations/s")
    print(f"  {total_steps / elapsed:>10,.0f} steps/s inside search")
    print(f"  {total_steps / args.sims:>10.1f} steps per simulation")
    print(f"  {1e6 * elapsed / args.sims:>10.0f} us per simulation")
    best = sorted(root.children.items(), key=lambda kv: -kv[1].visits)[:4]
    print("  root visits: " + ", ".join(f"a{a}={n.visits}" for a, n in best))
    print(
        "\n  A simulation is clone + descend + roll out, so it costs DEPTH steps, not one."
        "\n  At the emulator's ~72us/step that is the ceiling on any tree search, and it"
        "\n  is why the step path matters more than the clone path.",
    )
    env.close()


def section_pressure(args) -> None:
    print("\n── handle pool pressure ──")
    env = Sts2RunEnv(seed="MCTS000001", max_floors=64, max_episode_steps=4000)
    env.reset()
    root = Handle(env._run_handle)

    live: list[Handle] = []
    try:
        while True:
            live.append(root.clone(len(live)))
    except RuntimeError:
        pass
    print(f"  clones held at once before the pool refused: {len(live)}")
    for handle in live:
        handle.close()
    print("  released; a clone-simulate-destroy search never approaches this.")
    print(
        "  A node-per-handle design would: a tree of a few thousand nodes is two orders"
        "\n  of magnitude past the pool.",
    )
    env.close()


def play_run(seed: str, sims: int, depth: int, rollout: int, rng, use_search: bool) -> int:
    """Play one run to its end, and report the floor it died on."""
    env = Sts2RunEnv(seed=seed, max_floors=64, max_episode_steps=4000)
    env.reset()
    handle = Handle(env._run_handle)
    try:
        for _ in range(2000):
            legal = handle.legal()
            if legal.size == 0:
                break
            if use_search:
                root = Node(legal)
                for sim in range(sims):
                    simulate(handle, root, sim, depth, rollout, rng)
                action = max(root.children, key=lambda a: root.children[a].visits)
            else:
                action = int(rng.choice(legal))
            _reward, done = handle.step(action)
            if done:
                break
        return int(handle.snapshot()[1])
    finally:
        env.close()


def section_play(args) -> None:
    """Check whether the search actually beats random -- the whole path, end to end.

    Not a claim about the agent -- the value function is "how far did the rollout get"
    and the tree is tiny. It is a claim about the PLUMBING: if determinized search and
    random play are indistinguishable, something in the clone, the mask or the value is
    not wired up, and that is worth knowing before any of it is built on.
    """
    print("\n── search vs random, end to end ──")
    seeds = [f"PLAY{i:06d}" for i in range(args.runs)]

    results = {}
    for label, use_search in (("random", False), ("search", True)):
        rng = np.random.default_rng(0)
        start = time.perf_counter()
        floors = [
            play_run(seed, args.sims, args.depth, args.rollout, rng, use_search)
            for seed in seeds
        ]
        elapsed = time.perf_counter() - start
        results[label] = floors
        print(
            f"  {label:<7} floors: mean {np.mean(floors):5.1f}  median {np.median(floors):4.0f}"
            f"  max {max(floors):3d}   ({elapsed:.1f}s for {len(seeds)} runs)",
        )

    lift = np.mean(results["search"]) - np.mean(results["random"])
    print(f"\n  search - random = {lift:+.1f} floors on the same {args.runs} seeds")
    if lift <= 0:
        print(
            "  NOT an improvement. With a floor-count value and a tiny tree that is a"
            "\n  plausible outcome, but it is also what a broken clone or a misread mask"
            "\n  looks like -- worth separating before trusting the path.",
        )


SECTIONS = {
    "invariants": section_invariants,
    "search": section_search,
    "pressure": section_pressure,
    "play": section_play,
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sims", type=int, default=150)
    parser.add_argument("--depth", type=int, default=8)
    parser.add_argument("--rollout", type=int, default=12)
    parser.add_argument("--runs", type=int, default=12, help="seeds for the play section")
    parser.add_argument("--section", choices=[*SECTIONS, "all"], default="all")
    args = parser.parse_args()

    for name, fn in SECTIONS.items():
        if args.section in (name, "all"):
            fn(args)


if __name__ == "__main__":
    main()
