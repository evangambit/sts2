"""Run the differential comparisons against committed live-game fixtures.

The C# tests in RunEngineTests pin ground truth as hand-transcribed literals, which
is lossy — the map, in particular, only got a node count and per-row counts. These
tests instead run the *real* comparison code against captures taken from the game,
so the full structure is checked and the ground truth survives the next run
overwriting current_run.save.

Capture a new one with:
    python scripts/verify_run_generation.py --save-fixture tests/fixtures/run_generation/<SEED>.json
    python scripts/compare_draw_pile.py ... --save-live-json tests/fixtures/combat/<name>.json
    python scripts/verify_act_selection.py --save-fixture tests/fixtures/act_selection/<build>.json
"""

import contextlib
import functools
import importlib.util
import io
import json
import re
import sys
import unittest
from pathlib import Path
from types import ModuleType
from typing import TYPE_CHECKING, Any

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "src"))
sys.path.insert(0, str(ROOT / "scripts"))

FIXTURES = ROOT / "tests" / "fixtures"

# A test mixin has to NOT derive from TestCase, or unittest collects and runs it with
# no fixture named. Type checkers then cannot see that `self` will be a TestCase, so
# claim the base only while checking.
_TestCaseIfChecking = unittest.TestCase if TYPE_CHECKING else object


