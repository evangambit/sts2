"""Compare emulator and STS2MCP trace JSON files step-by-step."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

DEFAULT_FIELDS = [
    "player.hp",
    "player.max_hp",
    "player.block",
    "player.energy",
    "player.max_energy",
    "player.draw_pile_count",
    "player.discard_pile_count",
    "player.exhaust_pile_count",
]


def get_path(value: Any, dotted_path: str) -> Any:
    current = value
    for part in dotted_path.split("."):
        if current is None:
            return None
        if isinstance(current, list):
            current = current[int(part)]
        elif isinstance(current, dict):
            current = current.get(part)
        else:
            return None
    return current


def load_trace(path: Path) -> list[dict[str, Any]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    return load_trace_from_payload(payload, str(path))


def load_trace_from_payload(
    payload: dict[str, Any],
    source: str = "<payload>",
) -> list[dict[str, Any]]:
    trace = payload.get("trace")
    if not isinstance(trace, list):
        raise ValueError(f"{source} does not contain a top-level trace list")
    return trace


def summary(step: dict[str, Any]) -> dict[str, Any]:
    value = step.get("summary")
    if not isinstance(value, dict):
        raise ValueError(f"Trace step {step.get('step')} has no summary object")
    return value


def compare(
    left: list[dict[str, Any]],
    right: list[dict[str, Any]],
    fields: list[str],
) -> list[str]:
    diffs: list[str] = []
    for index, (left_step, right_step) in enumerate(zip(left, right)):
        left_summary = summary(left_step)
        right_summary = summary(right_step)
        left_dead = (get_path(left_summary, "player.hp") or 0) <= 0
        right_dead = (get_path(right_summary, "player.hp") or 0) <= 0
        if left_dead and right_dead:
            for field in ("player.hp", "player.max_hp", "player.block"):
                left_value = get_path(left_summary, field)
                right_value = get_path(right_summary, field)
                if left_value != right_value:
                    diffs.append(
                        f"step {index} field {field}: left={left_value!r} right={right_value!r}",
                    )
            continue

        for field in fields:
            left_value = get_path(left_summary, field)
            right_value = get_path(right_summary, field)
            if left_value != right_value:
                diffs.append(
                    f"step {index} field {field}: left={left_value!r} right={right_value!r}",
                )

        left_enemies = left_summary.get("enemies") or []
        right_enemies = right_summary.get("enemies") or []
        if len(left_enemies) != len(right_enemies):
            diffs.append(
                f"step {index} enemy count: left={len(left_enemies)} right={len(right_enemies)}",
            )
            continue
        for enemy_index, (left_enemy, right_enemy) in enumerate(
            zip(left_enemies, right_enemies),
        ):
            diffs.extend(
                (
                    "step "
                    f"{index} enemy {enemy_index} {field}: "
                    f"left={left_enemy.get(field)!r} right={right_enemy.get(field)!r}"
                )
                for field in ("hp", "max_hp", "block")
                if left_enemy.get(field) != right_enemy.get(field)
            )
    if len(left) != len(right):
        diffs.append(f"trace length: left={len(left)} right={len(right)}")
    return diffs


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("left", type=Path)
    parser.add_argument("right", type=Path)
    parser.add_argument(
        "--field",
        action="append",
        default=[],
        help="Additional dotted summary field",
    )
    parser.add_argument("--left-start", type=int, default=0)
    parser.add_argument("--right-start", type=int, default=0)
    parser.add_argument("--max-diffs", type=int, default=20)
    args = parser.parse_args()

    fields = [*DEFAULT_FIELDS, *args.field]
    left = load_trace(args.left)[args.left_start :]
    right = load_trace(args.right)[args.right_start :]
    diffs = compare(left, right, fields)
    if diffs:
        print(f"Trace mismatch: {len(diffs)} difference(s)")
        for diff in diffs[: args.max_diffs]:
            print(diff)
        raise SystemExit(1)
    print("Traces match on configured fields.")


if __name__ == "__main__":
    main()
