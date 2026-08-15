"""Run repeatable STS2MCP-vs-emulator validation suites."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import validate_real_game_trace

DEFAULT_DIRECT_ACTIONS = [5]
PASSIVE_BOSS_CASES = {
    "knowledge-demon": [5, 5, 5],
    "soul-fysh": [5, 5, 5],
    "insatiable": [5, 5, 5],
    "aeonglass": [5, 5, 5],
    "kaiser-crab": [5, 5, 5],
}


@dataclass(frozen=True)
class SweepCase:
    suite: str
    encounter: str
    actions: list[int]

    @property
    def label(self) -> str:
        return f"{self.suite}:{self.encounter}"


def direct_cases() -> list[SweepCase]:
    return [
        SweepCase("direct", encounter, DEFAULT_DIRECT_ACTIONS)
        for encounter in validate_real_game_trace.LIVE_ENCOUNTER_BY_EMULATOR
    ]


def passive_boss_cases() -> list[SweepCase]:
    return [
        SweepCase("passive-boss", encounter, actions)
        for encounter, actions in PASSIVE_BOSS_CASES.items()
    ]


def selected_cases(args: argparse.Namespace) -> list[SweepCase]:
    cases: list[SweepCase] = []
    if args.suite in ("direct", "all"):
        cases.extend(direct_cases())
    if args.suite in ("passive-boss", "all"):
        cases.extend(passive_boss_cases())

    if args.encounter:
        requested = set(args.encounter)
        cases = [case for case in cases if case.encounter in requested]
        missing = requested.difference(case.encounter for case in cases)
        if missing:
            raise ValueError(
                f"Unknown encounter(s) for suite {args.suite}: {sorted(missing)}",
            )

    return cases


def run_case(
    case: SweepCase,
    args: argparse.Namespace,
    output_dir: Path,
    run_id: str,
) -> dict[str, Any]:
    last_result: dict[str, Any] | None = None
    script = Path(__file__).with_name("validate_real_game_trace.py")
    for attempt in range(1, args.retries + 2):
        start_seed = (
            f"{args.start_seed_prefix}_{run_id}_{case.suite}_{case.encounter}_{attempt}"
        )
        command = [
            sys.executable,
            str(script),
            "--base-url",
            args.base_url,
            "--start-seed",
            start_seed,
            "--encounter",
            case.encounter,
            "--output-dir",
            str(output_dir),
        ]
        if args.ignore_hand_order:
            command.append("--ignore-hand-order")
        if case.actions:
            command.append("--actions")
            command.extend(str(action) for action in case.actions)
        if args.seed_search_limit is not None:
            command.extend(["--seed-search-limit", str(args.seed_search_limit)])
        if args.delay is not None:
            command.extend(["--delay", str(args.delay)])

        completed = subprocess.run(
            command,
            cwd=Path(__file__).parent.parent,
            text=True,
            capture_output=True,
            check=False,
        )
        last_result = {
            "suite": case.suite,
            "encounter": case.encounter,
            "actions": case.actions,
            "attempt": attempt,
            "returncode": completed.returncode,
            "stdout": completed.stdout,
            "stderr": completed.stderr,
        }
        if completed.returncode == 0:
            try:
                last_result["result"] = json.loads(completed.stdout)
            except json.JSONDecodeError:
                last_result["result"] = None
            return last_result

    assert last_result is not None
    return last_result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--base-url",
        default=validate_real_game_trace.trace_real_game.DEFAULT_BASE_URL,
    )
    parser.add_argument(
        "--suite",
        choices=("direct", "passive-boss", "all"),
        default="all",
        help="Validation suite to run. Direct cases use one passive end turn.",
    )
    parser.add_argument(
        "--encounter",
        action="append",
        default=[],
        help="Run only this encounter; can be passed multiple times.",
    )
    parser.add_argument("--output-dir", type=Path, default=None)
    parser.add_argument("--start-seed-prefix", default="FORCE_sweep")
    parser.add_argument("--seed-search-limit", type=int, default=500000)
    parser.add_argument("--delay", type=float, default=0.25)
    parser.add_argument(
        "--ignore-hand-order",
        action="store_true",
        help="Match opening hand as an unordered multiset.",
    )
    parser.add_argument(
        "--retries",
        type=int,
        default=1,
        help="Retry each case this many times after an initial failure.",
    )
    parser.add_argument(
        "--continue-on-failure",
        action="store_true",
        help="Run remaining cases after a failure instead of stopping immediately.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print selected cases only.",
    )
    args = parser.parse_args()

    cases = selected_cases(args)
    if not cases:
        raise ValueError("No validation cases selected.")

    if args.dry_run:
        print(
            json.dumps(
                [
                    {
                        "suite": case.suite,
                        "encounter": case.encounter,
                        "actions": case.actions,
                    }
                    for case in cases
                ],
                indent=2,
            ),
        )
        return

    run_id = time.strftime("%Y%m%d_%H%M%S")
    output_dir = (
        args.output_dir
        or Path(tempfile.gettempdir()) / "sts2-validation-sweep" / run_id
    )
    output_dir.mkdir(parents=True, exist_ok=True)

    results = []
    failed = False
    for index, case in enumerate(cases, start=1):
        print(f"[{index}/{len(cases)}] {case.label} actions={case.actions}", flush=True)
        result = run_case(case, args, output_dir, run_id)
        results.append(result)
        if result["returncode"] == 0:
            print(f"PASS {case.label}", flush=True)
            continue

        failed = True
        print(f"FAIL {case.label}", flush=True)
        print(result["stderr"], file=sys.stderr, end="")
        print(result["stdout"], file=sys.stderr, end="")
        if not args.continue_on_failure:
            break

    summary = {
        "output_dir": str(output_dir),
        "total": len(results),
        "passed": sum(1 for result in results if result["returncode"] == 0),
        "failed": sum(1 for result in results if result["returncode"] != 0),
        "results": results,
    }
    (output_dir / "summary.json").write_text(
        json.dumps(summary, indent=2),
        encoding="utf-8",
    )
    print(json.dumps({k: v for k, v in summary.items() if k != "results"}, indent=2))

    if failed:
        raise SystemExit(1)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"validate_real_game_sweep.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
