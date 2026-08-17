"""Verify the emulator's act-1 selection against the profile's own run history.

Act 1 is a coin flip between Overgrowth and Underdocks (`ActModel.GetRandomList` ->
`rng.NextItem` on the `"act_selection"` stream), so a single captured run cannot tell
a correct model from a lucky guess. The profile keeps one record per finished run in
`saves/history/*.run`, and each record carries the `seed` and the `acts` it rolled —
hundreds of free (seed -> act) ground-truth pairs, no game driving required.

That makes this the cheapest verification we have of the **Underdocks** branch: the
history holds real Underdocks act 1s, which no `current_run.save` capture so far does.

    python scripts/verify_act_selection.py                # installed build only
    python scripts/verify_act_selection.py --all-builds    # every build, for context
    python scripts/verify_act_selection.py --build v0.110.1
    python scripts/verify_act_selection.py --fixture tests/fixtures/act_selection/v0.107.1.json

Only the installed build's runs are ground truth for the emulator as it stands. Older
records are ground truth for *their* patch: the act pool, the stream, and the profile's
unlock state all moved over time, and `GetRandomList` force-selects an unlocked-but-
undiscovered alt act instead of rolling at all — so early runs are expected to diverge
and a low match rate there is not a bug. `--all-builds` prints them for context only.

Exit code 0 when every checked run matches.

NOTE: this reads local play history. It prints seeds and acts; it does not read or
print account ids, and `--save-fixture` writes only (seed, act) pairs plus the build
stamp, so a committed fixture carries no profile data.
"""

from __future__ import annotations

import argparse
import ctypes
import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

import game_version

from sts2_gym import game_seed, native

HISTORY_GLOB = (
    "Library/Application Support/SlayTheSpire2/steam/*/profile*/saves/history/*.run"
)

LIST_GENERATION_SUMMARY = 14  # [act, boss_encounter_id, map_node_count]

ACT_NAMES = {1: "OVERGROWTH", 2: "UNDERDOCKS"}


def load_history(root: Path) -> list[dict[str, Any]]:
    """Read every history record, newest last.

    A history record is a different (smaller) shape than `current_run.save`: it has a
    top-level `seed`, `acts` as plain id strings, and no room or map detail. Act
    selection is all we can check from it — but we can check it hundreds of times.
    """
    runs: list[dict[str, Any]] = []
    for path in sorted(root.glob(HISTORY_GLOB)):
        raw = path.read_text(errors="replace")
        try:
            record = json.loads(raw[raw.index("{") :])
        except ValueError:
            print(f"  ! skipping unparseable history record {path.name}")
            continue
        acts = record.get("acts") or []
        seed = record.get("seed")
        if not acts or not seed:
            continue
        runs.append(
            {
                "seed": str(seed),
                "act": str(acts[0]).replace("ACT.", ""),
                "build": record.get("build_id"),
                "mode": record.get("game_mode"),
                "ascension": record.get("ascension"),
                "modifiers": record.get("modifiers") or [],
                "start": record.get("start_time") or 0,
                "file": path.name,
            },
        )
    return runs


def load_fixture(path: Path) -> list[dict[str, Any]]:
    """Read a committed (seed, act) fixture back into the shape `predict` expects."""
    fixture = json.loads(path.read_text())
    return [
        {
            "seed": str(run["seed"]),
            "act": run["act"],
            "build": fixture.get("build_id"),
            "mode": None,
            "ascension": None,
            "modifiers": [],
            "start": 0,
            "file": path.name,
        }
        for run in fixture["runs"]
    ]


def predict(runs: list[dict[str, Any]]) -> None:
    """Fill in each run's emulator-predicted act 1, in place."""
    handle = native.run_create()
    obs = (ctypes.c_int * native.RUN_OBS_SIZE)()
    try:
        for run in runs:
            native.run_reset(handle, run["seed"], obs)
            act, _boss, _nodes = native.run_state_list(
                handle,
                LIST_GENERATION_SUMMARY,
                3,
            )
            run["emulator"] = ACT_NAMES.get(act, f"<{act}>")
            run["ok"] = run["emulator"] == run["act"]
    finally:
        native.run_destroy(handle)


