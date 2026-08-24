"""Find seeds whose Neow offers a blessing no committed trace has captured.

A full-run capture costs a few minutes of live game, so which seed to spend it on is
worth choosing rather than rolling. The tenth trace is the argument: it was taken for no
reason except that it would hold a relic the other nine did not, it diverged on the first
combat reward of the run, and four defects came out of it -- three of which had nothing
to do with the relic and simply sat on paths no earlier capture had walked.

Neow's three options are seed-deterministic and the emulator models the stream, so the
offer is knowable with no game running. Verified against every committed trace: the
predicted three match the live capture's own option titles on all ten.

    uv run python scripts/screen_neow_seeds.py --count 6
    uv run python scripts/screen_neow_seeds.py --count 6 --want LeadPaperweight,HeftyTablet

**Offered is not taken.** `trace_real_game_run.py`'s default Neow policy takes the first
option whose text avoids "choose", "transform", "upgrade" and friends, which makes every
blessing with a pickup CHOICE unreachable -- Lead Paperweight and Hefty Tablet were both
offered to traces already committed and neither was ever taken. Pass the index this
prints to `--neow-option` to take one anyway.
"""

from __future__ import annotations

import argparse
import json
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from sts2_gym import Sts2RunEnv  # noqa: E402

# The game's own alphabet (SeedHelper): no I and no O, which it folds to 1 and 0.
SEED_ALPHABET = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"
SEED_LENGTH = 10
TRACE_DIR = ROOT / "tests" / "fixtures" / "run_trace"
RELICS = ROOT / "src" / "Sts2Emulator" / "Generated" / "Relics.g.cs"


def relic_names() -> dict[int, str]:
    """Relic id to name, read from the generated table rather than restated."""
    return {
        int(match.group(1)): match.group(2)
        for match in re.finditer(r'Id: (\d+), Name: "([^"]+)"', RELICS.read_text())
    }


def neow_options(seed: str, names: dict[int, str]) -> list[str]:
    env = Sts2RunEnv(seed=seed)
    try:
        _, info = env.reset()
        return [names.get(relic, f"?{relic}") for relic in info["neow_options"] if relic]
    finally:
        env.close()


def captured_relics() -> set[str]:
    """Which blessings the committed traces actually TOOK, by their own record."""
    taken: set[str] = set()
    for path in sorted(TRACE_DIR.glob("*.json")):
        trace = json.loads(path.read_text())["trace"]
        for step in trace[:4]:
            message = (step.get("post_result") or {}).get("message") or ""
            if "event option" in message:
                taken.add(message.split(": ", 1)[-1].replace(" ", ""))
                break
    return taken


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--count", type=int, default=6, help="how many seeds to find")
    parser.add_argument(
        "--want",
        default=None,
        help="comma-separated relic names to hold out for, instead of anything uncaptured",
    )
    parser.add_argument("--random-seed", type=int, default=7)
    parser.add_argument(
        "--tries",
        type=int,
        default=4000,
        help="how many seeds to look at before giving up",
    )
    args = parser.parse_args()

    names = relic_names()
    wanted = set(args.want.split(",")) if args.want else None
    covered = captured_relics()
    if wanted is None:
        print(f"already captured: {sorted(covered) or '(none)'}\n")

    rng = random.Random(args.random_seed)  # noqa: S311 - picking test seeds, not crypto
    fresh_seen: set[str] = set()
    found = 0
    for _ in range(args.tries):
        if found >= args.count:
            break
        seed = "".join(rng.choice(SEED_ALPHABET) for _ in range(SEED_LENGTH))
        offered = neow_options(seed, names)
        if wanted is not None:
            fresh = [r for r in offered if r in wanted]
        else:
            fresh = [r for r in offered if r not in covered and r not in fresh_seen]
        if not fresh:
            continue
        fresh_seen.update(fresh)
        found += 1
        picks = ", ".join(
            f"--neow-option {i} -> {relic}" for i, relic in enumerate(offered)
        )
        print(f"{seed}  new={fresh}")
        print(f"    {picks}")

    if found < args.count:
        print(f"\nonly found {found} of {args.count} in {args.tries} seeds")


if __name__ == "__main__":
    main()
