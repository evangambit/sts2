"""Verify combat *starts* against the live game, across many seeds and encounters.

Run generation gets swept dozens of seeds at a time; combat had exactly one committed
capture ("ABCDEF" into CorpseSlugsWeak), all of Underdocks was unobserved, and enemy HP
and opening intents rested on a single two-enemy sample. Same headless loop as
`capture_sweep.py`, pointed at the other half of the emulator.

    python scripts/combat_sweep.py --count 3                    # 3 seeds x default set
    python scripts/combat_sweep.py --encounters corpse-slugs seapunk --count 5
    python scripts/combat_sweep.py --act underdocks --count 4 --save-fixtures

Per (seed, encounter) it embarks a fresh A8 run, jumps straight into the encounter with
`debug_start_encounter`, and compares what the game deals against the emulator:

  deck   — the whole shuffled deck IN ORDER (hand + draw pile), the strongest signal
           there is: 11 cards in the right order is 1 in 13,860 by luck
  enemies— count, HP and max HP, which is the Niche stream and the unique-HP rule
  intent — each enemy's opening intent, which is the MonsterAi stream
  player — HP/max HP, a cheap guard that both sides describe the same A8 fight

**Jump immediately and touch nothing on the way.** The direct combat env assumes fresh
per-stream RNG (CallCount 0), which only holds for a run's first combat — so the sweep
never answers Neow, never enters a room, and embarks a new run per encounter rather than
reusing one. That is the only real constraint: **normal-pool encounters do NOT need
three easy fights behind them**, because the debug jump names the encounter model
directly, so `--pool normal` works on a fresh run.

Exit code 0 when every section of every capture matches.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import random
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))
sys.path.insert(0, str(Path(__file__).parent))

import game_version

from sts2_gym import game_seed


def _load(name: str) -> ModuleType:
    path = Path(__file__).with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(f"_combat_{name}", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


capture_sweep = _load("capture_sweep")
compare_draw_pile = _load("compare_draw_pile")
validate = _load("validate_real_game_trace")
start_real_game_run = _load("start_real_game_run")
trace_real_game = _load("trace_real_game")

# BOTH pools are reachable here, which is worth understanding before adding encounters.
#
# `completed_combat_rooms in [0,3)` picks the weak variant, but that rule only governs
# what the MAP hands you: `debug_start_encounter` looks the encounter up by class name
# (ModelDb.AllEncounters) and enters a CombatRoom for it directly, so naming
# "NibbitsNormal" gets the normal pool on floor 1 with no combats behind it. The
# emulator matches by passing completed_combat_rooms = -1 for those, which
# validate_real_game_trace.emulator_completed_combat_rooms derives from the name.
#
# What DOES have to hold is stream freshness: the direct combat env assumes every named
# RNG stream is at CallCount 0, which is true of a run's FIRST combat whichever variant
# it is. So: embark, jump straight in, never answer Neow, one run per capture.
WEAK_BY_ACT = {
    "overgrowth": ["nibbit", "slimes", "shrinker-beetle", "fuzzy-wurm-crawler"],
    "underdocks": ["corpse-slugs", "seapunk", "sludge-spinner", "toadpoles"],
}
NORMAL_BY_ACT = {
    "overgrowth": ["nibbits", "large-slimes", "mawler", "vine-shambler"],
    "underdocks": ["sewer-clam", "punch-construct", "fossil-stalker", "haunted-ship"],
}
ENCOUNTERS_BY_ACT = {
    act: [*WEAK_BY_ACT[act], *NORMAL_BY_ACT[act]] for act in WEAK_BY_ACT
}
DEFAULT_ENCOUNTERS = [
    *ENCOUNTERS_BY_ACT["overgrowth"],
    *ENCOUNTERS_BY_ACT["underdocks"],
]


def emulator_summary(seed: str, encounter: str) -> dict[str, Any]:
    """Build the emulator's opening state for this fight, from the derived gen seed."""
    return validate.emulator_initial_summary(game_seed(seed), encounter)