def _load_script(name: str) -> ModuleType:
    """Import a repo script by path.

    Raises:
        RuntimeError: if the script is missing or cannot be loaded.

    """
    spec = importlib.util.spec_from_file_location(
        f"_fixture_{name}",
        ROOT / "scripts" / f"{name}.py",
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify_run_generation = _load_script("verify_run_generation")
verify_act_selection = _load_script("verify_act_selection")
compare_draw_pile = _load_script("compare_draw_pile")
validate_real_game_trace = _load_script("validate_real_game_trace")
trace_real_game = _load_script("trace_real_game")
combat_sweep = _load_script("combat_sweep")

from sts2_gym import Sts2CombatEnv  # noqa: E402 - after the sys.path setup above


def _quiet(fn, *args: Any, **kwargs: Any):
    """Run a comparison helper without its side-by-side report on stdout."""
    with contextlib.redirect_stdout(io.StringIO()):
        return fn(*args, **kwargs)


class FixtureStampTest(unittest.TestCase):
    """Every fixture must say which patch it came from, and they must agree.

    Deliberately does NOT check the installed game — tests must pass on a machine
    with no copy of StS2. The live comparison in verify_run_generation.py does that
    check and warns; this only guarantees the stamps exist and are not mixed across
    patches, which would silently compare captures from two different builds.
    """

    def test_every_fixture_is_stamped_and_they_agree(self):
        stamps = {}
        for path in sorted(FIXTURES.rglob("*.json")):
            game = json.loads(path.read_text()).get("game")
            self.assertIsNotNone(game, f"{path.name} has no game version stamp")
            self.assertTrue(game.get("release"), f"{path.name} has no release string")
            stamps[path.name] = (game.get("release"), game.get("steam_buildid"))

        self.assertTrue(stamps, "no fixtures found")
        self.assertEqual(
            1,
            len(set(stamps.values())),
            f"fixtures come from different game builds: {stamps}",
        )


class ProfileAssumptionTest(unittest.TestCase):
    """Captures must come from the profile the emulator models.

    Act selection and boss discovery read the player's unlock state, not just the
    seed: an unlocked-but-undiscovered act is force-selected instead of rolled, and
    the boss is overwritten by the first Act-1 boss the profile has never seen. The
    emulator models a fully-unlocked, fully-discovered profile, so a capture from a
    fresher account encodes different rules and is not comparable.
    """

    def test_run_generation_fixtures_come_from_a_fully_unlocked_profile(self):
        for path in sorted((FIXTURES / "run_generation").glob("*.json")):
            profile = json.loads(path.read_text()).get("profile")
            self.assertIsNotNone(profile, f"{path.name} records no profile facts")
            self.assertIsNot(
                profile.get("all_act1_bosses_seen"),
                False,
                f"{path.name}: boss roll was overridden by discovery order",
            )
            self.assertIsNot(
                profile.get("all_act1_acts_discovered"),
                False,
                f"{path.name}: act was force-selected, not rolled",
            )


class RunGenerationChecks(_TestCaseIfChecking):
    """The full run-generation comparison for one committed capture.

    A mixin at runtime, not a TestCase, so unittest does not collect it on its own:
    each concrete subclass below names a fixture and inherits every check. Act 1 is a
    coin flip between Overgrowth and Underdocks, and the two branches use different
    encounter pools, event counts and up-front RNG draws — so one capture per act is
    the minimum that can tell a correct model from a lucky one.
    """

    FIXTURE = ""

    @classmethod
    def setUpClass(cls) -> None:
        cls.path = FIXTURES / "run_generation" / cls.FIXTURE
        cls.save = verify_run_generation.load_save(cls.path)
        cls.act = cls.save["acts"][cls.save["current_act_index"]]
        cls.seed = cls.save["rng"]["seed"]
        cls.emu = verify_run_generation.emulator_generation(cls.seed)
        cls.names = verify_run_generation.encounter_names()

    def test_fixture_carries_no_profile_data(self):
        # The distilled fixture must not leak play history or account identifiers.
        raw = self.path.read_text()
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
            ),
        )

    def test_elite_encounters_match(self):
        self.assertTrue(
            _quiet(
                verify_run_generation.compare_sequence,
                "elite",
                self.emu["elite"],
                self.act["rooms"]["elite_encounter_ids"],
                self.names,
            ),
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

    def test_map_matches_row_for_row(self):
        """The whole map — every row, column and point type — matches the capture.

        The AAB capture previously allowed one known residual (row 1 held a node at the
        wrong column). That is fixed: start->row-1 edges are now wired in column order,
        as the game's ForEachInRow does, which is what decides the pre-shuffle order of
        each duplicate-segment group and therefore which node pruning keeps.
        """
        live_map = self.act["saved_map"]
        live = {
            (p["coord"]["col"], p["coord"]["row"]): p["type"]
            for p in live_map["points"]
        }
        for key in ("start", "boss"):
            node = live_map.get(key)
            if node:
                live[node["coord"]["col"], node["coord"]["row"]] = node["type"]

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
        self.assertEqual(set(), mismatched, "map diverged from the live capture")

    def test_map_edges_match(self):
        """Connectivity, which node positions alone do not pin down.

        Two maps can place every dot identically and still wire them differently, and
        the wiring is what constrains the rest: a node's legal columns come from its
        parents' and children's columns. Seed "L4CEF9U55L" surfaced exactly that — one
        extra edge the game had pruned, which then pinned a node to the wrong column.
        """
        self.assertTrue(
            _quiet(
                verify_run_generation.compare_edges,
                self.emu["edges"],
                self.act["saved_map"],
            ),
            "map edges diverged from the live capture",
        )


# One test class per committed capture, built from the directory rather than written
# out: a capture is only worth taking if it is checked, and headless embarks made them
# cheap enough that hand-maintaining a class per seed would be the thing that lapses.
def _capture_case(fixture: Path) -> type:
    return type(
        f"RunGeneration_{fixture.stem}_Test",
        (RunGenerationChecks, unittest.TestCase),
        {
            "__doc__": f"Ground truth: a live A8 capture on seed {fixture.stem!r}.",
            "FIXTURE": fixture.name,
        },
    )


for _fixture in sorted((FIXTURES / "run_generation").glob("*.json")):
    globals()[f"RunGeneration_{_fixture.stem}_Test"] = _capture_case(_fixture)


class ActCoverageTest(unittest.TestCase):
    """Act 1 is a coin flip, and the two acts run down different branches.

    Different encounter pools, three fewer events in Underdocks (which shifts the
    up-front RNG burn ahead of every encounter grab), and its own boss list. A wall of
    captures that all landed on one act would leave the other completely unchecked, so
    assert the committed set covers both.
    """

    def test_captures_cover_both_act_one_acts(self):
        acts = {
            json.loads(path.read_text())["acts"][0]["id"]
            for path in (FIXTURES / "run_generation").glob("*.json")
        }
        self.assertEqual({"ACT.OVERGROWTH", "ACT.UNDERDOCKS"}, acts)


class ActSelectionFixtureTest(unittest.TestCase):
    """Ground truth: which act 1 each seed actually rolled, over a whole build's runs.

    Act 1 is one coin flip, so the run-generation captures above can only ever confirm
    the branch they happen to land on. The profile's run history records the seed and
    rolled act of every finished run, which turns that flip into a large sample —
    including many Underdocks rolls. See scripts/verify_act_selection.py.
    """

    def test_every_recorded_run_rolled_the_predicted_act(self):
        for path in sorted((FIXTURES / "act_selection").glob("*.json")):
            runs = verify_act_selection.load_fixture(path)
            self.assertTrue(runs, f"{path.name} records no runs")
            verify_act_selection.predict(runs)
            self.assertEqual(
                [],
                [
                    f"{run['seed']}: live {run['act']} != emulator {run['emulator']}"
                    for run in runs
                    if not run["ok"]
                ],
                f"{path.name}: act selection diverged",
            )

    def test_both_act_one_branches_are_covered(self):
        acts = set()
        for path in sorted((FIXTURES / "act_selection").glob("*.json")):
            acts.update(run["act"] for run in verify_act_selection.load_fixture(path))
        self.assertEqual({"OVERGROWTH", "UNDERDOCKS"}, acts)


class CombatCaptureChecks(_TestCaseIfChecking):
    """Everything the live sweep compares at combat start, run offline.

    `scripts/combat_sweep.py` proves a combat against the running game; this pins the
    same comparison to a committed capture so it keeps being true with no game, no
    Steam and no 40 seconds per encounter. The four sections are four different
    generators, so they are asserted separately — a failure should say which one moved:

      deck    the shuffled deck in order (Shuffle stream)
      enemies roster and HP (encounter Rng for composition, Niche for HP)
      intent  each enemy's opening move (MonsterAi, plus per-enemy move tables)
      player  HP/max HP, which is really an "is this the same A8 fight" guard

    A capture records the inputs it needs to be reproduced — seed, encounter, the
    weak/normal context and the run's TotalFloor — because two of those are invisible
    in the state itself and getting either wrong yields a plausible wrong answer.
    """

    FIXTURE = ""

    @classmethod
    def setUpClass(cls) -> None:
        cls.path = FIXTURES / "combat" / cls.FIXTURE
        cls.state = json.loads(cls.path.read_text())
        capture = cls.state.get("capture") or {}
        cls.seed = capture["seed"]
        cls.encounter = capture["encounter"]
        cls.completed = capture["completed_combat_rooms"]
        cls.total_floor = capture["total_floor"]
        cls.ascension = capture["ascension"]
        cls.gen_seed = compare_draw_pile.game_seed(cls.seed)
        cls.live = trace_real_game.summarize_state(cls.state)
        cls.emu = validate_real_game_trace.emulator_initial_summary(
            cls.gen_seed,
            cls.encounter,
            total_floor=cls.total_floor,
            ascension=cls.ascension,
        )

    def test_deck_matches_card_for_card(self):
        for pile in ("hand", "draw"):
            emu = compare_draw_pile.emulator_pile(
                self.gen_seed,
                self.encounter,
                self.completed,
                pile,
                total_floor=self.total_floor,
                ascension=self.ascension,
            )
            live = compare_draw_pile.live_pile(self.state, pile)
            self.assertEqual(
                [(compare_draw_pile.normalize(n), up) for n, up in live],
                [(compare_draw_pile.normalize(n), up) for n, up in emu],
                f"{pile} pile diverged from the live capture",
            )

    def test_enemy_roster_and_hp_match(self):
        live = [
            (e["name"], e["hp"], e["max_hp"]) for e in (self.live.get("enemies") or [])
        ]
        emu = [(e["hp"], e["max_hp"]) for e in (self.emu.get("enemies") or [])]
        self.assertEqual(
            len(live),
            len(emu),
            f"enemy count differs: live {[n for n, _, _ in live]}",
        )
        # Names are live-only (the emulator reports ids), so compare on HP — which is
        # what distinguishes one slime from another anyway.
        self.assertEqual([(hp, mx) for _, hp, mx in live], emu)

    def test_opening_intents_match(self):
        for index, (live_enemy, emu_enemy) in enumerate(
            zip(self.live.get("enemies") or [], self.emu.get("enemies") or []),
        ):
            live_intent = validate_real_game_trace.live_enemy_intent(live_enemy)
            if live_intent is None:
                continue
            emu_intent = (
                emu_enemy.get("intent_type"),
                emu_enemy.get("intent_magnitude"),
            )
            self.assertEqual(
                live_intent[0],
                emu_intent[0],
                f"enemy {index} ({live_enemy.get('name')}) intent type",
            )
            if live_intent[1] is not None:
                # A bare Debuff reports no number live; only compare when it has one.
                self.assertEqual(
                    live_intent[1],
                    emu_intent[1],
                    f"enemy {index} ({live_enemy.get('name')}) intent magnitude",
                )

    def test_player_matches_the_captured_run(self):
        live = self.live["player"]
        emu = self.emu["player"]
        self.assertEqual((live["hp"], live["max_hp"]), (emu["hp"], emu["max_hp"]))
        # 64/80 holds at A8 and A10 alike: the HP-reducing levels (TightBelt,
        # AscendersBane) are both below 8, so nothing between them changes it.
        self.assertEqual((64, 80), (emu["hp"], emu["max_hp"]))


@functools.cache
def _card_slug_to_id() -> dict[str, int]:
    """Map the game's ModelId.Entry to our numeric card id, off the generated table.

    Read from Cards.g.cs rather than the native library because the point is to
    compare against what the *extractor* recorded: if a slug there is wrong, the
    reshuffle sorts wrongly and this map would hide it by being wrong the same way
    — so the values it maps come from the live capture, not from either.
    """
    text = (ROOT / "src" / "Sts2Emulator" / "Generated" / "Cards.g.cs").read_text()
    return {
        match.group(2): int(match.group(1))
        for match in re.finditer(
            r'Id: (\d+), Name: "[^"]*", Entry: "([A-Z0-9_]*)"', text
        )
    }


class FightChecks(_TestCaseIfChecking):
    """Replay a whole fight offline, turn by turn.

    The opening-state checks above prove an enemy's FIRST move. That is a small
    fraction of what an enemy is: Corpse Slug has three moves, and which one it opens
    on is itself a roll — so a capture can pass while the two moves behind it are
    wrong, which is exactly what happened (multi-hit attacks executed at their A9
    per-hit damage while announcing the right A8 total).

    Driving turns with no cards played walks the enemy through its table and puts enemy
    *damage* under test at the same time: the player's HP only stays in sync if every
    attack lands for the same amount. `test_covers_every_declared_move` is the part
    that makes this a fight test rather than a longer opening test — it fails if the
    capture never reached one of the enemy's moves.
    """

    FIXTURE = ""

    @classmethod
    def setUpClass(cls) -> None:
        cls.path = FIXTURES / "combat" / cls.FIXTURE
        cls.state = json.loads(cls.path.read_text())
        capture = cls.state["capture"]
        cls.trace = cls.state["turn_trace"]
        cls.coverage = cls.state["coverage"]
        cls.env = Sts2CombatEnv(
            seed=compare_draw_pile.game_seed(capture["seed"]),
            encounter=capture["encounter"],
            completed_combat_rooms=capture["completed_combat_rooms"],
            total_floor=capture["total_floor"],
            ascension=capture["ascension"],
        )
        obs, _info = cls.env.reset()

        # Max HP the capture granted, applied first: a boss that kills a starter deck
        # before its move table runs out cannot be captured to full coverage without it,
        # and the trace below is of the buffed fight.
        if capture.get("buff_max_hp"):
            cls.env.unwrapped.debug_gain_max_hp(int(capture["buff_max_hp"]))

        # Cards the capture stacked on top of the hand, put back in the same slots. A
        # capture that reaches a Phrog Parasite's Wrigglers only does so because it could
        # kill the parasite, and replaying it with a starter deck replays a different
        # fight that never gets there.
        slugs = _card_slug_to_id()
        for card in capture.get("add_cards") or []:
            entry, _, flag = card.partition(":")
            cls.env.unwrapped.debug_add_card_to_hand(
                slugs[entry.upper()],
                flag.lower() in {"u", "upgraded", "+"},
            )

        cls.emu_turns = []
        for row in cls.trace:
            # Every action the turn took, in order. `action` alone describes only a turn
            # that played nothing, which is every capture taken before --play existed.
            for action in row.get("actions") or [row["action"]]:
                obs, _reward, terminated, truncated, _info = cls.env.step(action)
                if terminated or truncated:
                    break
            cls.emu_turns.append(
                validate_real_game_trace.emulator_trace.summarize_observation(obs),
            )
            if terminated or truncated:
                break

    @classmethod
    def tearDownClass(cls) -> None:
        cls.env.close()

    def test_covers_every_declared_move(self):
        for name, counts in self.coverage.items():
            self.assertIsNotNone(
                counts["declared"],
                f"no move table found for {name}; scripts/enemy_moves.py reads them "
                "from decompiled/, which is gitignored — regenerate it",
            )
            # `seen` counts distinct (type, magnitude) readouts, which is how two
            # attacks of different damage are told apart — but a monster that buffs
            # itself announces ONE move at several magnitudes, so the count can exceed
            # the move table. Seapunk climbs 8, 12, 13, 16 on four hits of Bubble Burp
            # Strength. The signal worth keeping is the shortfall: a capture that never
            # reached a move shows fewer readouts than the table declares.
            self.assertGreaterEqual(
                counts["seen"],
                counts["declared"],
                f"{name}: the capture only ever saw {counts['seen']} readouts for its "
                f"{counts['declared']} moves, so some were never reached",
            )

    def test_every_turn_intents_match(self):
        self.assertEqual(len(self.trace), len(self.emu_turns), "fight ended early")
        for row, emu in zip(self.trace, self.emu_turns):
            live_enemies = row["enemies"]
            # Only the living. The emulator keeps a dead enemy in the roster at 0 HP so
            # an agent's observation has stable slots; the game removes the creature. Any
            # fight the player wins part of — which is every --play capture — otherwise
            # looks like the emulator inventing an extra attacker.
            emu_enemies = combat_sweep.living_emu_enemies(emu)
            self.assertEqual(
                len(live_enemies),
                len(emu_enemies),
                f"turn {row['turn']}: enemy count",
            )
            for index, (live_enemy, emu_enemy) in enumerate(
                zip(live_enemies, emu_enemies),
            ):
                live_intent = live_enemy["intent"]
                if live_intent is None:
                    continue
                self.assertTrue(
                    combat_sweep.intents_agree(
                        tuple(live_intent),
                        (
                            emu_enemy.get("intent_type"),
                            emu_enemy.get("intent_magnitude"),
                        ),
                    ),
                    f"turn {row['turn']} enemy {index} ({live_enemy['name']}): "
                    f"emulator {(emu_enemy.get('intent_type'), emu_enemy.get('intent_magnitude'))} "
                    f"vs live {tuple(live_intent)}",
                )

    def test_every_turn_hand_matches(self):
        """The draw order, which is really the mid-combat reshuffle under test.

        The game re-sorts the pile by ModelId before Fisher-Yates (StableShuffle), so
        the order the shuffle starts from is the slugified card name, not any numeric
        id of ours. Sorting by the wrong key yields the right pile *counts* and the
        wrong cards on top — which reads as a damage bug several turns later, when a
        status card that should still be buried burns the player in hand.
        """
        recorded = [row for row in self.trace if row.get("live_hand") is not None]
        if not recorded:
            self.skipTest("capture predates per-turn hands")
        slug_to_id = _card_slug_to_id()
        for row, emu in zip(recorded, self.emu_turns):
            expected = [slug_to_id.get(slug, slug) for slug in row["live_hand"]]
            actual = [card["id"] for card in emu["player"]["hand"]]
            self.assertEqual(
                expected,
                actual,
                f"turn {row['turn']} hand, in order: live {row['live_hand']}",
            )

    def test_every_turn_player_hp_matches(self):
        """Enemy damage, indirectly: HP only tracks if every attack lands for the same."""
        for row, emu in zip(self.trace, self.emu_turns):
            player = emu["player"]
            self.assertEqual(
                (row["player_hp"], row["player_max_hp"]),
                (player["hp"], player["max_hp"]),
                f"turn {row['turn']} player HP",
            )


def _fight_case(fixture: Path) -> type:
    return type(
        f"Fight_{fixture.stem.replace('-', '_')}_Test",
        (FightChecks, unittest.TestCase),
        {
            "__doc__": f"Ground truth: a live A8 fight, turn by turn, {fixture.stem}.",
            "FIXTURE": fixture.name,
        },
    )


for _fight_fixture in sorted((FIXTURES / "combat").glob("*.json")):
    if json.loads(_fight_fixture.read_text()).get("turn_trace"):
        globals()[f"Fight_{_fight_fixture.stem.replace('-', '_')}_Test"] = _fight_case(
            _fight_fixture,
        )


# One class per committed capture, same as the run-generation fixtures: a capture is
# only worth taking if something checks it.
def _combat_case(fixture: Path) -> type:
    return type(
        f"Combat_{fixture.stem.replace('-', '_')}_Test",
        (CombatCaptureChecks, unittest.TestCase),
        {
            "__doc__": f"Ground truth: a live A8 combat capture, {fixture.stem}.",
            "FIXTURE": fixture.name,
        },
    )


for _combat_fixture in sorted((FIXTURES / "combat").glob("*.json")):
    if "capture" in json.loads(_combat_fixture.read_text()):
        globals()[f"Combat_{_combat_fixture.stem.replace('-', '_')}_Test"] = (
            _combat_case(_combat_fixture)
        )


class CombatCoverageTest(unittest.TestCase):
    """The committed captures have to cover what actually varies.

    Both acts, both encounter pools, and at least one encounter whose composition is
    rolled from the per-encounter RNG (Slimes, Corpse Slugs) — that last one is the
    only thing that would catch a regression in EncounterRng, and it is exactly the
    part that was wrong for months.
    """

    ROLLED_COMPOSITION = {"slimes", "large-slimes", "corpse-slugs"}

    @staticmethod
    def _encounters() -> set[str]:
        encounters = set()
        for path in (FIXTURES / "combat").glob("*.json"):
            capture = json.loads(path.read_text()).get("capture") or {}
            name = capture.get("encounter")
            if isinstance(name, str):
                encounters.add(name)
        return encounters

    def test_covers_both_acts_and_pools(self):
        encounters = self._encounters()
        self.assertTrue(
            encounters & set(combat_sweep.WEAK_BY_ACT["overgrowth"]),
            "no Overgrowth weak-pool capture",
        )
        self.assertTrue(
            encounters & set(combat_sweep.WEAK_BY_ACT["underdocks"]),
            "no Underdocks weak-pool capture",
        )
        self.assertTrue(
            encounters
            & (
                set(combat_sweep.NORMAL_BY_ACT["overgrowth"])
                | set(combat_sweep.NORMAL_BY_ACT["underdocks"])
            ),
            "no normal-pool capture",
        )

    def test_covers_an_encounter_that_rolls_its_composition(self):
        self.assertTrue(self._encounters() & self.ROLLED_COMPOSITION)

    def test_covers_more_than_one_ascension(self):
        """A8 and A10 disagree on nearly every enemy's damage.

        Every `Ascension.Value(level, high, low)` pair has two branches and A8 only ever
        reaches one of them, so a suite captured entirely at A8 cannot tell a correct
        pair from a correct low value next to a wrong high one.
        """
        levels = {
            (json.loads(path.read_text()).get("capture") or {}).get("ascension")
            for path in (FIXTURES / "combat").glob("*.json")
        } - {None}
        self.assertGreater(len(levels), 1, f"only captured at {levels}")


class LegacyCombatFixtureTest(unittest.TestCase):
    """The original "ABCDEF" CorpseSlugsWeak capture, which predates capture metadata.

    Kept as-is rather than re-captured: its run's save is long gone, and it is the
    capture the whole opening-hand result was originally built on. New captures come
    from combat_sweep.py and get the full comparison above.
    """

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
