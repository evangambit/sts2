"""The Python side must read the observation's layout, not restate it.

Every decoder here used to carry the offsets as literals -- ``obs[8 + i * 2]`` for the
hand, ``54 + enemy * 15`` for the enemies -- copied into four modules. When a card slot
grew from two fields to four to carry the enchantment, all four went silently wrong:
``scripts/trace.py`` decoded zero enemies and 77 live-fixture tests failed at once, none
of them about the observation.

The one that mattered was quieter. ``sts2_gym.commands`` resolves which hand index holds
the card a replay wants to play, and with the wrong stride it resolved the wrong index --
so the replay played different cards and the run diverged 150 steps later, with the player
alive at 4 hp where the capture had them dead. A layout change had turned into what looked
like a simulation bug.

So these tests do not check the numbers. They check that what the decoders report is what
the emulator actually holds, which is true whatever the layout becomes.
"""

import importlib
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src"))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))

sts2_gym = importlib.import_module("sts2_gym")
native = importlib.import_module("sts2_gym.native")
commands = importlib.import_module("sts2_gym.commands")
run_constants = importlib.import_module("sts2_gym.run_constants")
trace = importlib.import_module("trace")

Sts2CombatEnv = sts2_gym.Sts2CombatEnv


class ObservationLayoutTests(unittest.TestCase):
    def setUp(self):
        self.env = Sts2CombatEnv(seed=0)
        self.obs, _ = self.env.reset(seed=0)

    def tearDown(self):
        self.env.close()

    def test_the_layout_adds_up_to_the_reported_size(self):
        """Every slot the layout describes has to fit inside the buffer."""
        last_enemy_end = (
            native.OBS_ENEMY_OFFSET + native.MAX_ENEMIES * native.OBS_ENEMY_SLOT_SIZE
        )

        self.assertLessEqual(
            native.OBS_HAND_OFFSET + native.OBS_MAX_HAND * native.OBS_CARD_SLOT_SIZE,
            native.OBS_PLAYER_BUFF_OFFSET,
        )
        self.assertLessEqual(
            native.OBS_PLAYER_BUFF_OFFSET + native.OBS_MAX_PLAYER_BUFFS * 2,
            native.OBS_ENEMY_OFFSET,
        )
        self.assertLessEqual(last_enemy_end, native.OBS_SECONDARY_INTENT_OFFSET)
        self.assertLessEqual(
            native.OBS_SECONDARY_INTENT_OFFSET + native.MAX_ENEMIES * 2,
            native.OBS_SIZE,
        )

    def test_the_decoded_hand_is_the_hand_the_emulator_holds(self):
        """scripts/trace.py against the emulator's own ordered pile dump."""
        summary = trace.summarize_observation(self.obs)

        self.assertEqual(
            [card for card, _ in self.env.get_pile("hand")],
            [entry["id"] for entry in summary["player"]["hand"]],
        )

    def test_commands_resolves_the_same_hand_indices(self):
        """The replay adapter resolves the same hand indices as the emulator.

        It reads the hand separately from trace.py, and IT is the one that turns a
        captured play into an emulator action -- so a stride it gets wrong shows up as a
        divergent run, not as a bad observation.
        """
        hand = [card for card, _ in self.env.get_pile("hand")]

        self.assertEqual(len(hand), commands.hand_count(self.obs))
        for index, card_id in enumerate(hand):
            self.assertEqual(
                card_id,
                commands.hand_card_id(self.obs, index),
                f"hand slot {index} decoded as a different card",
            )

    def test_an_empty_slot_past_the_hand_reads_as_nothing(self):
        hand = [card for card, _ in self.env.get_pile("hand")]

        for index in range(len(hand), native.OBS_MAX_HAND):
            self.assertEqual(0, commands.hand_card_id(self.obs, index))

    def test_the_decoded_enemies_are_the_enemies_the_fight_has(self):
        summary = trace.summarize_observation(self.obs)
        enemies = summary["enemies"]

        self.assertGreater(len(enemies), 0, "a fresh combat has enemies")
        for enemy in enemies:
            self.assertGreater(enemy["max_hp"], 0)
            self.assertLessEqual(enemy["hp"], enemy["max_hp"])


class MapChoiceLayoutTests(unittest.TestCase):
    """The map's choice width comes from the emulator, not from a copy of it.

    ``run_constants.MAP_CHOICES`` was a literal 4 while the emulator's arrays widened to
    a whole row for Winged Boots' free travel. A stale copy does not fail: it reads the
    first four options and drops the rest, so a run offered seven nodes silently could
    not pick three of them.
    """

    def test_map_choices_matches_the_emulator(self):
        self.assertEqual(
            native.RUN_OBS_LAYOUT["map_choices"],
            run_constants.MAP_CHOICES,
        )

    def test_the_map_blocks_sit_where_the_emulator_says(self):
        layout = native.RUN_OBS_LAYOUT

        self.assertEqual(
            layout["map_node_type_offset"] + layout["map_choices"],
            layout["map_choice_offset"],
        )
        self.assertLessEqual(
            layout["map_choice_offset"] + layout["map_choices"],
            layout["scalars"],
        )


class TargetMapTests(unittest.TestCase):
    """A captured target id has to resolve through the map, never past it.

    ``translate_target`` falls back to the entity id's numeric suffix when the map has no
    key for it -- and that suffix is the game's position among LIVING creatures, which
    stops matching the emulator's index the moment anything dies. So a key the map spells
    even one character differently does not fail, it quietly names another creature.
    """

    def test_a_hyphenated_name_is_slugged_the_way_the_game_spells_it(self):
        enemies = [
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_0", "hp": 22},
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_1", "hp": 21},
        ]

        target_map = commands.build_target_map(enemies)

        self.assertEqual(0, target_map["TWO_TAILED_RAT_0"])
        self.assertEqual(1, target_map["TWO_TAILED_RAT_1"])

    def test_the_dead_are_skipped_so_the_value_is_an_ordinal(self):
        enemies = [
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_0", "hp": 0},
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_0", "hp": 22},
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_1", "hp": 21},
        ]

        target_map = commands.build_target_map(enemies)

        self.assertEqual(0, target_map["TWO_TAILED_RAT_0"])
        self.assertEqual(1, target_map["TWO_TAILED_RAT_1"])

    def test_a_mapped_target_never_falls_back_to_the_suffix(self):
        enemies = [
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_0", "hp": 22},
            {"name": "Two-Tailed Rat", "entity_id": "TWO_TAILED_RAT_1", "hp": 21},
        ]
        target_map = {
            name: ordinal + 1
            for name, ordinal in commands.build_target_map(enemies).items()
        }

        resolved = commands.translate_target(
            {"target": "TWO_TAILED_RAT_1"},
            target_map,
        )

        self.assertEqual(2, resolved)


if __name__ == "__main__":
    unittest.main()
