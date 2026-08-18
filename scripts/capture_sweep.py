"""Capture and verify run generation across many seeds in one unattended pass.

Headless embarks (see HANDOFF, "Headless capture") made a capture cost ~15 seconds and
no hands, so ground truth stops being something we ration one seed at a time. This
drives the whole loop for a list of seeds: abandon whatever run exists, embark the seed
at A8, read what the game wrote to `current_run.save`, and compare every section the
emulator claims to model.

    python scripts/capture_sweep.py --count 8                 # random seeds
    python scripts/capture_sweep.py --count 6 --act underdocks # bias toward one act
    python scripts/capture_sweep.py --seeds AAB HEADLESS1 --save-fixtures

`--act` picks *candidate* seeds by asking the emulator which act each rolls — a search
heuristic, not an assumption: the capture then checks that prediction against the game
like any other, so a wrong act model shows up as a FAIL rather than being selected
around. Without it, seeds are drawn at random and land wherever they land.

Exit code 0 when every captured seed matched in every section.

Requires the game running with our STS2MCP fork (a recent one: the embark wait needs the
`rooms_entered` counter it reports); it starts one headless if the API is down. A run in
progress is ABANDONED — the sweep needs the lobby.
"""

from __future__ import annotations

import argparse
import contextlib
import ctypes
import importlib.util
import io
import json
import random
import subprocess
import sys
import time
from pathlib import Path
from types import ModuleType
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))
sys.path.insert(0, str(Path(__file__).parent))

import game_version

from sts2_gym import native

DEFAULT_BASE_URL = "http://localhost:15526"
GAME_DIR = Path.home() / (
    "Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
)
MACOS_DIR = GAME_DIR / "SlayTheSpire2.app/Contents/MacOS"
STEAM_APPID = "2868840"
SAVE_GLOB = (
    "Library/Application Support/SlayTheSpire2/steam/*/profile*/saves/current_run.save"
)

# The game's own alphabet (SeedHelper): no I, no O — it canonicalizes those to 1 and 0,
# so generating them here would embark a *different* seed than the one asked for.
SEED_ALPHABET = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"
SEED_LENGTH = 10  # SeedHelper.seedDefaultLength
LIST_GENERATION_SUMMARY = 14
ACT_NAMES = {1: "OVERGROWTH", 2: "UNDERDOCKS"}


