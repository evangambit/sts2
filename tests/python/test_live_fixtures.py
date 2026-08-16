"""Run the differential comparisons against committed live-game fixtures.

The C# tests in RunEngineTests pin ground truth as hand-transcribed literals, which
is lossy — the map, in particular, only got a node count and per-row counts. These
tests instead run the *real* comparison code against captures taken from the game,
so the full structure is checked and the ground truth survives the next run
overwriting current_run.save.

Capture a new one with:
    python scripts/verify_run_generation.py --save-fixture tests/fixtures/run_generation/<SEED>.json
    python scripts/compare_draw_pile.py ... --save-live-json tests/fixtures/combat/<name>.json
"""

import contextlib
import importlib.util
import io
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "src"))
sys.path.insert(0, str(ROOT / "scripts"))

FIXTURES = ROOT / "tests" / "fixtures"


def _load_script(name: str):
    spec = importlib.util.spec_from_file_location(
        f"_fixture_{name}", ROOT / "scripts" / f"{name}.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify_run_generation = _load_script("verify_run_generation")
compare_draw_pile = _load_script("compare_draw_pile")


def _quiet(fn, *args, **kwargs):
    """Run a comparison helper without its side-by-side report on stdout."""
    with contextlib.redirect_stdout(io.StringIO()):
        return fn(*args, **kwargs)


class RunGenerationFixtureTest(unittest.TestCase):
    """Ground truth: a live A8 run on seed "AAB" (Overgrowth)."""

    @classmethod
    def setUpClass(cls):
        fixture = FIXTURES / "run_generation" / "AAB.json"
        cls.save = verify_run_generation.load_save(fixture)
        cls.act = cls.save["acts"][cls.save["current_act_index"]]
        cls.seed = cls.save["rng"]["seed"]
        cls.emu = verify_run_generation.emulator_generation(cls.seed)
        cls.names = verify_run_generation.encounter_names()

    def test_fixture_carries_no_profile_data(self):
        # The distilled fixture must not leak play history or account identifiers.
        raw = (FIXTURES / "run_generation" / "AAB.json").read_text()
        for leaked in ("unlock_state", "encounters_seen", "number_of_runs", "7656119"):
            self.assertNotIn(leaked, raw)

    def test_act_matches(self):
        emu_act = verify_run_generation.ACT_NAMES[self.emu["act"]]
        live_act = self.act["id"].replace("ACT.", "")
        self.assertEqual(
            verify_run_generation.normalize(live_act),
            verify_run_generation.normalize(emu_act),
        )

    def test_normal_encounters_match(self):
        self.assertTrue(
            _quiet(
                verify_run_generation.compare_sequence,
                "normal",
                self.emu["normal"],
                self.act["rooms"]["normal_encounter_ids"],
                self.names,
            )
        )

    def test_elite_encounters_match(self):
        self.assertTrue(
            _quiet(
                verify_run_generation.compare_sequence,
                "elite",
                self.emu["elite"],
                self.act["rooms"]["elite_encounter_ids"],
                self.names,
            )
        )

    def test_boss_matches(self):
        emu_boss = self.names[self.emu["boss"]]
        self.assertEqual(
            verify_run_generation.normalize(self.act["rooms"]["boss_id"]),
            verify_run_generation.normalize(emu_boss),
        )

    def test_map_node_count_matches(self):
        live = self.act["saved_map"]
        expected = len(live["points"]) + sum(
            1 for key in ("start", "boss") if live.get(key)
        )
        emu_nodes = len(self.emu["map"]) // 3
        self.assertEqual(expected, emu_nodes)

    def test_map_rows_match_except_the_known_residual(self):
        """Row 1 differs by one node's column — see HANDOFF.

        Pinned rather than skipped: if the residual is fixed, or spreads to another
        row, this fails and asks to be updated.
        """
        live_map = self.act["saved_map"]
        live = {
            (p["coord"]["col"], p["coord"]["row"]): p["type"] for p in live_map["points"]
        }
        for key in ("start", "boss"):
            node = live_map.get(key)
            if node:
                live[(node["coord"]["col"], node["coord"]["row"])] = node["type"]

        tri = self.emu["map"]
        emu = {
            (tri[i], tri[i + 1]): verify_run_generation.NODE_TYPE_NAMES[tri[i + 2]]
            for i in range(0, len(tri), 3)
        }

        mismatched = {
            row
            for row in {r for _, r in live} | {r for _, r in emu}
            if {c: t for (c, r), t in live.items() if r == row}
            != {c: t for (c, r), t in emu.items() if r == row}
        }
        self.assertEqual({1}, mismatched, "known residual is exactly row 1")


class CombatFixtureTest(unittest.TestCase):
    """Ground truth: the live "ABCDEF" A8 run's CorpseSlugsWeak opening."""

    def test_opening_piles_match_exactly(self):
        fixture = FIXTURES / "combat" / "ABCDEF-corpse-slugs.json"
        state = json.loads(fixture.read_text())
        seed = compare_draw_pile.game_seed("ABCDEF")

        for pile in ("hand", "draw"):
            emu = compare_draw_pile.emulator_pile(seed, "corpse-slugs", 0, pile)
            live = compare_draw_pile.live_pile(state, pile)
            self.assertEqual(
                [(compare_draw_pile.normalize(n), up) for n, up in live],
                [(compare_draw_pile.normalize(n), up) for n, up in emu],
                f"{pile} pile diverged from the live capture",
            )

    def test_player_hp_matches_ascension_eight(self):
        fixture = FIXTURES / "combat" / "ABCDEF-corpse-slugs.json"
        player = json.loads(fixture.read_text())["player"]
        self.assertEqual((64, 80), (player["hp"], player["max_hp"]))


if __name__ == "__main__":
    unittest.main()
