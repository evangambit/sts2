"""Compare the emulator's ordered draw pile against the live game's, card for card.

The observation vector only carries pile *counts*, and STS2MCP's `draw_pile` is
sorted by rarity/id for display — so neither side exposed an ordered readout
until `Sts2_GetPile` (emulator) and `draw_pile_ordered` (our STS2MCP fork) were
added. This script joins the two.

Live half requires the game running with our STS2MCP fork installed:

    open "steam://rungameid/2868840"
    python scripts/compare_draw_pile.py --seed ABCDEF --encounter corpse-slugs

Use --live-json to re-diff a previously captured payload with no game running.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))
sys.path.insert(0, str(Path(__file__).parent))

from sts2_gym import Sts2CombatEnv, game_seed
from sts2_gym.commands import card_name_by_id


def _load(name: str) -> ModuleType:
    path = Path(__file__).with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(f"_cdp_{name}", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def normalize(name: str) -> str:
    """Collapse naming conventions so both sides compare on equal terms.

    The emulator stores PascalCase class names ("StrikeIronclad"); the game uses
    entry ids ("strike_ironclad"). Upgrade state is carried separately, so a
    trailing "+" is dropped here.
    """
    return re.sub(r"[^A-Za-z0-9]", "", name.strip().removesuffix("+")).lower()


def emulator_pile(
    seed: int, encounter: str, completed_combat_rooms: int, pile: str
) -> list[tuple[str, bool]]:
    names = card_name_by_id()
    env = Sts2CombatEnv(
        seed=seed,
        encounter=encounter,
        completed_combat_rooms=completed_combat_rooms,
    )
    try:
        env.reset()
        return [
            (names.get(card_id, f"<unknown:{card_id}>"), upgraded)
            for card_id, upgraded in env.get_pile(pile)
        ]
    finally:
        env.close()


# Emulator pile name -> the field our STS2MCP fork adds under result["player"].
LIVE_PILE_KEYS = {
    "hand": "hand_ordered",
    "draw": "draw_pile_ordered",
    "discard": "discard_pile_ordered",
}


def live_pile(state: dict[str, Any], pile: str) -> list[tuple[str, bool]]:
    key = LIVE_PILE_KEYS.get(pile)
    if key is None:
        raise SystemExit(
            f"No live readout for pile {pile!r}; expected one of "
            f"{sorted(LIVE_PILE_KEYS)}",
        )

    # The mod nests combat piles under result["player"]; accept a bare player dict
    # or a battle-wrapped payload too, so saved captures of either shape re-diff.
    cards = None
    for scope in (state.get("player"), (state.get("battle") or {}).get("player"), state):
        if isinstance(scope, dict) and scope.get(key) is not None:
            cards = scope[key]
            break

    if cards is None:
        raise SystemExit(
            f"Live state has no {key!r}. That field is a fork addition — rebuild and "
            "reinstall STS2MCP from ~/Projects/STSS/STS2MCP, then restart the game.",
        )
    return [(str(c.get("id") or ""), bool(c.get("is_upgraded"))) for c in cards]


def explain_embark_crash(
    start_real_game_run: ModuleType, args: Any, exc: Exception
) -> None:
    """Turn the known embark crash into the recovery recipe.

    Embarking through the lobby NREs in NRunMusicController.UpdateTrack — a game
    bug we cannot fix from here. But it crashes *after* the run is created and
    written to current_run.save, and loading that save takes a different code
    path (isRestoringRoomStackBase) which works cleanly. So the crash is
    recoverable: restart, Continue, then jump straight to the encounter.
    """
    try:
        state = start_real_game_run.get_state(args.base_url)
    except Exception:  # noqa: BLE001 - diagnostics only, keep the original error
        return

    options = start_real_game_run.option_names(state)
    if "report_bug" not in options:
        return

    print(
        f"\n!! The game hit its 'internal error!' popup ({exc}).\n"
        "   This is the known embark crash (NRunMusicController NRE) — but the run\n"
        "   WAS created and saved first, so nothing is lost. Recover with:\n\n"
        '     pkill -9 -if "slay the spire 2"; sleep 3; '
        'open "steam://rungameid/2868840"\n\n'
        "   then click CONTINUE (not New Run, and do not abandon), and re-run this\n"
        "   with --jump-encounter instead of --start-run.",
    )


def preflight_no_run_in_progress(start_real_game_run: ModuleType, args: Any) -> None:
    """Refuse to embark while a run exists, unless --abandon is given.

    The in-game abandon deletes current_run.save.backup and *throws* when that
    file is absent (observed: "Error deleting path ... Failed" inside
    NMainMenu.AbandonRun). The half-finished teardown then makes the next embark
    NRE in NRunMusicController, which is the "internal error!" popup. Once the
    backup is missing this reproduces every time, so failing fast beats retrying.
    """
    if args.abandon:
        return
    if start_real_game_run.current_run_seed(args.base_url) is None:
        return

    raise SystemExit(
        "A run is already in progress.\n"
        "  Driving the in-game abandon from here is what crashes the game, so this\n"
        "  stops instead. Pick one:\n"
        "    * Abandon the run yourself from the main menu, then re-run this; or\n"
        "    * Quit the game and move the stale save aside:\n"
        "        mv ~/Library/Application\\ Support/SlayTheSpire2/steam/*/profile1/"
        "saves/current_run.save /tmp/\n"
        "    * Or, if the run is ALREADY the one you want, re-run with "
        "--jump-encounter\n"
        "      to skip the lobby entirely and just jump it into the encounter.\n"
        "  --abandon forces the old (crash-prone) behaviour.",
    )


def check_run_config(
    state: dict[str, Any], seed: int, encounter: str, completed: int
) -> None:
    """Warn when the two sides aren't describing the same fight.

    Player HP is the cheapest proxy for ascension: the emulator models A8 (64/80),
    so a different live ascension yields different HP — and comparing piles across
    mismatched configs would produce a meaningless diff that looks like a real bug.
    """
    player = state.get("player") or (state.get("battle") or {}).get("player") or {}
    live_hp, live_max = player.get("hp"), player.get("max_hp")
    if live_hp is None or live_max is None:
        return

    env = Sts2CombatEnv(
        seed=seed, encounter=encounter, completed_combat_rooms=completed
    )
    try:
        obs, _ = env.reset()
        emu_hp, emu_max = int(obs[0]), int(obs[1])
    finally:
        env.close()

    if (live_hp, live_max) != (emu_hp, emu_max):
        print(
            f"\n!! CONFIG MISMATCH: player HP live {live_hp}/{live_max} vs emulator "
            f"{emu_hp}/{emu_max}.\n"
            "   Likely a different ascension level (emulator models A8) or a Neow "
            "option that altered HP.\n"
            "   The pile diff below is comparing different fights — fix this first.",
        )


def render(
    emu: list[tuple[str, bool]], live: list[tuple[str, bool]], label: str
) -> bool:
    """Print a side-by-side comparison. Returns True when the piles match."""
    matched = True
    print(f"\n=== {label} (index 0 = top of pile, drawn next) ===")
    print(f"{'#':>3}  {'emulator':<28} {'live':<28} ")
    print("-" * 66)

    for i in range(max(len(emu), len(live))):
        e = emu[i] if i < len(emu) else None
        lv = live[i] if i < len(live) else None
        e_txt = f"{e[0]}{'+' if e[1] else ''}" if e else "—"
        l_txt = f"{lv[0]}{'+' if lv[1] else ''}" if lv else "—"
        same = (
            e is not None
            and lv is not None
            and normalize(e[0]) == normalize(lv[0])
            and e[1] == lv[1]
        )
        matched &= same
        print(f"{i:>3}  {e_txt:<28} {l_txt:<28} {'' if same else '<-- MISMATCH'}")

    if len(emu) != len(live):
        matched = False
        print(f"\nLENGTH MISMATCH: emulator {len(emu)} vs live {len(live)}")

    # A multiset check separates "wrong order" from "wrong cards" — the former
    # points at shuffle/reorder, the latter at deck construction.
    if not matched:
        emu_bag = sorted(normalize(n) for n, _ in emu)
        live_bag = sorted(normalize(n) for n, _ in live)
        if emu_bag == live_bag:
            print("\nSame cards, different ORDER -> shuffle or turn-1 reorder divergence.")
        else:
            print("\nDifferent CARDS -> deck construction diverges, not just ordering.")

    return matched


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seed", default="ABCDEF", help="run's string seed")
    parser.add_argument("--encounter", default="corpse-slugs")
    parser.add_argument("--character", default="ironclad")
    parser.add_argument("--base-url", default="http://localhost:15526")
    parser.add_argument(
        "--completed-combat-rooms",
        type=int,
        default=None,
        help="defaults to the weak/normal variant implied by --encounter",
    )
    parser.add_argument(
        "--live-json",
        type=Path,
        default=None,
        help="re-diff a saved live state instead of driving the game",
    )
    parser.add_argument(
        "--save-live-json",
        type=Path,
        default=None,
        help="write the captured live state here for offline re-diffing",
    )
    parser.add_argument(
        "--start-run",
        action="store_true",
        help="start a seeded run and jump into the encounter before capturing",
    )
    parser.add_argument(
        "--jump-encounter",
        action="store_true",
        help="use the run already in progress: skip the lobby entirely and just "
        "debug_start_encounter into it. Pair with a run you embarked by hand.",
    )
    parser.add_argument(
        "--abandon",
        action="store_true",
        help="allow driving the in-game abandon before embarking (crash-prone; "
        "see preflight_no_run_in_progress)",
    )
    parser.add_argument(
        "--ascension",
        type=int,
        default=8,
        help="live ascension level; the emulator models A8 (player 64/80). "
        "Only used with --start-run.",
    )
    parser.add_argument("--piles", default="hand,draw")
    args = parser.parse_args()

    validate = _load("validate_real_game_trace")
    completed = args.completed_combat_rooms
    if completed is None:
        completed = validate.emulator_completed_combat_rooms(args.encounter)

    if args.live_json is not None:
        state = json.loads(args.live_json.read_text())
    else:
        start_real_game_run = _load("start_real_game_run")
        if args.jump_encounter:
            live_encounter = validate.LIVE_ENCOUNTER_BY_EMULATOR.get(args.encounter)
            if live_encounter is None:
                raise SystemExit(f"No live encounter mapped for {args.encounter!r}")
            print(f"Jumping the *existing* run into {live_encounter} ...")
            validate.jump_to_encounter(args.base_url, live_encounter)
        elif args.start_run:
            live_encounter = validate.LIVE_ENCOUNTER_BY_EMULATOR.get(args.encounter)
            if live_encounter is None:
                raise SystemExit(f"No live encounter mapped for {args.encounter!r}")
            print(
                f"Starting seeded run {args.seed!r} -> {live_encounter} "
                f"(ascension {args.ascension}) ..."
            )
            preflight_no_run_in_progress(start_real_game_run, args)
            try:
                validate.start_debug_encounter(
                    args.base_url,
                    args.seed,
                    args.character,
                    live_encounter,
                    ascension=args.ascension,
                    abandon_existing=args.abandon,
                )
            except RuntimeError as exc:
                explain_embark_crash(start_real_game_run, args, exc)
                raise
        state = start_real_game_run.get_state(args.base_url)

    if args.save_live_json is not None:
        args.save_live_json.write_text(json.dumps(state, indent=2))
        print(f"wrote live state -> {args.save_live_json}")

    seed = game_seed(args.seed)
    print(f"seed {args.seed!r} -> gen seed {seed}, encounter {args.encounter!r} "
          f"(completed_combat_rooms={completed})")
    check_run_config(state, seed, args.encounter, completed)

    all_matched = True
    combined: dict[str, list[tuple[str, bool]]] = {"emu": [], "live": []}
    for pile in (p.strip() for p in args.piles.split(",") if p.strip()):
        emu = emulator_pile(seed, args.encounter, completed, pile)
        live = live_pile(state, pile)
        combined["emu"] += emu
        combined["live"] += live
        all_matched &= render(emu, live, pile)

    # Per-pile multisets differ whenever a card merely lands in the other pile, so
    # judge "wrong order vs wrong cards" across hand+draw together — that union is
    # the shuffled deck.
    if not all_matched and {"hand", "draw"} <= set(
        p.strip() for p in args.piles.split(",")
    ):
        emu_bag = sorted(normalize(n) for n, _ in combined["emu"])
        live_bag = sorted(normalize(n) for n, _ in combined["live"])
        print("\n=== whole deck (hand + draw) ===")
        if emu_bag == live_bag:
            hits = sum(
                normalize(a[0]) == normalize(b[0])
                for a, b in zip(combined["emu"], combined["live"])
            )
            print(
                f"Deck composition MATCHES ({len(emu_bag)} cards); "
                f"{hits}/{len(emu_bag)} positions align.\n"
                "-> Pure SHUFFLE-ORDER divergence, not deck construction.",
            )
        else:
            print("Deck composition itself differs -> deck construction diverges.")

    print("\n" + ("ALL PILES MATCH" if all_matched else "MISMATCH — see above"))
    raise SystemExit(0 if all_matched else 1)


if __name__ == "__main__":
    main()