def _load(name: str) -> ModuleType:
    path = Path(__file__).with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(f"_sweep_{name}", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify_run_generation = _load("verify_run_generation")
start_real_game_run = _load("start_real_game_run")


def quiet(fn, *args: Any, **kwargs: Any) -> Any:
    """Call a comparison helper without its side-by-side report on stdout."""
    with contextlib.redirect_stdout(io.StringIO()):
        return fn(*args, **kwargs)


def predicted_act(seed: str) -> str:
    """Ask the emulator which act 1 a seed rolls, to steer seed choice."""
    handle = native.run_create()
    obs = (ctypes.c_int * native.RUN_OBS_SIZE)()
    try:
        native.run_reset(handle, seed, obs)
        act, _boss, _nodes = native.run_state_list(handle, LIST_GENERATION_SUMMARY, 3)
        return ACT_NAMES.get(act, f"<{act}>")
    finally:
        native.run_destroy(handle)


def pick_seeds(count: int, act: str | None, rng: random.Random) -> list[str]:
    seeds: list[str] = []
    # Act 1 is a coin flip, so filtering costs ~2 draws per seed; cap the search
    # anyway rather than spinning forever if the act model ever changes shape.
    for _ in range(count * 200):
        if len(seeds) == count:
            break
        seed = "".join(rng.choice(SEED_ALPHABET) for _ in range(SEED_LENGTH))
        if seed in seeds:
            continue
        if act is None or predicted_act(seed) == act:
            seeds.append(seed)
    if len(seeds) < count:
        raise SystemExit(f"Could only find {len(seeds)} of {count} seeds for act {act}")
    return seeds


def api_is_up(base_url: str) -> bool:
    try:
        start_real_game_run.get_state(base_url)
    except Exception:  # noqa: BLE001 - any failure means "not ready"
        return False
    return True


def ensure_game(base_url: str, timeout: float = 120.0) -> None:
    """Start the game headless if its API is not already answering.

    Raises:
        SystemExit: the appid file is missing, or the API never came up.

    """
    if api_is_up(base_url):
        return

    appid = MACOS_DIR / "steam_appid.txt"
    if not appid.exists():
        raise SystemExit(
            f"Headless launch needs {appid} (Steamworks init hangs on a popup "
            f"without it). Create it once with:\n"
            f'  echo -n "{STEAM_APPID}" > "{appid}"',
        )

    print("launching the game headless ...")
    subprocess.Popen(
        [str(MACOS_DIR / "Slay the Spire 2"), "--headless"],
        cwd=str(MACOS_DIR),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        # The API answers before the menu exists — an empty option list is a lobby that
        # is still building, and driving it there is what wedges the whole sweep.
        if api_is_up(base_url) and start_real_game_run.option_names(
            start_real_game_run.get_state(base_url),
        ):
            print("  api up")
            return
        time.sleep(2)
    raise SystemExit(f"Game API never came up at {base_url}")


def recover_to_menu(base_url: str) -> None:
    """Get back to a usable main menu after a failed capture.

    One wedged seed used to fail every seed after it: the game was left on a submenu
    or mid-run, and every subsequent embark timed out waiting for the main menu. Walk
    back out, save-and-quit a run if there is one, and restart the game as a last
    resort.
    """
    with contextlib.suppress(Exception):
        state = start_real_game_run.get_state(base_url)
        if state.get("state_type") != "menu":
            start_real_game_run.post_action(base_url, {"action": "return_to_main_menu"})
            time.sleep(3)
        start_real_game_run.back_out_to_main_menu(base_url)
        if start_real_game_run.option_names(start_real_game_run.get_state(base_url)):
            return

    print("  restarting the game to recover ...")
    subprocess.run(["pkill", "-9", "-if", "slay the spire 2"], check=False)
    time.sleep(3)
    ensure_game(base_url)


def find_save() -> Path:
    matches = sorted(Path.home().glob(SAVE_GLOB))
    if not matches:
        raise SystemExit("No current_run.save found after embarking.")
    return matches[-1]


def abandon_any_run(base_url: str) -> None:
    """Clear a run in progress so the lobby is reachable again.

    Two steps, and skipping the first is why a sweep used to fail every other seed: a
    capture leaves the game *inside* the new run, where the main menu does not exist
    yet, so save-and-quit out of it before looking for `abandon_run`.

    Then the abandon itself: it deletes `current_run.save.backup` and throws when it
    is absent, and the half-finished teardown poisons the next embark — so hand it the
    file first. It also raises a yes/no popup that has to be answered before the main
    menu offers `singleplayer` again.

    Raises:
        RuntimeError: the main menu never offered `singleplayer` again.

    """
    state = start_real_game_run.get_state(base_url)
    if state.get("state_type") != "menu":
        start_real_game_run.post_action(base_url, {"action": "return_to_main_menu"})

    # Wait for the MAIN menu specifically, not merely "some screen with options" —
    # save-and-quit passes through intermediate screens and a confirmation popup, and
    # treating those as arrival is what left one seed in four embarking from the wrong
    # screen and timing out.
    # No `singleplayer` requirement here: while a run exists the main menu offers
    # `continue`/`abandon_run` instead, which is exactly the state we came for.
    if not wait_for_main_menu(base_url):
        raise RuntimeError("Never reached the main menu to abandon from")

    if "abandon_run" not in start_real_game_run.option_names(
        start_real_game_run.get_state(base_url),
    ):
        return

    save = find_save()
    backup = save.with_suffix(".save.backup")
    if not backup.exists():
        backup.write_bytes(save.read_bytes())

    start_real_game_run.post_menu(base_url, "abandon_run")
    if not wait_for_main_menu(base_url, require="singleplayer"):
        raise RuntimeError(
            "Abandon did not return to a main menu offering singleplayer",
        )


def wait_for_main_menu(
    base_url: str,
    require: str | None = None,
    timeout: float = 45.0,
) -> bool:
    """Settle on the main menu, answering any confirmation popup on the way.

    `require` names an option that must be present — pass `singleplayer` to mean "and
    no run is in progress any more", since that option only appears once there is none.
    """
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = start_real_game_run.get_state(base_url)
        options = start_real_game_run.option_names(state)
        if "yes" in options:
            start_real_game_run.post_menu(base_url, "yes")
        elif (
            state.get("state_type") == "menu"
            and state.get("menu_screen") == "main"
            and (require is None or require in options)
        ):
            return True
        time.sleep(1)
    return False


def compare(save: dict[str, Any]) -> dict[str, bool]:
    """Run every section comparison the verifier does, without its report."""
    seed = str(save["rng"]["seed"])
    act = save["acts"][save["current_act_index"]]
    emu = verify_run_generation.emulator_generation(seed)
    names = verify_run_generation.encounter_names()
    normalize = verify_run_generation.normalize

    return {
        "act": normalize(verify_run_generation.ACT_NAMES.get(emu["act"], "?"))
        == normalize(act["id"].replace("ACT.", "")),
        "normal": quiet(
            verify_run_generation.compare_sequence,
            "normal",
            emu["normal"],
            act["rooms"]["normal_encounter_ids"],
            names,
        ),
        "elite": quiet(
            verify_run_generation.compare_sequence,
            "elite",
            emu["elite"],
            act["rooms"]["elite_encounter_ids"],
            names,
        ),
        "boss": normalize(names.get(emu["boss"], "?"))
        == normalize(act["rooms"]["boss_id"]),
        "map": quiet(verify_run_generation.compare_map, emu["map"], act["saved_map"]),
        # Connectivity as well as positions: the same dots wired differently is a real
        # divergence, and it is what pins each node's legal columns.
        "edges": quiet(
            verify_run_generation.compare_edges,
            emu["edges"],
            act["saved_map"],
        ),
    }


# Seeds whose embark had to be retried. Empty is the expected state; anything here
# means the embark race is back and wants investigating, not a bigger retry count.
_retries: list[str] = []


def capture_one(
    base_url: str,
    seed: str,
    character: str,
    ascension: int,
    attempts: int = 2,
) -> dict[str, Any]:
    # Embarks used to crash roughly one seed in five; that race is fixed at the source
    # (see wait_for_run in start_real_game_run.py), and 34 consecutive embarks with
    # retries disabled confirmed it. This retry is a backstop for something genuinely
    # one-off, NOT the flake handling — it announces itself and the summary counts it,
    # so a returning flake shows up as a number instead of being quietly absorbed.
    for attempt in range(attempts):
        try:
            abandon_any_run(base_url)
            start_real_game_run.start_seeded_run(
                base_url,
                seed,
                character,
                abandon_existing=False,
                ascension=ascension,
            )
            break
        except Exception as exc:
            if attempt == attempts - 1:
                raise
            print(f"  !! embark failed ({exc}); retrying once", flush=True)
            _retries.append(seed)
            recover_to_menu(base_url)

    save_path = find_save()
    save = verify_run_generation.load_save(save_path)

    written = str((save.get("rng") or {}).get("seed"))
    if written != seed:
        raise RuntimeError(f"embarked {seed!r} but the save says {written!r}")

    return {
        "seed": seed,
        "act": save["acts"][save["current_act_index"]]["id"].replace("ACT.", ""),
        "predicted": predicted_act(seed),
        "ascension": save.get("ascension"),
        "sections": compare(save),
        "save": save,
        "save_path": save_path,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--seeds", nargs="*", default=None, help="explicit seeds")
    parser.add_argument("--count", type=int, default=6, help="random seeds to capture")
    parser.add_argument(
        "--act",
        choices=["overgrowth", "underdocks"],
        default=None,
        help="only capture seeds the emulator says roll this act (checked, not assumed)",
    )
    parser.add_argument("--character", default="IRONCLAD")
    parser.add_argument(
        "--ascension",
        type=int,
        default=8,
        help="the emulator models A8; a capture at another level is not comparable",
    )
    parser.add_argument(
        "--random-seed",
        type=int,
        default=0,
        help="seed the seed picker",
    )
    parser.add_argument(
        "--save-fixtures",
        action="store_true",
        help="write each capture to tests/fixtures/run_generation/<SEED>.json",
    )
    args = parser.parse_args()

    seeds = args.seeds or pick_seeds(
        args.count,
        args.act.upper() if args.act else None,
        random.Random(args.random_seed),  # noqa: S311 - picking test seeds, not crypto
    )

    print(f"game    : {game_version.describe(game_version.detect())}")
    print(f"seeds   : {' '.join(seeds)}")
    ensure_game(args.base_url)

    fixtures = Path(__file__).parent.parent / "tests/fixtures/run_generation"
    results: list[dict[str, Any]] = []
    for index, seed in enumerate(seeds, start=1):
        # Flushed: a 20-seed sweep runs for many minutes, and block-buffered
        # output means a redirected log stays empty until the very end.
        print(f"\n[{index}/{len(seeds)}] {seed}", flush=True)
        try:
            result = capture_one(
                args.base_url,
                seed,
                args.character,
                args.ascension,
            )
        except Exception as exc:  # noqa: BLE001 - one bad seed must not end the sweep
            print(f"  CAPTURE FAILED: {exc}", flush=True)
            results.append({"seed": seed, "error": str(exc)})
            recover_to_menu(args.base_url)
            continue

        sections = result["sections"]
        marks = " ".join(
            f"{name}:{'ok' if ok else 'FAIL'}" for name, ok in sections.items()
        )
        print(f"  {result['act']:11} {marks}", flush=True)
        if result["predicted"] != result["act"]:
            print(f"  !! emulator predicted {result['predicted']}")

        if args.save_fixtures:
            path = fixtures / f"{seed}.json"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                json.dumps(
                    verify_run_generation.distill_fixture(
                        result["save"],
                        result["save_path"],
                    ),
                    indent=2,
                )
                + "\n",
            )
            print(f"  wrote {path}")
        results.append(result)

    print("\n" + "=" * 60)
    acts: dict[str, int] = {}
    failed = []
    for result in results:
        if "error" in result:
            failed.append(result["seed"])
            print(f"  {result['seed']:10} ERROR  {result['error']}")
            continue
        acts[result["act"]] = acts.get(result["act"], 0) + 1
        bad = [name for name, ok in result["sections"].items() if not ok]
        if bad:
            failed.append(result["seed"])
        print(
            f"  {result['seed']:10} {result['act']:11} "
            f"{'ALL MATCH' if not bad else 'FAIL: ' + ', '.join(bad)}",
        )
    print(f"\n  acts captured: {acts}")
    if _retries:
        print(
            f"  !! {len(_retries)} embark(s) needed a retry: {', '.join(_retries)}\n"
            "     Expected zero — see wait_for_run in start_real_game_run.py.",
        )
    print(f"  {len(results) - len(failed)}/{len(results)} seeds match in every section")
    raise SystemExit(1 if failed else 0)


if __name__ == "__main__":
    main()
