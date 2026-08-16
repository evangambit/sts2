"""Verify emulator run generation (act, encounters, boss, map) against a live save.

The live `current_run.save` is plain JSON and records exactly what the game
generated for a seed: `acts[i].id`, `acts[i].rooms.{normal,elite}_encounter_ids`,
`boss_id`, and `saved_map.points`. That makes it ground truth for everything the
run engine rolls up front — no need to drive the game.

    python scripts/verify_run_generation.py                 # auto-detect the save
    python scripts/verify_run_generation.py --save PATH     # explicit

Exit code 0 when every checked section matches.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sts2_gym import game_seed, native

SAVE_GLOB = (
    "Library/Application Support/SlayTheSpire2/steam/*/profile*/saves/current_run.save"
)

LIST_NORMAL_ENCOUNTERS = 11
LIST_ELITE_ENCOUNTERS = 12
LIST_EVENTS = 13
LIST_GENERATION_SUMMARY = 14  # [act, boss_encounter_id, map_node_count]
LIST_MAP_NODES = 15  # (col, row, node_type) triples

# RunConstants node types -> the save's MapPointType strings.
# The emulator carries the start node as NodeNone and calls the treasure row
# NodeRelic; the save names them "ancient" and "treasure".
NODE_TYPE_NAMES = {
    0: "ancient", 1: "monster", 2: "elite", 3: "rest_site",
    4: "shop", 5: "treasure", 6: "boss", 7: "unknown",
}

ACT_NAMES = {1: "OVERGROWTH", 2: "UNDERDOCKS"}


def find_save(explicit: Path | None) -> Path:
    if explicit is not None:
        return explicit
    matches = sorted(Path.home().glob(SAVE_GLOB))
    if not matches:
        raise SystemExit(
            "No current_run.save found. Start a run in game, or pass --save PATH.",
        )
    return matches[-1]


def load_save(path: Path) -> dict[str, Any]:
    raw = path.read_text(errors="replace")
    return json.loads(raw[raw.index("{") :])


def distill_fixture(save: dict[str, Any]) -> dict[str, Any]:
    """Reduce a live save to just the ground truth this script compares.

    Deliberately drops everything else — `unlock_state` (play history), timestamps,
    deck/relic state — so a committed fixture carries no profile data. The shape is
    kept identical to a real save so `--fixture` and `--save` share one code path.
    """
    return {
        "_comment": (
            "Ground truth captured from a live StS2 run. Distilled from "
            "current_run.save by verify_run_generation.py --save-fixture."
        ),
        "rng": {"seed": save["rng"]["seed"]},
        "ascension": save.get("ascension"),
        "current_act_index": save["current_act_index"],
        "acts": [
            {
                "id": act["id"],
                "rooms": {
                    "normal_encounter_ids": act["rooms"]["normal_encounter_ids"],
                    "elite_encounter_ids": act["rooms"]["elite_encounter_ids"],
                    "boss_id": act["rooms"]["boss_id"],
                },
                "saved_map": act["saved_map"],
            }
            for act in save["acts"]
            if "saved_map" in act
        ],
    }


def encounter_names() -> dict[int, str]:
    """Emulator encounter id -> name, read from CombatFactory's ActOneEncounter."""
    src = (
        Path(__file__).parent.parent / "src/Sts2Emulator/Core/CombatFactory.cs"
    ).read_text()
    body = re.search(r"private enum ActOneEncounter\s*\{(.*?)\n    \}", src, re.S)
    if body is None:
        raise SystemExit("Could not parse ActOneEncounter from CombatFactory.cs")
    names = [
        line.strip().rstrip(",").split("=")[0].strip()
        for line in body.group(1).split("\n")
        if line.strip() and not line.strip().startswith("//")
    ]
    return dict(enumerate(names))


def normalize(name: str) -> str:
    """Compare on letters only, ignoring variant decoration.

    Strip the variant suffix *after* collapsing to lowercase letters, so it works
    whether the source writes SLIMES_NORMAL or SlimesNormal. The weak/normal split
    is a variant of one encounter (the emulator selects it via
    completed_combat_rooms), so it is not a difference worth failing on here.
    """
    n = re.sub(r"[^A-Za-z0-9]", "", name.replace("ENCOUNTER.", "").replace("EVENT.", ""))
    n = n.lower()
    for suffix in ("weak", "normal", "elite", "boss"):
        if n.endswith(suffix) and len(n) > len(suffix):
            n = n[: -len(suffix)]
            break
    return n


def emulator_generation(seed: str) -> dict[str, Any]:
    import ctypes

    handle = native.run_create()
    try:
        obs = (ctypes.c_int * native.RUN_OBS_SIZE)()
        native.run_reset(handle, seed, obs)
        act, boss, map_nodes = native.run_state_list(handle, LIST_GENERATION_SUMMARY, 3)
        return {
            "act": act,
            "boss": boss,
            "map_nodes": map_nodes,
            "normal": list(native.run_state_list(handle, LIST_NORMAL_ENCOUNTERS, 64)),
            "elite": list(native.run_state_list(handle, LIST_ELITE_ENCOUNTERS, 64)),
            "events": list(native.run_state_list(handle, LIST_EVENTS, 64)),
            "map": list(native.run_state_list(handle, LIST_MAP_NODES, 1024)),
        }
    finally:
        native.run_destroy(handle)


