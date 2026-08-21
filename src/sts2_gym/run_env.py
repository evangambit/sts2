"""Run-level Gymnasium wrapper around the native C# run engine."""

from __future__ import annotations

import ctypes

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native
from . import run_constants as constants
from .commands import execute_command
from .env import ENCOUNTER_NAMES

REWARD_SKIP_ACTION = constants.REWARD_SKIP_ACTION
SHOP_SKIP_ACTION = constants.SHOP_SKIP_ACTION
EVENT_SKIP_ACTION = constants.EVENT_SKIP_ACTION
MAP_CHOICES = constants.MAP_CHOICES
RUN_OBS_SIZE = constants.RUN_OBS_SIZE
RUN_MAX_EPISODE_STEPS = constants.RUN_MAX_EPISODE_STEPS

PHASE_COMBAT = constants.PHASE_COMBAT
PHASE_CARD_REWARD = constants.PHASE_CARD_REWARD
PHASE_MAP = constants.PHASE_MAP
PHASE_REST = constants.PHASE_REST
PHASE_SHOP = constants.PHASE_SHOP
PHASE_RELIC_REWARD = constants.PHASE_RELIC_REWARD
PHASE_COMPLETE = constants.PHASE_COMPLETE
PHASE_EVENT = constants.PHASE_EVENT
PHASE_ANCIENT = constants.PHASE_ANCIENT
PHASE_TRANSFORM_SELECT = constants.PHASE_TRANSFORM_SELECT
PHASE_TREASURE = constants.PHASE_TREASURE

NODE_NONE = constants.NODE_NONE
NODE_NORMAL = constants.NODE_NORMAL
NODE_ELITE = constants.NODE_ELITE
NODE_REST = constants.NODE_REST
NODE_SHOP = constants.NODE_SHOP
NODE_RELIC = constants.NODE_RELIC
NODE_BOSS = constants.NODE_BOSS
NODE_EVENT = constants.NODE_EVENT

ACT_OVERGROWTH = constants.ACT_OVERGROWTH
ACT_UNDERDOCKS = constants.ACT_UNDERDOCKS