def compare_deck(
    seed: str,
    encounter: str,
    live_state: dict[str, Any],
) -> tuple[bool, str]:
    """Hand and draw pile, in order — the shuffled deck the game dealt."""
    completed = validate.emulator_completed_combat_rooms(encounter)
    gen_seed = game_seed(seed)
    for pile in ("hand", "draw"):
        emu = compare_draw_pile.emulator_pile(gen_seed, encounter, completed, pile)
        live = compare_draw_pile.live_pile(live_state, pile)
        norm = compare_draw_pile.normalize
        if [(norm(n), up) for n, up in emu] != [(norm(n), up) for n, up in live]:
            return False, f"{pile} pile differs ({len(emu)} emu vs {len(live)} live)"
    return True, ""


def compare_enemies(
    live_summary: dict[str, Any],
    emu_summary: dict[str, Any],
) -> tuple[bool, bool, str]:
    """Compare enemy roster/HP and opening intents, reported separately.

    Two different generators: HP comes off the Niche stream (with the unique-HP rule),
    intents off MonsterAi. Collapsing them into one verdict would hide which is wrong.
    """
    live = live_summary.get("enemies") or []
    emu = emu_summary.get("enemies") or []
    if len(live) != len(emu):
        return False, False, f"enemy count {len(emu)} emu vs {len(live)} live"

    hp_ok, intent_ok, notes = True, True, []
    for index, (live_enemy, emu_enemy) in enumerate(zip(live, emu)):
        if (live_enemy.get("hp"), live_enemy.get("max_hp")) != (
            emu_enemy.get("hp"),
            emu_enemy.get("max_hp"),
        ):
            hp_ok = False
            notes.append(
                f"enemy {index} hp {emu_enemy.get('hp')}/{emu_enemy.get('max_hp')} emu "
                f"vs {live_enemy.get('hp')}/{live_enemy.get('max_hp')} live",
            )

        live_intent = validate.live_enemy_intent(live_enemy)
        if live_intent is None:
            continue
        emu_intent = (emu_enemy.get("intent_type"), emu_enemy.get("intent_magnitude"))
        if live_intent[0] != emu_intent[0] or (
            live_intent[1] is not None and live_intent[1] != emu_intent[1]
        ):
            intent_ok = False
            notes.append(f"enemy {index} intent {emu_intent} emu vs {live_intent} live")

    return hp_ok, intent_ok, "; ".join(notes)


