"""Shared constants for the native full-run environment."""

from __future__ import annotations

from . import native

REWARD_SKIP_ACTION = 3
SHOP_REMOVE_ACTION = 13
SHOP_SKIP_ACTION = 14
EVENT_SKIP_ACTION = 3
# Read from the emulator rather than restated: the map's choice arrays widened to a
# whole row when Winged Boots' free travel was modelled, and a copy of the old 4 here
# would have silently hidden the three options past it.
MAP_CHOICES = native.RUN_OBS_LAYOUT["map_choices"]
RUN_OBS_SIZE = native.RUN_OBS_SIZE
RUN_MAX_EPISODE_STEPS = 1000

PHASE_COMBAT = 0
PHASE_CARD_REWARD = 1
PHASE_MAP = 2
PHASE_REST = 3
PHASE_SHOP = 4
PHASE_RELIC_REWARD = 5
PHASE_COMPLETE = 6
PHASE_EVENT = 7
PHASE_ANCIENT = 8
PHASE_TRANSFORM_SELECT = 9
PHASE_TREASURE = 10
PHASE_CRYSTAL_SPHERE = 11

NODE_NONE = 0
NODE_NORMAL = 1
NODE_ELITE = 2
NODE_REST = 3
NODE_SHOP = 4
NODE_RELIC = 5
NODE_BOSS = 6
NODE_EVENT = 7

ACT_OVERGROWTH = 1
ACT_UNDERDOCKS = 2