class Sts2RunEnv(gym.Env):
    """Gym wrapper for deterministic full-run simulation owned by C#."""

    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int | str = 0,
        max_episode_steps: int = RUN_MAX_EPISODE_STEPS,
        max_floors: int = 16,
    ):
        super().__init__()
        self._seed = seed
        self._max_episode_steps = max_episode_steps
        self._max_floors = max_floors
        self._elapsed_steps = 0
        self._run_handle: int | None = None
        self._run_obs_buf = (ctypes.c_int * native.RUN_OBS_SIZE)()
        self._run_rew_buf = (ctypes.c_float * 1)()
        self._run_terminal_buf = (ctypes.c_int * 1)()
        self._run_truncated_buf = (ctypes.c_int * 1)()
        self.observation_space = spaces.Box(
            low=0,
            high=2**15,
            shape=(native.RUN_OBS_SIZE,),
            dtype=np.int32,
        )
        self.action_space = spaces.Discrete(native.RUN_MAX_ACTIONS)

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        actual_seed = seed if seed is not None else self._seed
        self._seed = actual_seed
        self._elapsed_steps = 0
        if self._run_handle is not None:
            native.run_destroy(self._run_handle)
        self._run_handle = native.run_create()
        status = native.run_reset(self._run_handle, str(actual_seed), self._run_obs_buf)
        if status != 0:
            raise RuntimeError(f"Sts2Run_Reset failed with status {status}.")
        return self._obs(), self._info()

    def clone(self, *, resample_hidden: bool = True, seed: int | None = None):
        """Fork this run into an independent env, for search.

        By default everything the agent has not been shown is resampled: the rewards,
        shop stock and encounter compositions still to come, and the part of the draw
        pile it has not seen placed. That is what makes a fork safe to search with --
        every run-level stream derives from the run seed, so a faithful copy lets a
        rollout read the real future rather than a plausible one.

        Pass resample_hidden=False only to reproduce this exact run, never to search.
        See docs/agent-interface.md.

        Raises:
            RuntimeError: If the environment has not been reset yet.

        """
        if self._run_handle is None:
            raise RuntimeError("Call reset() before clone().")

        resample_seed = (
            int(seed)
            if seed is not None
            else int(self.np_random.integers(0, 2**31 - 1))
        )
        copy = Sts2RunEnv(
            seed=self._seed,
            max_episode_steps=self._max_episode_steps,
            max_floors=self._max_floors,
        )
        copy._run_handle = native.run_clone(
            self._run_handle,
            resample_hidden,
            resample_seed,
            copy._run_obs_buf,
        )
        copy._elapsed_steps = self._elapsed_steps
        return copy

    def step(self, action: int, target: int = -1):
        self._elapsed_steps += 1
        if self._run_handle is None:
            raise RuntimeError("Call reset() before step().")

        status = native.run_step(
            self._run_handle,
            action,
            target,
            self._run_obs_buf,
            self._run_rew_buf,
            self._run_terminal_buf,
            self._run_truncated_buf,
        )
        if status != 0:
            return self._invalid_action()

        terminal = bool(self._run_terminal_buf[0])
        truncated = bool(self._run_truncated_buf[0])
        if not terminal and self._elapsed_steps >= self._max_episode_steps:
            truncated = True
        return (
            self._obs(),
            float(self._run_rew_buf[0]),
            terminal,
            truncated,
            self._info(),
        )

    def command(self, payload: dict, *, target_map=None, reference_step=None):
        """Execute an STS2MCP-style command through the integer action API."""
        reward, terminated, truncated, obs, info = execute_command(
            self,
            payload,
            self._obs(),
            self._info(),
            target_map=target_map,
            reference_step=reference_step,
        )
        return obs, reward, terminated, truncated, info

    def action_masks(self) -> np.ndarray:
        if self._run_handle is None:
            return np.zeros(native.RUN_MAX_ACTIONS, dtype=bool)
        mask_buf = native.run_action_mask(self._run_handle, native.RUN_MAX_ACTIONS)
        return np.ctypeslib.as_array(mask_buf).astype(bool)

    def close(self):
        if self._run_handle is not None:
            native.run_destroy(self._run_handle)
            self._run_handle = None

    def _invalid_action(self):
        return self._obs(), -1.0, False, False, self._info()

    def _obs(self) -> np.ndarray:
        return np.ctypeslib.as_array(self._run_obs_buf).copy()

    def _info(self) -> dict:
        if self._run_handle is None:
            raise RuntimeError("Call reset() before _info().")

        info_buf = native.run_info(self._run_handle)
        obs = np.ctypeslib.as_array(self._run_obs_buf)
        run_offset = native.OBS_SIZE
        phase = int(info_buf[0])
        act = "overgrowth" if int(info_buf[2]) == ACT_OVERGROWTH else "underdocks"
        map_option_coords = native.run_state_list(self._run_handle, 7, MAP_CHOICES * 2)
        return {
            "phase": phase,
            "floor": int(info_buf[1]),
            "act": act,
            "deck_size": int(info_buf[3]),
            "gold": int(info_buf[4]),
            "player_hp": int(info_buf[5]),
            "player_max_hp": int(info_buf[6]),
            "potions": native.run_state_list(self._run_handle, 2, 3),
            "relics": native.run_state_list(self._run_handle, 1, 64),
            "current_node_type": int(info_buf[8]),
            "card_rewards": tuple(int(obs[run_offset + 9 + i]) for i in range(3)),
            "card_reward_upgraded": tuple(
                bool(value) for value in native.run_state_list(self._run_handle, 5, 3)
            ),
            "shop_cards": native.run_state_list(self._run_handle, 8, 7),
            "shop_relics": native.run_state_list(self._run_handle, 9, 3),
            "shop_potions": native.run_state_list(self._run_handle, 10, 3),
            "shop_costs": native.run_state_list(self._run_handle, 4, 14),
            "relic_reward": int(info_buf[10]),
            "pending_rewards": native.run_state_list(self._run_handle, 6, 4),
            "neow_options": native.run_state_list(self._run_handle, 3, 3),
            "potion_reward_odds": 0.4,
            "event_id": int(info_buf[9]),
            "map_choices": (
                tuple(
                    {
                        "node_type": int(obs[run_offset + 12 + i]),
                        "x": int(map_option_coords[i * 2]),
                        "y": int(map_option_coords[i * 2 + 1]),
                        "encounter": ENCOUNTER_NAMES.get(
                            int(obs[run_offset + 16 + i]),
                            f"unknown-{int(obs[run_offset + 16 + i])}",
                        ),
                    }
                    for i in range(MAP_CHOICES)
                    if int(obs[run_offset + 12 + i]) != NODE_NONE
                )
                if phase == PHASE_MAP
                else ()
            ),
            "player_won": native.run_player_won(self._run_handle),
            "encounter_id": native.run_encounter_id(self._run_handle),
            "encounter": ENCOUNTER_NAMES.get(
                native.run_encounter_id(self._run_handle),
                "none",
            ),
        }
