"""Gymnasium environment wrapping the Sts2Emulator native library."""

import ctypes

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native

MAX_ACTIONS = 32  # hand(10) + end_turn(1) + potions(3) + buffer
MAX_EPISODE_STEPS = 50
ENCOUNTER_NAMES = {
    0: "cultists",
    1: "chompers",
    2: "nibbits-weak",
    3: "slimes-weak",
    4: "exoskeletons-weak",
    5: "inklets",
    6: "two-tailed-rats",
    7: "gremlin-merc",
    8: "fuzzy-wurm-crawler",
    9: "corpse-slugs",
    10: "sludge-spinner",
    11: "shrinker-beetle",
    12: "seapunk",
    13: "toadpoles",
    14: "mawler",
    15: "nibbits-normal",
    16: "slimes-normal",
    17: "flyconid-normal",
    18: "snapping-jaxfruit-normal",
    19: "cubex-construct",
    20: "vine-shambler",
    21: "overgrowth-crawlers",
    22: "cultist-and-seapunk",
    23: "fossil-stalker",
    24: "punch-construct",
    25: "sewer-clam",
    26: "haunted-ship",
    27: "slithering-strangler",
    28: "ruby-raiders",
    29: "fogmog",
    30: "living-fog",
    31: "bowlbugs-weak",
    32: "bowlbugs",
    33: "tunneler",
    34: "tunneler-and-chomper",
    35: "thieving-hopper",
    36: "mytes",
    37: "slumbering-beetle",
    38: "spiny-toad",
    39: "ovicopter",
    40: "louse-progenitor",
    41: "hunter-killer",
    42: "axebot",
    43: "devoted-sculptor",
    44: "fabricator",
    45: "frog-knight",
    46: "globe-head",
    47: "turret-operator",
    48: "owl-magistrate",
    49: "scrolls-weak",
    50: "scrolls",
    51: "slimed-berserker",
    52: "lost-and-forgotten",
    53: "obscura",
    54: "construct-menagerie",
    55: "dense-vegetation",
    56: "punch-off",
    57: "fake-merchant",
    58: "mysterious-knight",
    59: "battleworn-dummy-1",
    60: "battleworn-dummy-2",
    61: "battleworn-dummy-3",
    62: "bygone-effigy",
    63: "entomancer",
    64: "infested-prisms",
    65: "phrog-parasite",
    66: "soul-nexus",
    67: "terror-eel",
    68: "byrdonis",
    69: "decimillipede",
    70: "knights",
    71: "mecha-knight",
    72: "phantasmal-gardeners",
    73: "aeonglass",
    74: "ceremonial-beast",
    75: "kaiser-crab",
    76: "knowledge-demon",
    77: "lagavulin-matriarch",
    78: "queen",
    79: "soul-fysh",
    80: "test-subject",
    81: "insatiable",
    87: "exoskeletons-normal",
    82: "kin",
    83: "vantom",
    84: "waterfall-giant",
    85: "architect",
    86: "skulking-colony",
}
ENCOUNTER_IDS = {name: encounter_id for encounter_id, name in ENCOUNTER_NAMES.items()}

# Names corrected against the game's own act pool (decompiled
# MegaCrit.Sts2.Core.Models.Acts/Overgrowth.cs) — the emulator had invented labels
# for four encounters. The old strings still resolve so existing traces and
# scripts keep working.
ENCOUNTER_IDS.update(
    {
        "large-slimes": ENCOUNTER_IDS["slimes-normal"],
        "slime-and-flyconid": ENCOUNTER_IDS["flyconid-normal"],
        "jaxfruit-and-flyconid": ENCOUNTER_IDS["snapping-jaxfruit-normal"],
        "shrinker-and-fuzzy": ENCOUNTER_IDS["overgrowth-crawlers"],
        "nibbit": ENCOUNTER_IDS["nibbits-weak"],
        "nibbits": ENCOUNTER_IDS["nibbits-normal"],
        "slimes": ENCOUNTER_IDS["slimes-weak"],
        # Corpse Slugs is ONE encounter with two rosters — 2 slugs weak, 3 normal — and
        # which one it builds comes from completed_combat_rooms, not from the id. The
        # alias exists so a sweep can name the normal variant; the harness's
        # emulator_completed_combat_rooms is what actually selects it.
        "corpse-slugs-normal": ENCOUNTER_IDS["corpse-slugs"],
    },
)


