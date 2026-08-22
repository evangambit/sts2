import ctypes
import importlib
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src"))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))

replay_full_run_trace = importlib.import_module("replay_full_run_trace")
compare_traces = importlib.import_module("compare_traces")
sts2_gym = importlib.import_module("sts2_gym")
native = importlib.import_module("sts2_gym.native")
run_env = importlib.import_module("sts2_gym.run_env")

Sts2CombatEnv = sts2_gym.Sts2CombatEnv
Sts2RunEnv = sts2_gym.Sts2RunEnv
NODE_NORMAL = run_env.NODE_NORMAL
PHASE_ANCIENT = run_env.PHASE_ANCIENT
PHASE_CARD_REWARD = run_env.PHASE_CARD_REWARD
PHASE_COMBAT = run_env.PHASE_COMBAT
PHASE_MAP = run_env.PHASE_MAP
PHASE_RELIC_REWARD = run_env.PHASE_RELIC_REWARD

HAND_ID_INDICES = range(8, 28, 2)
ENEMY_INTENT_INDICES = (47, 87, 127)
ASCENDERS_BANE_OBS_ID = 10001


def first_valid_action(env: Sts2CombatEnv) -> int:
    return int(np.flatnonzero(env.action_masks())[0])


class Sts2GymTests(unittest.TestCase):
    def test_native_run_api_resets_to_ancient_phase(self):
        handle = native.run_create()
        try:
            obs = (ctypes.c_int * native.RUN_OBS_SIZE)()

            self.assertEqual(native.run_reset(handle, "0", obs), 0)
            self.assertEqual(native.run_phase(handle), PHASE_ANCIENT)
            # 35 scalars, then the deck and the relics card by card and relic by relic.
            layout = native.RUN_OBS_LAYOUT
            self.assertEqual(
                native.RUN_OBS_SIZE,
                native.OBS_SIZE
                + layout["scalars"]
                + layout["max_deck"] * layout["deck_slot_size"]
                + layout["max_relics"] * layout["relic_slot_size"]
                + layout["shop_slots"] * layout["shop_slot_size"],
            )
            self.assertEqual(layout["deck_offset"], layout["scalars"])
            # Wide enough for the Crystal Sphere's board -- 121 cells, either tool --
            # which is what pushed this past the 32 a shop needed.
            self.assertEqual(native.RUN_MAX_ACTIONS, 256)
            self.assertEqual(native.RUN_INFO_SIZE, 11)

            run_offset = native.OBS_SIZE
            self.assertEqual(
                list(obs[run_offset : run_offset + 9]),
                [PHASE_ANCIENT, 1, 2, 11, 99, 64, 80, 1, NODE_NORMAL],
            )
            self.assertEqual(
                list(native.run_info(handle)),
                [PHASE_ANCIENT, 1, 2, 11, 99, 64, 80, 1, NODE_NORMAL, 0, 0],
            )
            self.assertEqual(
                list(native.run_action_mask(handle, native.RUN_MAX_ACTIONS))[:6],
                [1, 1, 1, 0, 0, 0],
            )
        finally:
            native.run_destroy(handle)

    def test_native_run_api_exposes_state_lists(self):
        handle = native.run_create()
        try:
            obs = (ctypes.c_int * native.RUN_OBS_SIZE)()
            self.assertEqual(native.run_reset(handle, "0", obs), 0)

            self.assertEqual(
                native.run_state_list(handle, 0, 16),
                (472, 472, 472, 472, 472, 131, 131, 131, 131, 30, 10001),
            )
            self.assertEqual(native.run_state_list(handle, 1, 8), (36,))
            self.assertEqual(native.run_state_list(handle, 2, 3), (0, 0, 0))
            # Neow's three offers. Locked to catch drift; the live-derived anchor is
            # RunEngineTests.NeowOptions_MatchTheLiveGame.
            self.assertEqual(native.run_state_list(handle, 3, 3), (124, 231, 240))
        finally:
            native.run_destroy(handle)

    def test_native_run_api_starts_and_steps_combat_in_process(self):
        handle = native.run_create()
        try:
            obs = (ctypes.c_int * native.RUN_OBS_SIZE)()

            self.assertEqual(native.run_reset(handle, "0", obs), 0)
            self.assertEqual(
                native.run_start_combat(
                    handle,
                    [472, 472, 472, 472, 472, 131, 131, 131, 131, 30, 10001],
                    1,
                    [36],
                    64,
                    80,
                    [0, 0, 0],
                    99,
                    0,
                    obs,
                ),
                0,
            )
            self.assertEqual(native.run_phase(handle), PHASE_COMBAT)
            self.assertEqual(native.run_encounter_id(handle), 1)
            self.assertGreater(native.run_get_shuffle_rng_call_count(handle), 0)
            self.assertGreater(native.run_get_niche_rng_call_count(handle), 0)
            self.assertTrue(any(native.run_action_mask(handle, native.RUN_MAX_ACTIONS)))

            reward = (ctypes.c_float * 1)()
            terminal = (ctypes.c_int * 1)()
            truncated = (ctypes.c_int * 1)()
            self.assertEqual(
                native.run_step(handle, 10, -1, obs, reward, terminal, truncated),
                0,
            )
            self.assertEqual(truncated[0], 0)
        finally:
            native.run_destroy(handle)

    def test_run_env_uses_native_run_api_by_default(self):
        env = Sts2RunEnv(seed="0", max_episode_steps=2)
        try:
            obs, info = env.reset()

            self.assertEqual(int(obs[native.OBS_SIZE]), PHASE_ANCIENT)
            self.assertEqual(info["phase"], PHASE_ANCIENT)
            self.assertEqual(
                list(env.action_masks()[:6]),
                [True, True, True, False, False, False],
            )

            next_obs, reward, terminated, truncated, _ = env.step(0)
            # Seed 0 opens on Kaleidoscope, whose two card rewards are offered on the
            # rewards screen and claimed one at a time — the card reward is a claim away.
            self.assertEqual(int(next_obs[native.OBS_SIZE]), PHASE_RELIC_REWARD)
            self.assertEqual(reward, 0.0)
            self.assertFalse(terminated)
            self.assertFalse(truncated)
            self.assertTrue(any(env.action_masks()[:4]))
        finally:
            env.close()

    def test_run_env_accepts_sts2mcp_style_commands(self):
        env = Sts2RunEnv(seed="0", max_episode_steps=2)
        try:
            obs, info = env.reset()
            self.assertEqual(info["phase"], PHASE_ANCIENT)

            obs, reward, terminated, truncated, info = env.command(
                {"action": "choose_event_option", "index": 0},
            )

            # The rewards screen first: seed 0's Neow option 0 is Kaleidoscope, which
            # offers two card rewards there rather than opening one directly.
            self.assertEqual(int(obs[native.OBS_SIZE]), PHASE_RELIC_REWARD)
            self.assertEqual(info["phase"], PHASE_RELIC_REWARD)
            self.assertEqual(reward, 0.0)
            self.assertFalse(terminated)
            self.assertFalse(truncated)
        finally:
            env.close()

    def test_reset_is_deterministic_for_same_seed(self):
        first = Sts2CombatEnv(seed=123)
        second = Sts2CombatEnv(seed=123)
        try:
            first_obs, _ = first.reset()
            second_obs, _ = second.reset()

            self.assertTrue(np.array_equal(first_obs, second_obs))
        finally:
            first.close()
            second.close()

    def test_action_mask_excludes_ascenders_bane(self):
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(128):
                obs, _ = env.reset(seed=seed)
                hand_ids = [int(obs[i]) for i in HAND_ID_INDICES]
                if ASCENDERS_BANE_OBS_ID not in hand_ids:
                    continue

                bane_index = hand_ids.index(ASCENDERS_BANE_OBS_ID)
                mask = env.action_masks()

                self.assertFalse(mask[bane_index])
                self.assertTrue(
                    mask[len([card_id for card_id in hand_ids if card_id != 0])],
                )
                return

            self.fail("No tested seed put Ascender's Bane in the opening hand.")
        finally:
            env.close()

    def test_episode_truncates_at_step_cap(self):
        env = Sts2CombatEnv(seed=0, max_episode_steps=1)
        try:
            env.reset()
            _, _, terminated, truncated, _ = env.step(first_valid_action(env))

            self.assertFalse(terminated)
            self.assertTrue(truncated)
        finally:
            env.close()

    def test_info_reports_encounter_identity(self):
        seen = set()
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(64):
                _, info = env.reset(seed=seed)
                self.assertIsInstance(info["encounter_id"], int)
                self.assertIsInstance(info["encounter"], str)
                self.assertNotEqual(info["encounter"], "none")
                seen.add(info["encounter"])

            self.assertGreaterEqual(len(seen), 8)
        finally:
            env.close()

    def test_reset_can_force_encounter(self):
        env = Sts2CombatEnv(seed=0, encounter="chompers")
        try:
            _, info = env.reset()
            self.assertEqual(info["encounter"], "chompers")

            _, info = env.reset(options={"encounter": "cultists"})
            self.assertEqual(info["encounter"], "cultists")
        finally:
            env.close()

    def test_enemy_status_move_adds_cards_to_discard(self):
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(128):
                obs, info = env.reset(seed=seed)
                # "slimes-weak" since id 3 is the game's SlimesWeak — it was
                # mislabelled "slimes", which collided with SlimesNormal.
                if info["encounter"] not in {"chompers", "slimes-weak"}:
                    continue
                if not any(int(obs[i]) == 3 for i in ENEMY_INTENT_INDICES):
                    continue

                end_turn = len(
                    [int(obs[i]) for i in HAND_ID_INDICES if int(obs[i]) != 0],
                )
                obs, _, _, _, _ = env.step(end_turn)

                self.assertGreaterEqual(int(obs[6]), 6)
                return

            self.fail("No tested seed produced an opening enemy status move.")
        finally:
            env.close()

    def test_full_run_trace_boundaries_include_floor_and_combat_edges(self):
        trace = [
            {"summary": {"state_type": "event", "run": {"floor": 1}}},
            {"summary": {"state_type": "map", "run": {"floor": 1}}},
            {"summary": {"state_type": "monster", "run": {"floor": 1}}},
            {"summary": {"state_type": "monster", "run": {"floor": 1}}},
            {"summary": {"state_type": "card_reward", "run": {"floor": 2}}},
        ]

        self.assertEqual(replay_full_run_trace.boundary_indices(trace), [0, 2, 4])

    def test_full_run_trace_boundary_compare_reports_first_mismatch(self):
        reference = [
            {
                "summary": {
                    "state_type": "event",
                    "run": {"floor": 1},
                    "player": {"hp": 64, "max_hp": 80, "gold": 99},
                },
            },
            {
                "summary": {
                    "state_type": "monster",
                    "run": {"floor": 1},
                    "player": {"hp": 60, "max_hp": 80, "gold": 99},
                    "battle": {"enemies": [{"hp": 20, "max_hp": 20, "block": 0}]},
                },
            },
        ]
        emulator = [
            {
                "summary": {
                    "state_type": "event",
                    "run": {"floor": 1},
                    "player": {"hp": 64, "max_hp": 80, "gold": 99},
                },
            },
            {
                "summary": {
                    "state_type": "monster",
                    "run": {"floor": 1},
                    "player": {"hp": 61, "max_hp": 80, "gold": 99},
                    "battle": {"enemies": [{"hp": 18, "max_hp": 20, "block": 0}]},
                },
            },
        ]

        diffs = replay_full_run_trace.compare_boundary_snapshots(
            reference,
            emulator,
            replay_full_run_trace.DEFAULT_BOUNDARY_FIELDS,
        )

        self.assertEqual(
            diffs,
            [
                "step 1 field player.hp: reference=60 emulator=61",
                "step 1 enemy 0 hp: reference=20 emulator=18",
            ],
        )

    def test_full_run_trace_boundary_compare_coalesces_terminal_tail(self):
        reference = [
            {
                "summary": {
                    "state_type": "monster",
                    "run": {"floor": 13},
                    "player": {"hp": 2, "max_hp": 80, "gold": 169},
                    "battle": {"enemies": [{"hp": 1, "max_hp": 29, "block": 0}]},
                },
            },
            {
                "summary": {
                    "state_type": "monster",
                    "run": {"floor": 13},
                    "player": {"hp": 0, "max_hp": 80, "gold": 169},
                    "battle": {"enemies": [{"hp": 1, "max_hp": 29, "block": 0}]},
                },
            },
            {
                "summary": {
                    "state_type": "game_over",
                    "run": {"floor": 13},
                    "player": {"hp": 0, "max_hp": 80, "gold": 169},
                },
            },
        ]
        emulator = [
            {
                "summary": {
                    "state_type": "game_over",
                    "run": {"floor": 13},
                    "player": {"hp": 0, "max_hp": 80, "gold": 169},
                },
            },
        ]
        emulator = [reference[0], *emulator]

        diffs = replay_full_run_trace.compare_boundary_snapshots(
            reference,
            emulator,
            replay_full_run_trace.DEFAULT_BOUNDARY_FIELDS,
        )

        self.assertEqual(diffs, [])

    def test_full_run_replay_unsupported_action_reports_reference_context(self):
        payload = {
            "trace": [
                {
                    "step": 0,
                    "summary": {
                        "state_type": "event",
                        "run": {"floor": 1},
                        "player": {"hp": 64, "max_hp": 80, "gold": 99},
                    },
                },
                {
                    "step": 1,
                    "action": {"action": "select_card", "index": 9},
                    "summary": {
                        "state_type": "card_select",
                        "run": {"floor": 1},
                        "player": {"hp": 64, "max_hp": 80, "gold": 99},
                    },
                },
            ],
        }

        result = replay_full_run_trace.replay_trace(payload, emulator_seed=0)

        self.assertIsNotNone(result.unsupported_action)
        self.assertIn(
            "step 1: unsupported action 'select_card'",
            result.unsupported_action,
        )
        self.assertIn("reference state_type='card_select'", result.unsupported_action)
        self.assertIn("floor=1", result.unsupported_action)

    def test_full_run_replay_coalesces_live_reward_substeps(self):
        payload = {
            "trace": [
                {
                    "step": 0,
                    "summary": {
                        "state_type": "event",
                        "run": {"floor": 1},
                        "player": {"hp": 64, "max_hp": 80, "gold": 99},
                    },
                },
                {
                    "step": 1,
                    "action": {"action": "choose_event_option", "index": 0},
                    "summary": {"state_type": "event", "run": {"floor": 1}},
                },
                {
                    "step": 2,
                    "action": {"action": "claim_reward", "index": 0},
                    "summary": {"state_type": "rewards", "run": {"floor": 1}},
                },
                {
                    "step": 3,
                    "action": {"action": "proceed"},
                    "summary": {"state_type": "map", "run": {"floor": 1}},
                },
                {
                    "step": 4,
                    "action": {"action": "choose_map_node", "index": 0},
                    "summary": {"state_type": "monster", "run": {"floor": 1}},
                },
            ],
        }

        # Seed 3, not 0: this test is about the replay coalescing live reward
        # substeps, and seed 0 now opens on Kaleidoscope, whose TWO card rewards would
        # change the phase sequence for a reason unrelated to coalescing. Seed 3 offers
        # Lost Coffer, which grants exactly one.
        result = replay_full_run_trace.replay_trace(payload, emulator_seed=3)

        self.assertIsNone(result.unsupported_action)
        self.assertEqual(
            [
                "event",
                "card_reward",
                "card_reward",
                "map",
                "monster",
            ],
            [step["summary"]["state_type"] for step in result.payload["trace"]],
        )

    def test_full_run_replay_treats_card_reward_claim_as_noop(self):
        obs = np.zeros(1, dtype=np.int32)

        self.assertIsNone(
            replay_full_run_trace.translate_action(
                {"action": "claim_reward", "index": 0},
                obs,
                {"phase": PHASE_CARD_REWARD},
            ),
        )


