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
SHOP_REMOVE_ACTION = constants.SHOP_REMOVE_ACTION
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
PHASE_CRYSTAL_SPHERE = constants.PHASE_CRYSTAL_SPHERE
PHASE_BUNDLE_SELECT = constants.PHASE_BUNDLE_SELECT

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

    def map_graph(self) -> dict:
        """Read the whole act map: every node, every edge, and where the run stands.

        Deliberately NOT part of ``_info()``. The map is six hundred integers that change
        once an act, and ``_info()`` is rebuilt on every step of every training run --
        paying for the map on each of them to serve the two screens that want it is the
        wrong trade. Nothing about it is hidden, though: a map is fully visible in-game
        from the moment the act starts, which is why this exposes structure and node
        TYPES and not the encounter behind any node.

        Returns:
            ``{"nodes": {(col, row): node_type}, "edges": ((from, to), ...),
            "current": (col, row)}``.

        Raises:
            RuntimeError: If the environment has not been reset yet.

        """
        if self._run_handle is None:
            raise RuntimeError("Call reset() before map_graph().")

        flat_nodes = native.run_state_list(self._run_handle, 15, 3 * 1024)
        nodes = {
            (flat_nodes[i], flat_nodes[i + 1]): flat_nodes[i + 2]
            for i in range(0, len(flat_nodes) - 2, 3)
        }
        flat_edges = native.run_state_list(self._run_handle, 16, 4 * 2048)
        edges = tuple(
            ((flat_edges[i], flat_edges[i + 1]), (flat_edges[i + 2], flat_edges[i + 3]))
            for i in range(0, len(flat_edges) - 3, 4)
        )
        current = native.run_state_list(self._run_handle, 21, 2)
        # The act's boss, which the game names on its map from the moment the act opens --
        # unlike the encounter behind a monster node, which it never names. Read here
        # rather than put in the observation: this is what a SCREEN needs, and widening
        # what a policy is handed is a separate decision from drawing a map.
        act_summary = native.run_state_list(self._run_handle, 14, 3)
        return {
            "nodes": nodes,
            "edges": edges,
            "boss_encounter": ENCOUNTER_NAMES.get(
                act_summary[1] if len(act_summary) > 1 else -1,
                "unknown",
            ),
            "current": (current[0], current[1]) if len(current) == 2 else None,
        }

    def action_masks(self) -> np.ndarray:
        if self._run_handle is None:
            return np.zeros(native.RUN_MAX_ACTIONS, dtype=bool)
        mask_buf = native.run_action_mask(self._run_handle, native.RUN_MAX_ACTIONS)
        return np.ctypeslib.as_array(mask_buf).astype(bool)

    def debug_gain_max_hp(self, amount: int) -> tuple[np.ndarray, dict]:
        """Mirror the mod's debug_gain_max_hp: raise the maximum AND heal by it.

        Only for replaying a BUFFED live capture. The auto-player has never finished act
        1 -- the two deepest runs both died to the boss on floor 17 -- so the boss reward
        and the act transition are covered by nothing. Buffing both sides identically
        buys that coverage; the rules under test are unchanged, because the game is still
        the reference for every step.
        """
        assert self._run_handle is not None, "Call reset() before debug_gain_max_hp()"
        native.run_debug_gain_max_hp(self._run_handle, amount, self._run_obs_buf)
        return self._obs(), self._info()

    def debug_enter_next_act(self) -> tuple[np.ndarray, dict]:
        """Enter the next act, as the mod's debug_enter_next_act does.

        The point is testability: reaching act 2 honestly costs a buffed run that wins a
        boss fight. This is the same transition, without the run.
        """
        assert self._run_handle is not None, "Call reset() before debug_enter_next_act()"
        native.run_debug_enter_next_act(self._run_handle, self._run_obs_buf)
        return self._obs(), self._info()

    def debug_upgrade_deck(self) -> tuple[np.ndarray, dict]:
        """Mirror the mod's debug_upgrade_deck. See debug_gain_max_hp."""
        assert self._run_handle is not None, "Call reset() before debug_upgrade_deck()"
        native.run_debug_upgrade_deck(self._run_handle, self._run_obs_buf)
        return self._obs(), self._info()

    def close(self):
        if self._run_handle is not None:
            native.run_destroy(self._run_handle)
            self._run_handle = None

    def _invalid_action(self):
        return self._obs(), -1.0, False, False, self._info()

    def _obs(self) -> np.ndarray:
        return np.ctypeslib.as_array(self._run_obs_buf).copy()

    @staticmethod
    def _deck(obs: np.ndarray) -> tuple[dict, ...]:
        """Return the deck as the observation carries it, card by card.

        Slot ``i`` is deck index ``i``, which is also the action that selects that card at
        a card-select screen -- so this is readable straight against an action mask.
        """
        layout = native.RUN_OBS_LAYOUT
        base = native.OBS_SIZE + layout["deck_offset"]
        width = layout["deck_slot_size"]
        cards = []
        for i in range(layout["max_deck"]):
            at = base + i * width
            card_id = int(obs[at])
            if card_id == 0:
                break
            cards.append(
                {
                    "card_id": card_id,
                    "upgraded": bool(obs[at + 1]),
                    "enchantment": int(obs[at + 2]),
                    "enchant_amount": int(obs[at + 3]),
                },
            )
        return tuple(cards)

    @staticmethod
    def _shop(obs: np.ndarray) -> tuple[dict, ...]:
        """Return the merchant's board, slot by slot and priced.

        Slot ``i`` is shop action ``i``, so this reads straight against an action mask.
        Slot 13 is the card-removal service, which has a price and no item.
        """
        layout = native.RUN_OBS_LAYOUT
        base = native.OBS_SIZE + layout["shop_offset"]
        width = layout["shop_slot_size"]
        return tuple(
            {
                "action": i,
                "item_id": int(obs[base + i * width]),
                "cost": int(obs[base + i * width + 1]),
            }
            for i in range(layout["shop_slots"])
        )

    @staticmethod
    def _relics(obs: np.ndarray) -> tuple[dict, ...]:
        """Return the relics as the observation carries them, with their counters."""
        layout = native.RUN_OBS_LAYOUT
        base = native.OBS_SIZE + layout["relic_offset"]
        width = layout["relic_slot_size"]
        relics = []
        for i in range(layout["max_relics"]):
            at = base + i * width
            relic_id = int(obs[at])
            if relic_id == 0:
                break
            relics.append(
                {
                    "relic_id": relic_id,
                    "counter": int(obs[at + 1]),
                    "used_up": bool(obs[at + 2]),
                },
            )
        return tuple(relics)

    def _info(self) -> dict:
        if self._run_handle is None:
            raise RuntimeError("Call reset() before _info().")

        info_buf = native.run_info(self._run_handle)
        obs = np.ctypeslib.as_array(self._run_obs_buf)
        run_offset = native.OBS_SIZE
        phase = int(info_buf[0])
        # A two-way guess here reported every act after the first as "underdocks",
        # which would have quietly mislabelled act 2 in every trace it appears in.
        act = constants.ACT_NAMES.get(int(info_buf[2]), "unknown")
        map_option_coords = native.run_state_list(self._run_handle, 7, MAP_CHOICES * 2)
        layout = native.RUN_OBS_LAYOUT
        node_types = run_offset + layout["map_node_type_offset"]
        return {
            "phase": phase,
            "floor": int(info_buf[1]),
            "act": act,
            "deck_size": int(info_buf[3]),
            "deck": self._deck(obs),
            "relic_slots": self._relics(obs),
            "shop_slots": self._shop(obs),
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
            # The cards on an open choose-a-card grid, empty when the card-select phase is
            # a selection over the deck instead. The two resolve differently -- a grid on
            # the click, a deck selection on a confirm after it -- so a replay has to know
            # which screen it is looking at.
            "offer_cards": native.run_state_list(self._run_handle, 17, 16),
            # Scroll Boxes' two bundles, flat: three cards each.
            "bundle_offer": native.run_state_list(self._run_handle, 18, 6),
            "potion_reward_odds": 0.4,
            "event_id": int(info_buf[9]),
            # What the game's map shows: an icon and a position. NOT the encounter behind
            # the node -- that used to be here, read out of the observation, and neither
            # the observation nor a player has it. You learn what is in a room by walking
            # into it.
            "map_choices": (
                tuple(
                    {
                        "node_type": int(obs[node_types + i]),
                        "x": int(map_option_coords[i * 2]),
                        "y": int(map_option_coords[i * 2 + 1]),
                    }
                    for i in range(MAP_CHOICES)
                    if int(obs[node_types + i]) != NODE_NONE
                )
                if phase == PHASE_MAP
                else ()
            ),
            # What an open card-select screen is FOR: (kind, its argument, whether it
            # is the rest site's upgrade). One screen answers four different questions --
            # remove, upgrade, transform, duplicate -- and which one is not recoverable
            # from the cards it lists.
            "deck_selection": native.run_state_list(self._run_handle, 20, 3),
            # Which enemies are on the field, in the same order as the observation's
            # enemy slots -- the dead kept in place, as the engine keeps them. The
            # observation says how much hp an enemy has and what it means to do, and
            # never says what it is.
            "enemy_def_ids": native.run_state_list(
                self._run_handle,
                19,
                native.MAX_ENEMIES,
            ),
            "player_won": native.run_player_won(self._run_handle),
            "encounter_id": native.run_encounter_id(self._run_handle),
            "encounter": ENCOUNTER_NAMES.get(
                native.run_encounter_id(self._run_handle),
                "none",
            ),
        }
