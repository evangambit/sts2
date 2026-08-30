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
PHASE_BUNDLE_SELECT = 12

# The rest site's own options. They are SPARSE rather than contiguous: 3 is the leave
# action every reward screen shares, so Girya's lift and Shovel's dig sit past it.
REST_HEAL_ACTION = 0
REST_UPGRADE_ACTION = 1
REST_CLONE_ACTION = 2
REST_LIFT_ACTION = 4
REST_DIG_ACTION = 5

# Commits the highlighted bundle on Scroll Boxes' choose-a-bundle screen. The game answers
# that screen in two steps -- `select_bundle` then `confirm_bundle_selection` -- and the
# emulator spends an action on each the same way.
BUNDLE_CONFIRM_ACTION = 2

NODE_NONE = 0
NODE_NORMAL = 1
NODE_ELITE = 2
NODE_REST = 3
NODE_SHOP = 4
NODE_RELIC = 5
NODE_BOSS = 6
NODE_EVENT = 7
# The act's own ancient, which is the node a run stands on before row 1 and again at the
# top of every act after the first. It is a real node in the map, not a placeholder.
NODE_ANCIENT = 8

ACT_OVERGROWTH = 1
ACT_UNDERDOCKS = 2
ACT_HIVE = 3
ACT_GLORY = 4

# The emulator's act ids are REGIONS, not ordinals: a run's first act is Overgrowth or
# Underdocks depending on the seed, and Hive and Glory follow in that order.
ACT_NAMES = {
    ACT_OVERGROWTH: "overgrowth",
    ACT_UNDERDOCKS: "underdocks",
    ACT_HIVE: "hive",
    ACT_GLORY: "glory",
}