class CommittedRunTraceTests(unittest.TestCase):
    """The full-run capture replays end to end against the emulator.

    This is the only test that exercises a whole act the way the game played it:
    every live action from Neow to a natural game over, replayed against the
    emulator and compared field by field at every step. Nothing is tolerated, so
    any divergence at all is a real behavioural difference.
    """

    FIXTURE = (
        Path(__file__).resolve().parents[1]
        / "fixtures"
        / "run_trace"
        / "QS2GYXRKWN-a8.json"
    )

    def _replay(self):
        payload = replay_full_run_trace.load_payload(self.FIXTURE)
        result = replay_full_run_trace.replay_trace(
            payload,
            emulator_seed=payload["seed"],
        )
        return payload, result

    def test_replay_runs_to_the_end_of_the_capture(self):
        _, result = self._replay()
        self.assertIsNone(result.unsupported_action)

    def test_replay_matches_at_every_boundary(self):
        payload, result = self._replay()
        reference = compare_traces.load_trace_from_payload(payload)
        emulator = result.payload["trace"]
        diffs = replay_full_run_trace.compare_boundary_snapshots(
            reference,
            emulator,
            replay_full_run_trace.DEFAULT_BOUNDARY_FIELDS,
        )
        self.assertEqual(diffs, [])

    def test_nothing_diverges_at_any_step(self):
        payload, result = self._replay()
        reference = compare_traces.load_trace_from_payload(payload)
        divergences = replay_full_run_trace.first_divergences(
            reference,
            result.payload["trace"],
            replay_full_run_trace.DEFAULT_PER_STEP_FIELDS,
        )

        self.assertEqual(divergences, [])