def report(label: str, runs: list[dict[str, Any]]) -> bool:
    live = Counter(run["act"] for run in runs)
    hits = sum(run["ok"] for run in runs)
    print(
        f"\n=== {label} — {len(runs)} runs "
        f"({live['OVERGROWTH']} Overgrowth / {live['UNDERDOCKS']} Underdocks) ===",
    )
    for run in runs:
        if not run["ok"]:
            print(
                f"  MISMATCH {run['seed']:12} gen {game_seed(run['seed']):>10}  "
                f"live {run['act']:11} emulator {run['emulator']:11} "
                f"(a{run['ascension']} {run['mode']} {run['file']})",
            )
    print(f"  -> {hits}/{len(runs)} match")
    return hits == len(runs)


def summarize_other_builds(runs: list[dict[str, Any]], installed: str | None) -> None:
    by_build: dict[str | None, list[dict[str, Any]]] = {}
    for run in runs:
        by_build.setdefault(run["build"], []).append(run)
    print("\n=== other builds (context only — ground truth for their own patch) ===")
    for build, group in sorted(
        by_build.items(),
        key=lambda kv: min(r["start"] for r in kv[1]),
    ):
        if build == installed:
            continue
        hits = sum(run["ok"] for run in group)
        print(f"  {build!s:10} {hits:4}/{len(group):<4} {hits / len(group):6.1%}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path.home(),
        help="directory the history glob is relative to (default: home)",
    )
    parser.add_argument(
        "--build",
        default=None,
        help="check this build id instead of the installed one, e.g. v0.107.1",
    )
    parser.add_argument(
        "--all-builds",
        action="store_true",
        help="check every build, not just the installed one",
    )
    parser.add_argument(
        "--fixture",
        type=Path,
        default=None,
        help="re-check a committed fixture offline, with no profile or game needed",
    )
    parser.add_argument(
        "--save-fixture",
        type=Path,
        default=None,
        help="write the checked (seed, act) pairs to a committable fixture and exit",
    )
    args = parser.parse_args()

    if args.fixture is not None:
        runs = load_fixture(args.fixture)
        print(f"fixture : {args.fixture} ({len(runs)} runs)")
        predict(runs)
        matched = report(f"fixture {args.fixture.name}", runs)
        raise SystemExit(0 if matched else 1)

    runs = load_history(args.root)
    if not runs:
        raise SystemExit(
            f"No history records under {args.root / HISTORY_GLOB}. "
            "Finish (or abandon) a run in game first.",
        )

    installed = args.build or game_version.release_string()
    print(f"history : {len(runs)} runs")
    print(f"game    : {game_version.describe(game_version.detect())}")

    predict(runs)

    if args.all_builds:
        checked = runs
        label = "all builds"
    else:
        if installed is None:
            raise SystemExit(
                "Could not detect the installed game version (launch the game once, "
                "or pass --build). Use --all-builds to check everything regardless.",
            )
        checked = [run for run in runs if run["build"] == installed]
        label = f"build {installed}"
        if not checked:
            raise SystemExit(
                f"No history records for build {installed}. Play a run on this patch, "
                "or pass --build/--all-builds.",
            )

    if args.save_fixture is not None:
        args.save_fixture.parent.mkdir(parents=True, exist_ok=True)
        args.save_fixture.write_text(
            json.dumps(
                {
                    "_comment": (
                        "Ground truth for act-1 selection, distilled from the local "
                        "profile's run history by verify_act_selection.py. Seed and "
                        "rolled act only — no account id, timestamps or play history."
                    ),
                    "game": game_version.detect(),
                    "build_id": None if args.all_builds else installed,
                    "runs": [
                        {"seed": run["seed"], "act": run["act"]} for run in checked
                    ],
                },
                indent=2,
            )
            + "\n",
        )
        print(f"wrote fixture -> {args.save_fixture} ({len(checked)} runs)")
        raise SystemExit(0)

    matched = report(label, checked)
    if not args.all_builds:
        summarize_other_builds(runs, installed)

    underdocks = [run for run in checked if run["act"] == "UNDERDOCKS"]
    print("\n" + "=" * 60)
    print(
        f"  act selection {'PASS' if matched else 'FAIL'} on {label}: "
        f"{sum(run['ok'] for run in checked)}/{len(checked)} runs, "
        f"{len(underdocks)} of them Underdocks",
    )
    if matched and underdocks:
        print(
            "  The Underdocks *branch* of act selection is verified. Its rooms and map\n"
            "  are not — that needs a live Underdocks capture (verify_run_generation.py).\n"
            "  Seeds to capture with, from this history:",
        )
        for run in underdocks[:5]:
            print(f"    {run['seed']:12} gen {game_seed(run['seed']):>10}")
    raise SystemExit(0 if matched else 1)


if __name__ == "__main__":
    main()
