"""The hand-play CLI must be able to draw every screen the run can put in front of it.

A renderer is only ever exercised by the screens someone happened to walk onto. A shop
that crashes, an event whose options are numbered wrong, a card-select grid read against
the deck instead of the offer -- none of that shows up until a run reaches it, and a run
reaches a Crystal Sphere about once in a hundred.

So this walks random runs and renders every step of them, and asserts the two things a
screen owes the player: it never offers a move the engine would refuse, and it never
drops a legal one without saying so. A label that names the wrong thing is beyond what a
test can see; a screen that silently loses an option is not.
"""

import importlib
import random
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src"))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))

play = importlib.import_module("play")
run_constants = importlib.import_module("sts2_gym.run_constants")
Sts2RunEnv = importlib.import_module("sts2_gym.run_env").Sts2RunEnv

# Enough runs, played long enough, to reach shops, rest sites, events, card-select
# screens and the reward screens between them. Kept modest because each is a real run.
SEEDS = ("PLAYCLI1", "PLAYCLI2", "PLAYCLI3", "PLAYCLI4", "PLAYCLI5", "PLAYCLI6")
STEPS = 400


class ScreenTests(unittest.TestCase):
    def test_every_screen_matches_the_mask_it_was_built_from(self):
        seen_phases = set()
        for seed in SEEDS:
            rng = random.Random(seed)  # noqa: S311 - walking a run, not crypto
            env = Sts2RunEnv(seed=seed, max_episode_steps=STEPS + 1, max_floors=64)
            obs, info = env.reset()
            try:
                for _ in range(STEPS):
                    legal = {int(a) for a in np.flatnonzero(env.action_masks())}
                    if not legal:
                        break
                    phase = int(info["phase"])
                    seen_phases.add(phase)
                    screen = play.build_screen(env, obs, info, legal)

                    where = (
                        f"seed {seed}, phase {phase} ({play.PHASE_NAMES.get(phase)})"
                    )
                    # Never offer what the mask forbids: the engine answers an illegal
                    # action with a refusal and no state change, which reads as a screen
                    # that ignored the player.
                    self.assertLessEqual(
                        set(screen.choices),
                        legal,
                        f"{where}: offered an illegal action",
                    )
                    # And never quietly drop one. A legal action may be withheld -- the
                    # card-reward mask offers slots the engine then refuses -- but it has
                    # to be withheld visibly, or a screen missing an option looks the same
                    # as a screen that has none.
                    self.assertLessEqual(
                        legal,
                        set(screen.choices) | set(screen.disabled),
                        f"{where}: a legal action appears nowhere on the screen",
                    )
                    # Rendering is where a missing name or a bad slice actually throws.
                    rendered = play.render(info, seed, screen, colour=False)
                    self.assertIn(screen.title, rendered)

                    # Walk on what the SCREEN offers, so the run goes where a player
                    # would take it rather than into actions the engine refuses.
                    action = rng.choice(sorted(screen.choices) or sorted(legal))
                    target = -1
                    if action in screen.targeted or action in screen.aimable:
                        living = importlib.import_module(
                            "sts2_gym.commands",
                        ).living_enemy_indices(obs)
                        target = living[0] if living else -1
                    obs, _reward, terminated, truncated, info = env.step(
                        action,
                        target=target,
                    )
                    if terminated or truncated:
                        break
            finally:
                env.close()

        # Combat, map and the reward screens are unmissable; the point of the assertion is
        # that the walk was long enough to be worth anything at all.
        self.assertGreaterEqual(
            len(seen_phases),
            5,
            f"only reached phases {sorted(seen_phases)} -- the walk is too shallow to test much",
        )


class NameTests(unittest.TestCase):
    """The name tables are parsed out of the emulator's source, so parsing is the risk."""

    def test_tables_are_populated_and_agree_with_the_engine(self):
        names = importlib.import_module("sts2_gym.names")
        self.assertGreater(len(names.cards()), 500)
        self.assertGreater(len(names.relics()), 200)
        self.assertGreater(len(names.potions()), 50)
        self.assertGreater(len(names.enemies()), 100)

        # Ordinals, not ids: the observation carries the enum's position, so a parser
        # that lost the order would rename every buff in a readout without failing.
        self.assertEqual(names.buff_name(0), "Strength")
        self.assertEqual(names.intent_name(0), "Attack")

        strike = names.card(472)
        assert strike is not None, "card 472 is Strike and has to be in the table"
        self.assertEqual(strike.name, "StrikeIronclad")
        self.assertEqual(strike.cost, 1)
        self.assertTrue(strike.targets_an_enemy())
        self.assertEqual(
            strike.damage_for(True),
            strike.base_damage + strike.upgrade_damage,
        )


class InfoTests(unittest.TestCase):
    def test_run_info_names_the_enemies_it_is_fighting(self):
        """The observation says an enemy's hp and never what it is; the run must."""
        env = Sts2RunEnv(seed="PLAYCLI1", max_episode_steps=400, max_floors=64)
        obs, info = env.reset()
        try:
            for _ in range(400):
                legal = [int(a) for a in np.flatnonzero(env.action_masks())]
                if not legal:
                    break
                if int(info["phase"]) == run_constants.PHASE_COMBAT:
                    enemies = play.read_enemies(obs, info)
                    self.assertTrue(
                        enemies,
                        "a combat with no enemies in the observation",
                    )
                    for enemy in enemies:
                        self.assertGreater(
                            enemy.def_id,
                            0,
                            "an enemy on the field with no def id -- state list 19 is stale",
                        )
                        self.assertFalse(
                            play.names.enemy_name(enemy.def_id).startswith("enemy-"),
                            f"def id {enemy.def_id} is not in Enemies.g.cs",
                        )
                    return
                obs, _reward, terminated, truncated, info = env.step(legal[0])
                if terminated or truncated:
                    break
            self.fail("never reached a combat")
        finally:
            env.close()


class DeckSelectionTests(unittest.TestCase):
    def test_a_deck_screen_says_what_answering_it_does(self):
        """One screen answers four questions, and the cards on it tell them apart in none.

        This seed's first Neow blessing opens a REMOVAL on floor one, so the screen is
        reachable by taking the first legal action every time -- no fixture needed. A
        screen titled "choose a card" over a deck the run is about to cut from is a
        decision made blind, which is what `DeckSelection` is carried here to prevent.
        """
        env = Sts2RunEnv(seed="DECKSEL0002", max_episode_steps=400, max_floors=64)
        _obs, info = env.reset()
        try:
            for _ in range(400):
                legal = [int(a) for a in np.flatnonzero(env.action_masks())]
                if not legal:
                    break
                if int(info["phase"]) == run_constants.PHASE_TRANSFORM_SELECT:
                    self.assertEqual(play.deck_selection_purpose(info), "remove")
                    return
                _obs, _reward, terminated, truncated, info = env.step(legal[0])
                if terminated or truncated:
                    break
            self.fail("never reached a card-select screen")
        finally:
            env.close()


if __name__ == "__main__":
    unittest.main()