class RunDeckObservationTests(unittest.TestCase):
    """The deck and relic block of the run observation, read back through the env.

    The observation is the only thing the agent sees, so every non-combat decision -- card
    reward, shop, rest upgrade, transform -- turns on this block being there and being
    readable against the action mask.
    """

    def test_info_reports_the_deck_the_run_is_actually_holding(self):
        env = sts2_gym.Sts2RunEnv(seed="ABCDEF")
        try:
            _, info = env.reset()
            self.assertEqual(len(info["deck"]), info["deck_size"])
            self.assertTrue(all(card["card_id"] > 0 for card in info["deck"]))
        finally:
            env.close()

    def test_the_deck_block_sits_where_the_layout_says(self):
        env = sts2_gym.Sts2RunEnv(seed="ABCDEF")
        try:
            obs, info = env.reset()
            layout = native.RUN_OBS_LAYOUT
            base = native.OBS_SIZE + layout["deck_offset"]
            for i, card in enumerate(info["deck"]):
                at = base + i * layout["deck_slot_size"]
                self.assertEqual(int(obs[at]), card["card_id"])
            # The slot after the deck is empty, so a reader can stop at the first zero.
            self.assertEqual(
                int(obs[base + len(info["deck"]) * layout["deck_slot_size"]]),
                0,
            )
        finally:
            env.close()

    def test_the_relic_block_carries_what_the_run_is_wearing(self):
        env = sts2_gym.Sts2RunEnv(seed="ABCDEF")
        try:
            _, info = env.reset()
            self.assertEqual(
                [relic["relic_id"] for relic in info["relic_slots"]],
                [relic for relic in info["relics"] if relic != 0],
            )
        finally:
            env.close()

    def test_the_deck_block_follows_the_deck_as_it_grows(self):
        env = sts2_gym.Sts2RunEnv(seed="ABCDEF")
        try:
            env.reset()
            before = env._info()["deck"]
            # Play until a card reward is answered, which is the first thing that can move
            # the deck; stop either way rather than looping the whole run.
            for _ in range(400):
                legal = np.flatnonzero(env.action_masks())
                if legal.size == 0:
                    break
                _, _, terminated, truncated, info = env.step(int(legal[0]))
                if len(info["deck"]) != len(before) or terminated or truncated:
                    break

            info = env._info()
            self.assertEqual(len(info["deck"]), info["deck_size"])
        finally:
            env.close()