def compare_map(emu_triples: list[int], saved_map: dict[str, Any]) -> bool:
    """Compare map structure row by row.

    The save keeps `start` and `boss` outside `points`, so fold them in — the
    emulator carries them as ordinary nodes in the same grid.
    """
    live: dict[tuple[int, int], str] = {}
    for pt in saved_map["points"]:
        live[(pt["coord"]["col"], pt["coord"]["row"])] = pt["type"]
    for key in ("start", "boss"):
        node = saved_map.get(key)
        if node:
            live[(node["coord"]["col"], node["coord"]["row"])] = node["type"]

    emu: dict[tuple[int, int], str] = {}
    for i in range(0, len(emu_triples), 3):
        col, row, node_type = emu_triples[i : i + 3]
        emu[(col, row)] = NODE_TYPE_NAMES.get(node_type, f"<{node_type}>")

    print(f"\n=== map — emulator {len(emu)} nodes vs live {len(live)} "
          f"(points + start + boss) ===")
    rows = sorted({r for _, r in live} | {r for _, r in emu})
    matched = True
    for row in rows:
        live_row = {c: t for (c, r), t in live.items() if r == row}
        emu_row = {c: t for (c, r), t in emu.items() if r == row}
        same = live_row == emu_row
        matched &= same
        if same:
            print(f"  row {row:2}: ok  ({len(live_row)} nodes)")
        else:
            fmt = lambda d: " ".join(f"c{c}:{d[c]}" for c in sorted(d)) or "-"
            print(f"  row {row:2}: MISMATCH")
            print(f"           emu  {fmt(emu_row)}")
            print(f"           live {fmt(live_row)}")
    return matched


def compare_sequence(
    label: str, emu_ids: list[int], live_ids: list[str], names: dict[int, str]
) -> bool:
    print(f"\n=== {label} — emulator {len(emu_ids)} vs live {len(live_ids)} ===")
    hits = 0
    span = max(len(emu_ids), len(live_ids))
    for i in range(span):
        emu = names.get(emu_ids[i], f"<{emu_ids[i]}>") if i < len(emu_ids) else "—"
        live = live_ids[i] if i < len(live_ids) else "—"
        ok = i < len(emu_ids) and i < len(live_ids) and normalize(emu) == normalize(live)
        hits += ok
        print(f"  {i:2}  {emu:26} {live:36} {'ok' if ok else 'MISMATCH'}")
    matched = hits == span
    print(f"  -> {hits}/{span} match")
    return matched


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--save", type=Path, default=None)
    parser.add_argument(
        "--fixture", type=Path, default=None, help="verify against a stored fixture"
    )
    parser.add_argument(
        "--save-fixture",
        type=Path,
        default=None,
        help="distill the live save into a committable fixture and exit",
    )
    parser.add_argument("--act-index", type=int, default=None, help="default: current")
    args = parser.parse_args()

    if args.fixture is not None:
        save_path = args.fixture
        save = load_save(save_path)
    else:
        save_path = find_save(args.save)
        save = load_save(save_path)

    if args.save_fixture is not None:
        args.save_fixture.parent.mkdir(parents=True, exist_ok=True)
        args.save_fixture.write_text(json.dumps(distill_fixture(save), indent=2) + "\n")
        print(f"wrote fixture -> {args.save_fixture}")
        raise SystemExit(0)
    seed = (save.get("rng") or {}).get("seed")
    act_index = args.act_index if args.act_index is not None else save["current_act_index"]
    act = save["acts"][act_index]
    rooms = act["rooms"]

    print(f"save : {save_path}")
    print(f"seed : {seed!r} -> gen seed {game_seed(str(seed))}")
    ascension = save.get("ascension")
    print(f"act  : index {act_index} = {act['id']}  (ascension {ascension})")
    if ascension != 8:
        print(
            f"\n!! WARNING: this save is ascension {ascension}, not 8.\n"
            "   The emulator models high ascension, so it always budgets 8 elites\n"
            "   (the game's round(5 * 1.6) with SwarmingElites). At a lower ascension\n"
            "   the game budgets 5, and the elite/map sections below will mismatch for\n"
            "   that reason alone. Re-capture at A8 to make this comparison meaningful.",
        )

    emu = emulator_generation(str(seed))
    names = encounter_names()
    results: dict[str, bool] = {}

    print("\n=== act ===")
    emu_act = ACT_NAMES.get(emu["act"], f"<{emu['act']}>")
    live_act = act["id"].replace("ACT.", "")
    results["act"] = normalize(emu_act) == normalize(live_act)
    print(f"  emulator {emu_act} vs live {live_act} "
          f"{'ok' if results['act'] else 'MISMATCH'}")

    results["normal"] = compare_sequence(
        "normal encounters", emu["normal"], rooms["normal_encounter_ids"], names
    )
    results["elite"] = compare_sequence(
        "elite encounters", emu["elite"], rooms["elite_encounter_ids"], names
    )

    print("\n=== boss ===")
    emu_boss = names.get(emu["boss"], f"<{emu['boss']}>")
    results["boss"] = normalize(emu_boss) == normalize(rooms["boss_id"])
    print(f"  emulator {emu_boss} vs live {rooms['boss_id']} "
          f"{'ok' if results['boss'] else 'MISMATCH'}")

    results["map"] = compare_map(emu["map"], act["saved_map"])

    print("\n" + "=" * 60)
    for key, ok in results.items():
        print(f"  {key:22} {'PASS' if ok else 'FAIL'}")
    failed = [k for k, ok in results.items() if not ok]
    print("ALL SECTIONS MATCH" if not failed else f"FAILING: {', '.join(failed)}")
    raise SystemExit(0 if not failed else 1)


if __name__ == "__main__":
    main()
