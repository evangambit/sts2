"""Tests for the ordered-pile dump and the string-seed -> gen-seed derivation.

Together these are the two halves the draw-pile differential test needs: the
emulator's *ordered* draw pile (the observation vector only carries counts), and
the seed bridge that lines the emulator's RNG streams up with a live run.
"""

import importlib
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src"))

sts2_gym = importlib.import_module("sts2_gym")
seeds = importlib.import_module("sts2_gym.seeds")

Sts2CombatEnv = sts2_gym.Sts2CombatEnv

# The live "ABCDEF" custom run this fork is validated against.
ABCDEF_GEN_SEED = 3334281563

STRIKE = 472
DEFEND = 131
BASH = 30
ASCENDERS_BANE = 10001
STARTER_DECK = [STRIKE] * 5 + [DEFEND] * 4 + [BASH, ASCENDERS_BANE]


class SeedDerivationTest(unittest.TestCase):
    def test_derives_known_gen_seeds(self):
        # Pinned by the C# test RunRngSet_DerivesGameSeedForStringSeed.
        self.assertEqual(ABCDEF_GEN_SEED, seeds.game_seed("ABCDEF"))
        self.assertEqual(3452614542, seeds.game_seed("0"))

    def test_gen_seed_is_unsigned_32_bit(self):
        for seed in ("", "A", "ABCDEF", "0", "a-longer-seed-string"):
            self.assertTrue(0 <= seeds.game_seed(seed) <= 0xFFFFFFFF, seed)


class OrderedPileTest(unittest.TestCase):
    def setUp(self):
        self.env = Sts2CombatEnv(
            seed=ABCDEF_GEN_SEED,
            encounter="corpse-slugs",
            completed_combat_rooms=0,
        )
        self.obs, _ = self.env.reset()

    def tearDown(self):
        self.env.close()

    def test_piles_partition_the_starter_deck(self):
        drawn = [card for card, _ in self.env.get_pile("hand")]
        remaining = [card for card, _ in self.env.get_pile("draw")]

        self.assertEqual(sorted(STARTER_DECK), sorted(drawn + remaining))
        self.assertEqual([], self.env.get_pile("discard"))
        self.assertEqual([], self.env.get_pile("exhaust"))

    def test_draw_pile_length_matches_the_observation_count(self):
        self.assertEqual(int(self.obs[5]), len(self.env.get_pile("draw")))

    def test_hand_holds_the_opening_five(self):
        self.assertEqual(5, len(self.env.get_pile("hand")))

    def test_pile_dump_is_ordered_not_sorted(self):
        # Guards against anyone "helpfully" sorting this the way the STS2MCP mod
        # sorts its draw_pile for display — that would defeat the whole purpose.
        pile = [card for card, _ in self.env.get_pile("draw")]
        self.assertNotEqual(sorted(pile), pile, "draw pile came back sorted")

    def test_reports_upgrade_state_per_card(self):
        for _, upgraded in self.env.get_pile("draw"):
            self.assertIsInstance(upgraded, bool)
        # A fresh starter deck holds no upgraded cards.
        self.assertFalse(any(up for _, up in self.env.get_pile("draw")))

    def test_rejects_an_unknown_pile_name(self):
        with self.assertRaises(KeyError):
            self.env.get_pile("nope")


if __name__ == "__main__":
    unittest.main()