class RunShopObservationTests(unittest.TestCase):
    """The shop block: every slot a merchant sells, priced, in action order.

    What the block *means* is checked on the C# side, where a shop can be opened outright;
    a greedy walk from reset dies in the first fight long before it reaches a merchant.
    What is checked here is the decoder -- that ``info["shop_slots"]`` reads exactly the
    numbers the observation holds at the offsets the native layout reports, which is where
    a hard-coded offset would drift.
    """

    def test_the_decoder_reads_the_block_the_layout_points_at(self):
        env = sts2_gym.Sts2RunEnv(seed="ABCDEF")
        try:
            obs, info = env.reset()
            layout = native.RUN_OBS_LAYOUT
            base = native.OBS_SIZE + layout["shop_offset"]
            width = layout["shop_slot_size"]
            slots = info["shop_slots"]

            self.assertEqual(len(slots), layout["shop_slots"])
            self.assertEqual(
                [slot["action"] for slot in slots],
                list(range(len(slots))),
            )
            for i, slot in enumerate(slots):
                self.assertEqual(slot["item_id"], int(obs[base + i * width]))
                self.assertEqual(slot["cost"], int(obs[base + i * width + 1]))
        finally:
            env.close()

    def test_the_shop_block_is_the_last_thing_in_the_observation(self):
        layout = native.RUN_OBS_LAYOUT
        end = (
            native.OBS_SIZE
            + layout["shop_offset"]
            + layout["shop_slots"] * layout["shop_slot_size"]
        )
        self.assertEqual(end, native.RUN_OBS_SIZE)


