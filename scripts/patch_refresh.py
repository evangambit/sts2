#!/usr/bin/env python3
"""Patch-day driver: refresh everything mechanical, and report what needs judgement.

Run this when Steam has updated Slay the Spire 2.

What it automates
-----------------
* Detects the installed build and compares it to the one recorded in data/game_version.json.
* Re-decompiles and re-extracts the generated data (ids are stable, so this is safe).
* Reports id-map additions and data drift.
* Runs the test suites and *classifies* what broke.
* Tells you which fixtures are now stale and prints the commands to re-capture them.

What it deliberately does NOT automate
--------------------------------------
Rewriting expected values. Auto-updating an assertion to whatever the code now
produces turns a regression detector into a rubber stamp — the failing DarkEmbrace
test is exactly how we caught the Exhaust-flag bug, and a script that "fixed" it
would have buried a defect affecting ~30 cards. Ground truth also cannot be
regenerated from the emulator by definition: it has to come from the game.

    python scripts/patch_refresh.py            # report only
    python scripts/patch_refresh.py --apply    # also decompile + extract
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

import game_version

REPO = Path(__file__).resolve().parent.parent
VERSION_FILE = REPO / "data" / "game_version.json"
FIXTURES = REPO / "tests" / "fixtures"

# Tests that encode algorithms rather than content. These should survive a content
# patch; a failure here means the game changed how something *works*.
MECHANISM_TESTS = [
    "GameRng_HelperOutputsAreLocked",
    "RunRngSet_NamedStreamOutputsAreLocked",
    "RunRngSet_FreshSpecialStreamOutputsAreLocked",
    "RunRngSet_DerivesGameSeedForStringSeed",
    "DeterministicHash_MatchesPythonPinnedValues",
    "MegaRandomReproducesTheLiveShuffle",
    "TurnOneReorder",
]


def run(cmd: list[str], **kw) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, **kw)


def recorded_version() -> dict | None:
    if VERSION_FILE.exists():
        return json.loads(VERSION_FILE.read_text())
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--apply", action="store_true", help="run decompile + extract, not just report"
    )
    parser.add_argument("--game-dir", default=None)
    args = parser.parse_args()

    installed = game_version.detect()
    recorded = recorded_version()

    print("=" * 68)
    print(f"installed : {game_version.describe(installed)}")
    print(f"recorded  : {game_version.describe(recorded) if recorded else '(none)'}")
    changed = recorded is None or recorded.get("steam_buildid") != installed.get(
        "steam_buildid"
    )
    print(f"changed   : {'YES' if changed else 'no'}")
    print("=" * 68)

    if not changed and not args.apply:
        print("\nNothing to do — the recorded build matches what is installed.")
        return

    if args.apply:
        print("\n--- decompile ---")
        cmd = ["bash", "scripts/decompile.sh"] + ([args.game_dir] if args.game_dir else [])
        res = run(cmd)
        print(res.stdout[-1500:] or res.stderr[-1500:])
        if res.returncode != 0:
            raise SystemExit("decompile failed — stopping.")

        print("\n--- extract (ids are stable; watch for NEW entries) ---")
        res = run([sys.executable, "scripts/extract_data.py"])
        print(res.stdout or res.stderr)
        if res.returncode != 0:
            raise SystemExit(
                "extraction failed — most likely a card-id constant now names a card "
                "that no longer exists. Fix data/card_id_classes.json before continuing."
            )

        print("\n--- data drift ---")
        print(run([sys.executable, "scripts/diff_patch.py"]).stdout[-3000:])

    print("\n--- tests ---")
    cs = run(["dotnet", "test", "src/Sts2Emulator.Tests/"])
    py = run([sys.executable, "-m", "unittest", "discover", "-s", "tests/python"])
    cs_ok, py_ok = cs.returncode == 0, py.returncode == 0
    print(f"  C#     : {'pass' if cs_ok else 'FAIL'}")
    print(f"  Python : {'pass' if py_ok else 'FAIL'}")

    if not cs_ok:
        failed = sorted(
            {
                line.split("Failed ")[1].split(" ")[0].split("(")[0]
                for line in cs.stdout.splitlines()
                if "  Failed " in line
            }
        )
        mechanism = [f for f in failed if any(m in f for m in MECHANISM_TESTS)]
        content = [f for f in failed if f not in mechanism]
        if mechanism:
            print("\n  !! MECHANISM tests failed — an algorithm changed, not just data.")
            print("     Do NOT re-baseline these. Investigate:")
            for f in mechanism:
                print(f"       {f}")
        if content:
            print("\n  Content-derived failures (expected when values change);")
            print("  check each against the decompiled source before updating:")
            for f in content[:20]:
                print(f"       {f}")

    print("\n--- fixtures ---")
    stale = []
    for path in sorted(FIXTURES.rglob("*.json")):
        stamp = json.loads(path.read_text()).get("game") or {}
        if stamp.get("steam_buildid") != installed.get("steam_buildid"):
            stale.append(path)
            print(f"  STALE  {path.relative_to(REPO)}  ({game_version.describe(stamp)})")
        else:
            print(f"  ok     {path.relative_to(REPO)}")

    if stale:
        print(
            "\n  Ground truth must come from the game — it cannot be regenerated here.\n"
            "  Start a run at A8 on the seed named by each fixture, then:\n"
        )
        for path in stale:
            if path.parent.name == "run_generation":
                print(
                    f"    python scripts/verify_run_generation.py --save-fixture {path.relative_to(REPO)}"
                )
            else:
                seed = path.stem.split("-")[0]
                print(
                    f"    python scripts/compare_draw_pile.py --seed {seed} "
                    f"--jump-encounter --save-live-json {path.relative_to(REPO)}"
                )

    # Record only when everything is verified at this build — the recorded
    # version is a claim that the emulator was checked against it.
    if changed and cs_ok and py_ok and not stale:
        VERSION_FILE.parent.mkdir(parents=True, exist_ok=True)
        VERSION_FILE.write_text(json.dumps(installed, indent=2) + "\n")
        print(f"\nrecorded {game_version.describe(installed)} -> {VERSION_FILE.name}")
    elif changed:
        print(
            "\nNot recording the new build yet — do that once tests pass and fixtures "
            "are re-captured, so the recorded version always means 'fully verified'."
        )


if __name__ == "__main__":
    main()
