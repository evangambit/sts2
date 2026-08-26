#!/usr/bin/env python3
"""Measure which combat positions the run engine actually produces, by encounter class.

The position sampler needs to know what CO-OCCURS: which decks meet which encounters.
Any prior about that is checkable against the run engine directly, so check it rather
than guessing, and re-run this after any change to rewards, map generation or routing.

    uv run python scripts/measure_positions.py 300

What it does NOT measure, and why:

- **HP.** Max HP is inflated at reset so a random policy's death on floor 4 does not
  truncate every route before it reaches an elite. That is the point of the harness and
  it also makes HP unobservable here.
- **Relic counts, honestly.** The random router reaches the boss having fought a median
  of two elites while holding a median of two non-starter relics -- Neow plus the
  guaranteed treasure room should supply about that many before any elite drops one, so
  something here leaves the relic-reward screen without claiming. Treat the relic column
  as a lower bound until that is run down.

Random-but-legal choice is also not a competent policy: it takes roughly three card
rewards in four, routes without preferring elites, and rarely spends a rest on an
upgrade. The floors and the card counts survive that; the rest is indicative.
"""

from __future__ import annotations

import collections
import random
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from sts2_gym import native
from sts2_gym.run_env import (
    NODE_BOSS,
    NODE_ELITE,
    NODE_NORMAL,
    PHASE_COMBAT,
    Sts2RunEnv,
)

STARTER = collections.Counter({472: 5, 131: 4, 30: 1, 10001: 1})
STARTER_RELICS = 1  # Burning Blood; the run's relic list includes it
ACT_ONE = ("overgrowth", "underdocks")
CLASSES = ("weak", "normal", "elite", "boss")


def non_starter_cards(deck) -> int:
    return sum((collections.Counter(c["card_id"] for c in deck) - STARTER).values())


def encounter_class(node_type: int, combats_done: int) -> str | None:
    """Classify a combat room; the first three of act 1 draw from the weak pool."""
    if node_type == NODE_BOSS:
        return "boss"
    if node_type == NODE_ELITE:
        return "elite"
    if node_type == NODE_NORMAL:
        return "weak" if combats_done < 3 else "normal"
    return None


def deciles(values: list[int]) -> str:
    xs = sorted(values)
    at = lambda f: xs[min(len(xs) - 1, int(f * len(xs)))]  # noqa: E731
    return f"{at(.1)}-{at(.5)}-{at(.9)}"


def pearson(xs: list[int], ys: list[int]) -> float:
    n = len(xs)
    if n < 2:
        return float("nan")
    mx, my = sum(xs) / n, sum(ys) / n
    num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    dx = sum((x - mx) ** 2 for x in xs) ** 0.5
    dy = sum((y - my) ** 2 for y in ys) ** 0.5
    return num / (dx * dy) if dx and dy else float("nan")


def collect(runs: int) -> tuple[dict[str, list[tuple[int, ...]]], list[int]]:
    rows: dict[str, list[tuple[int, ...]]] = collections.defaultdict(list)
    ends: list[int] = []
    for run in range(runs):
        env = Sts2RunEnv(seed=run, max_floors=16, max_episode_steps=4000)
        _, info = env.reset(seed=run)
        # A lethal blow terminates before any post-step top-up could fire, so buy the
        # headroom up front instead of healing after the fact.
        _, info = env.debug_gain_max_hp(2000)
        rng = random.Random(run)  # noqa: S311
        combats_done = elites_done = 0
        last_node: int | None = None
        in_combat = False
        for _ in range(4000):
            legal = [i for i, m in enumerate(env.action_masks()) if m]
            if not legal:
                break
            if info["phase"] == PHASE_COMBAT and not in_combat:
                in_combat = True
                last_node = info["current_node_type"]
                klass = encounter_class(last_node, combats_done)
                # max_floors does not stop the run at the act boundary, so a "normal
                # combat at floor 19" is act-2 data wearing an act-1 label.
                if klass and info["act"] in ACT_ONE:
                    rows[klass].append(
                        (
                            info["floor"],
                            non_starter_cards(info["deck"]),
                            max(0, len([r for r in info["relics"] if r]) - STARTER_RELICS),
                            combats_done,
                            elites_done,
                        ),
                    )
            elif info["phase"] != PHASE_COMBAT and in_combat:
                in_combat = False
                combats_done += 1
                elites_done += last_node == NODE_ELITE
            _, _, term, trunc, info = env.step(rng.choice(legal))
            if info["player_hp"] < info["player_max_hp"] // 2:
                native.run_debug_set_hp(
                    env._run_handle, info["player_max_hp"], info["player_max_hp"], env._run_obs_buf,
                )
                info = env._info()
            if term or trunc:
                ends.append(info["floor"])
                break
        env.close()
    return rows, ends


def main() -> None:
    runs = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 300
    rows, ends = collect(runs)

    print(
        f"{runs} act-1 runs, random-but-legal choices, max HP inflated; "
        f"median end floor {statistics.median(ends) if ends else float('nan'):.0f}\n",
    )
    header = (
        f"{'class':8} {'n':>5}  {'floor':>12}  {'non-starter cards':>18}  "
        f"{'non-starter relics':>19}"
    )
    print(header)
    print("-" * len(header))
    for klass in CLASSES:
        seen = rows[klass]
        if not seen:
            print(f"{klass:8} {0:>5}  (never reached)")
            continue
        print(
            f"{klass:8} {len(seen):>5}  {deciles([r[0] for r in seen]):>12}  "
            f"{deciles([r[1] for r in seen]):>18}  {deciles([r[2] for r in seen]):>19}",
        )

    every = [r for seen in rows.values() for r in seen]
    print("\nIs the structure 'cards ~ combats fought, relics ~ elites fought'?")
    for label, x, y in (
        ("cards  vs combats", 3, 1),
        ("cards  vs elites ", 4, 1),
        ("relics vs combats", 3, 2),
        ("relics vs elites ", 4, 2),
        ("cards  vs floor  ", 0, 1),
        ("relics vs floor  ", 0, 2),
    ):
        print(f"  {label} : {pearson([r[x] for r in every], [r[y] for r in every]):+.2f}")

    boss = rows["boss"]
    if boss:
        print("\nAt boss entry, what the random router actually did:")
        print(f"  combats fought : {deciles([r[3] for r in boss])}")
        print(f"  elites  fought : {deciles([r[4] for r in boss])}")
        print(f"  relics held    : {deciles([r[2] for r in boss])}")


if __name__ == "__main__":
    main()