class RunEnvCloneTests(unittest.TestCase):
    """Forking a run for search.

    A faithful fork is a copy of the world; a resampled one is a world the agent
    cannot tell apart from it. Search needs the second, because every run-level
    stream derives from the run seed -- a faithful fork lets a rollout read the real
    rewards and shop stock rather than plausible ones. See docs/agent-interface.md.
    """

    @staticmethod
    def _env_in_combat() -> "sts2_gym.Sts2RunEnv":
        env = sts2_gym.Sts2RunEnv(seed="QS2GYXRKWN")
        env.reset()
        rng = np.random.default_rng(0)
        for _ in range(40):
            legal = np.flatnonzero(env.action_masks())
            if not len(legal):
                break
            _, _, terminated, truncated, info = env.step(
                int(rng.choice(legal)),
                target=0,
            )
            if terminated or truncated or int(info["phase"]) == run_env.PHASE_COMBAT:
                break
        return env

    @staticmethod
    def _rollout(env, steps: int = 20) -> list[tuple[int, int, int]]:
        seen = []
        for _ in range(steps):
            legal = np.flatnonzero(env.action_masks())
            if not len(legal):
                break
            _, _, terminated, truncated, info = env.step(int(legal[0]), target=0)
            seen.append((int(info["player_hp"]), int(info["gold"]), int(info["floor"])))
            if terminated or truncated:
                break
        return seen

    def test_clone_before_reset_is_refused(self):
        env = sts2_gym.Sts2RunEnv(seed="QS2GYXRKWN")
        with self.assertRaises(RuntimeError):
            env.clone()

    def test_a_fork_starts_from_the_same_observation(self):
        env = self._env_in_combat()
        try:
            for resample in (False, True):
                fork = env.clone(resample_hidden=resample, seed=11)
                try:
                    np.testing.assert_array_equal(fork._obs(), env._obs())
                finally:
                    fork.close()
        finally:
            env.close()

    def test_stepping_a_fork_leaves_the_original_where_it_was(self):
        env = self._env_in_combat()
        fork = env.clone()
        try:
            before = env._obs().copy()
            legal = np.flatnonzero(fork.action_masks())
            fork.step(int(legal[0]), target=0)

            np.testing.assert_array_equal(env._obs(), before)
        finally:
            fork.close()
            env.close()

    def test_two_faithful_forks_play_out_identically(self):
        env = self._env_in_combat()
        first = env.clone(resample_hidden=False)
        second = env.clone(resample_hidden=False)
        try:
            self.assertEqual(self._rollout(first), self._rollout(second))
        finally:
            first.close()
            second.close()
            env.close()

    def test_a_resampled_fork_plays_out_differently(self):
        env = self._env_in_combat()
        faithful = env.clone(resample_hidden=False)
        resampled = env.clone(resample_hidden=True, seed=99)
        try:
            self.assertNotEqual(self._rollout(faithful), self._rollout(resampled))
        finally:
            faithful.close()
            resampled.close()
            env.close()


if __name__ == "__main__":
    unittest.main()