def capture_one(
    base_url: str,
    seed: str,
    encounter: str,
    ascension: int,
) -> dict[str, Any]:
    live_encounter = validate.LIVE_ENCOUNTER_BY_EMULATOR.get(encounter)
    if live_encounter is None:
        raise RuntimeError(f"No live encounter mapped for {encounter!r}")

    capture_sweep.abandon_any_run(base_url)
    start_real_game_run.start_seeded_run(
        base_url,
        seed,
        "IRONCLAD",
        abandon_existing=False,
        ascension=ascension,
    )
    validate.jump_to_encounter(base_url, live_encounter)

    live_state = start_real_game_run.get_state(base_url)
    live_summary = trace_real_game.summarize_state(live_state)
    emu_summary = emulator_summary(seed, encounter)

    deck_ok, deck_note = compare_deck(seed, encounter, live_state)
    hp_ok, intent_ok, enemy_note = compare_enemies(live_summary, emu_summary)
    live_player = live_summary.get("player") or {}
    emu_player = emu_summary.get("player") or {}
    player_ok = (live_player.get("hp"), live_player.get("max_hp")) == (
        emu_player.get("hp"),
        emu_player.get("max_hp"),
    )

    return {
        "seed": seed,
        "encounter": encounter,
        "live_encounter": live_encounter,
        "sections": {
            "deck": deck_ok,
            "enemies": hp_ok,
            "intent": intent_ok,
            "player": player_ok,
        },
        "notes": "; ".join(n for n in (deck_note, enemy_note) if n),
        "live_state": live_state,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=capture_sweep.DEFAULT_BASE_URL)
    parser.add_argument("--seeds", nargs="*", default=None)
    parser.add_argument("--count", type=int, default=2, help="random seeds to use")
    parser.add_argument(
        "--encounters",
        nargs="*",
        default=None,
        help=f"default: {' '.join(DEFAULT_ENCOUNTERS)}",
    )
    parser.add_argument("--act", choices=sorted(ENCOUNTERS_BY_ACT), default=None)
    parser.add_argument(
        "--pool",
        choices=["weak", "normal", "both"],
        default="both",
        help="which encounter pool to sweep (default: both)",
    )
    parser.add_argument("--ascension", type=int, default=8)
    parser.add_argument("--random-seed", type=int, default=0)
    parser.add_argument(
        "--save-fixtures",
        action="store_true",
        help="write each capture to tests/fixtures/combat/<SEED>-<encounter>.json",
    )
    args = parser.parse_args()

    by_act = {"weak": WEAK_BY_ACT, "normal": NORMAL_BY_ACT, "both": ENCOUNTERS_BY_ACT}[
        args.pool
    ]
    encounters = args.encounters or (
        by_act[args.act] if args.act else [e for acts in by_act.values() for e in acts]
    )
    seeds = args.seeds or capture_sweep.pick_seeds(
        args.count,
        None,
        random.Random(args.random_seed),  # noqa: S311 - picking test seeds, not crypto
    )

    print(f"game       : {game_version.describe(game_version.detect())}")
    print(f"seeds      : {' '.join(seeds)}")
    print(f"encounters : {' '.join(encounters)}")
    capture_sweep.ensure_game(args.base_url)

    fixtures = Path(__file__).parent.parent / "tests/fixtures/combat"
    results: list[dict[str, Any]] = []
    jobs = [(seed, enc) for seed in seeds for enc in encounters]
    for index, (seed, encounter) in enumerate(jobs, start=1):
        print(f"\n[{index}/{len(jobs)}] {seed} -> {encounter}", flush=True)
        try:
            result = capture_one(args.base_url, seed, encounter, args.ascension)
        except Exception as exc:  # noqa: BLE001 - one bad job must not end the sweep
            print(f"  CAPTURE FAILED: {exc}", flush=True)
            results.append({"seed": seed, "encounter": encounter, "error": str(exc)})
            capture_sweep.recover_to_menu(args.base_url)
            continue

        marks = " ".join(
            f"{name}:{'ok' if ok else 'FAIL'}"
            for name, ok in result["sections"].items()
        )
        print(f"  {marks}", flush=True)
        if result["notes"]:
            print(f"  {result['notes']}", flush=True)

        if args.save_fixtures:
            path = fixtures / f"{seed}-{encounter}.json"
            path.parent.mkdir(parents=True, exist_ok=True)
            # The live state verbatim (it is already the shape compare_draw_pile and
            # trace_real_game read), plus the inputs needed to rebuild the emulator side
            # offline. Recording them beats re-deriving from the filename: the floor and
            # the weak/normal context are what make an encounter reproducible at all.
            stamped = {
                **result["live_state"],
                "game": game_version.detect(),
                "capture": {
                    "seed": seed,
                    "encounter": encounter,
                    "live_encounter": result["live_encounter"],
                    "completed_combat_rooms": validate.emulator_completed_combat_rooms(
                        encounter,
                    ),
                    "total_floor": validate.NEOW_JUMP_TOTAL_FLOOR,
                    "ascension": args.ascension,
                },
            }
            path.write_text(json.dumps(stamped, indent=2) + "\n")
            print(f"  wrote {path}", flush=True)
        results.append(result)

    print("\n" + "=" * 60)
    failed = []
    for result in results:
        label = f"{result['seed']:12} {result['encounter']:20}"
        if "error" in result:
            failed.append(result)
            print(f"  {label} ERROR  {result['error']}")
            continue
        bad = [name for name, ok in result["sections"].items() if not ok]
        if bad:
            failed.append(result)
        print(f"  {label} {'ALL MATCH' if not bad else 'FAIL: ' + ', '.join(bad)}")
    print(f"\n  {len(results) - len(failed)}/{len(results)} captures match everywhere")
    raise SystemExit(1 if failed else 0)


if __name__ == "__main__":
    main()