class Sts2CombatEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int = 0,
        max_episode_steps: int = MAX_EPISODE_STEPS,
        encounter: int | str | None = None,
        completed_combat_rooms: int = -1,
        total_floor: int | None = None,
        ascension: int = 8,
        # Card ids appended to the starter deck, for a capture that has to reach a state
        # the starter deck cannot: a Phrog Parasite's Wrigglers only spawn when it dies.
        # The live side adds the same cards with debug_add_card, and both sides append in
        # this order before shuffling, so the decks stay identical.
        extra_cards: list[int] | None = None,
    ):
        super().__init__()
        self._extra_cards = list(extra_cards or [])
        self._seed = seed
        self._max_episode_steps = max_episode_steps
        self._forced_encounter = self._normalize_encounter(encounter)
        # completed_combat_rooms in [0,3) selects weak encounter variants (early-floor
        # combats, e.g. CorpseSlugsWeak); -1 keeps the normal variant.
        self._completed_combat_rooms = completed_combat_rooms
        # The run's TotalFloor. Seeds the per-encounter RNG that rolls Slimes rosters
        # and Corpse Slug starting moves; None leaves those on the combat rng, which
        # does not reproduce the live game. See Core/Run/EncounterRng.cs.
        self._total_floor = total_floor
        # The run's ascension level. Enemy damage and buff amounts are read from it, so
        # a capture at A10 is only comparable to an emulator told it is at A10.
        self._ascension = ascension
        self._elapsed_steps = 0
        self._handle: int | None = None
        self._obs_buf = (ctypes.c_int * native.OBS_SIZE)()
        self._rew_buf = (ctypes.c_float * 1)()

        self.observation_space = spaces.Box(
            low=0,
            high=2**15,
            shape=(native.OBS_SIZE,),
            dtype=np.int32,
        )
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    # ── gymnasium API ─────────────────────────────────────────────────────────

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        if self._handle is not None:
            native.destroy(self._handle)
        self._handle = native.create(seed if seed is not None else self._seed)
        self._elapsed_steps = 0
        encounter = options.get("encounter") if options is not None else None
        encounter_id = self._normalize_encounter(encounter)
        if encounter_id is None:
            encounter_id = self._forced_encounter
        completed = (
            options.get("completed_combat_rooms")
            if options is not None and "completed_combat_rooms" in options
            else self._completed_combat_rooms
        )
        if encounter_id is None:
            native.reset(self._handle, self._obs_buf)
        else:
            total_floor = (
                options.get("total_floor")
                if options is not None and "total_floor" in options
                else self._total_floor
            )
            ascension = (
                options.get("ascension")
                if options is not None and "ascension" in options
                else self._ascension
            )
            extra = (
                options.get("extra_cards")
                if options is not None and "extra_cards" in options
                else self._extra_cards
            )
            if extra:
                native.reset_encounter_with_extra_cards(
                    self._handle,
                    list(extra),
                    encounter_id,
                    self._obs_buf,
                    completed,
                    total_floor or 0,
                    ascension,
                )
            else:
                native.reset_encounter(
                    self._handle,
                    encounter_id,
                    self._obs_buf,
                    completed,
                    total_floor,
                    ascension,
                )
        return self._obs(), self._info()

    def step(self, action: int):
        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(self._handle, action, self._obs_buf, self._rew_buf)
        self._elapsed_steps += 1
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps
        reward = float(self._rew_buf[0])
        return self._obs(), reward, terminal, truncated, self._info()

    def debug_add_card_to_hand(
        self, card_id: int, upgraded: bool = False,
    ) -> np.ndarray:
        """Put a card on top of the hand, as the mod's debug_add_card does live.

        Only for differential captures: it is how a fight reaches a state the starter
        deck cannot, such as a Phrog Parasite dead early enough for its Wrigglers to
        spawn while the capture is still running.
        """
        assert self._handle is not None, "Call reset() before debug_add_card_to_hand()"
        native.debug_add_card_to_hand(self._handle, card_id, self._obs_buf, upgraded)
        return self._obs()

    def debug_gain_max_hp(self, amount: int) -> np.ndarray:
        """Raise max HP and heal by it, as the mod's debug_gain_max_hp does live.

        Only for differential captures. A boss capture is worth what it survives: the
        Kaiser Crab kills a starter deck two moves short of either half's table, and a
        capture that never reaches a move cannot put that move under test.
        """
        assert self._handle is not None, "Call reset() before debug_gain_max_hp()"
        native.debug_gain_max_hp(self._handle, amount, self._obs_buf)
        return self._obs()

    def pending_selection_kind(self) -> int:
        """Report the card selection this combat is waiting on, or 0 for none."""
        assert self._handle is not None, "Call reset() before pending_selection_kind()"
        return native.pending_selection_kind(self._handle)

    def action_masks(self) -> np.ndarray:
        """Return a boolean mask of valid actions (for MaskablePPO)."""
        assert self._handle is not None, "Call reset() before action_masks()"
        mask_buf = native.valid_actions(self._handle, MAX_ACTIONS)
        return np.array(mask_buf, dtype=bool)

    def get_pile(self, pile: str = "draw") -> list[tuple[int, bool]]:
        """Dump a pile in true order — index 0 is the top (next card drawn).

        Returns (card_def_id, upgraded) per card. Introspection for differential
        testing; the observation vector only carries pile counts.
        """
        assert self._handle is not None, "Call reset() before get_pile()"
        return native.get_pile(self._handle, pile)

    def close(self):
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None

    # ── internals ─────────────────────────────────────────────────────────────

    def _obs(self) -> np.ndarray:
        return np.ctypeslib.as_array(self._obs_buf).copy()

    def _info(self) -> dict:
        if self._handle is None:
            return {"player_won": False, "encounter_id": -1, "encounter": "none"}

        encounter_id = native.encounter_id(self._handle)
        return {
            "player_won": native.player_won(self._handle),
            "encounter_id": encounter_id,
            "encounter": ENCOUNTER_NAMES.get(encounter_id, f"unknown-{encounter_id}"),
        }

    @staticmethod
    def _normalize_encounter(encounter: int | str | None) -> int | None:
        if encounter is None:
            return None
        if isinstance(encounter, int):
            if encounter not in ENCOUNTER_NAMES:
                raise ValueError(f"Unknown encounter id: {encounter}")
            return encounter
        try:
            return ENCOUNTER_IDS[encounter]
        except KeyError as exc:
            valid = ", ".join(sorted(ENCOUNTER_IDS))
            raise ValueError(
                f"Unknown encounter '{encounter}'. Valid encounters: {valid}",
            ) from exc
