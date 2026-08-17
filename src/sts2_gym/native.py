"""ctypes bindings to the NativeAOT-compiled Sts2Emulator native library."""

import ctypes
import os
import sys
from pathlib import Path

_LIB_NAMES = {
    "win32": "Sts2Emulator.dll",
    "linux": "Sts2Emulator.so",
    "darwin": "Sts2Emulator.dylib",
}
_ALLOW_STALE_ENV = "STS2_ALLOW_STALE_NATIVE"
_REQUIRED_NATIVE_API_VERSION = 14
_REQUIRED_RUN_NATIVE_API_VERSION = 9


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _native_source_paths(repo_root: Path) -> list[Path]:
    source_root = repo_root / "src" / "Sts2Emulator"
    paths = [
        path
        for path in source_root.rglob("*.cs")
        if "bin" not in path.parts and "obj" not in path.parts
    ]
    paths.extend(source_root.rglob("*.csproj"))
    return paths


def _assert_native_library_is_fresh(path: Path) -> None:
    if os.environ.get(_ALLOW_STALE_ENV) == "1":
        return

    source_paths = _native_source_paths(_repo_root())
    if not source_paths:
        return

    newest_source = max(source_paths, key=lambda source: source.stat().st_mtime)
    if path.stat().st_mtime >= newest_source.stat().st_mtime:
        return

    raise RuntimeError(
        f"{path} is older than {newest_source}. Rebuild the native library with "
        f"`bash scripts/build.sh win-x64` or `dotnet publish "
        f'"src\\Sts2Emulator\\Sts2Emulator.csproj" -c Release -r win-x64 '
        f'--self-contained -o "out"`. Set {_ALLOW_STALE_ENV}=1 only when '
        "intentionally testing an older native build.",
    )


def _assert_native_api_version(lib: ctypes.CDLL, path: Path) -> None:
    try:
        version_func = lib.Sts2_NativeApiVersion
    except AttributeError as exc:
        raise RuntimeError(
            f"{path} does not export Sts2_NativeApiVersion and is too old for "
            "these Python bindings. Rebuild the native library with "
            "`bash scripts/build.sh win-x64` or `dotnet publish "
            '"src\\Sts2Emulator\\Sts2Emulator.csproj" -c Release -r win-x64 '
            '--self-contained -o "out"`.',
        ) from exc

    version_func.restype = ctypes.c_int
    version_func.argtypes = []
    actual_version = int(version_func())
    if actual_version != _REQUIRED_NATIVE_API_VERSION:
        raise RuntimeError(
            f"{path} exports native API version {actual_version}, but "
            f"sts2_gym requires {_REQUIRED_NATIVE_API_VERSION}. Rebuild the "
            "native library with `bash scripts/build.sh win-x64` or "
            '`dotnet publish "src\\Sts2Emulator\\Sts2Emulator.csproj" '
            '-c Release -r win-x64 --self-contained -o "out"`.',
        )

    try:
        run_version_func = lib.Sts2Run_NativeApiVersion
    except AttributeError as exc:
        raise RuntimeError(
            f"{path} does not export Sts2Run_NativeApiVersion and is too old "
            "for these Python bindings. Rebuild the native library with "
            "`bash scripts/build.sh win-x64` or `dotnet publish "
            '"src\\Sts2Emulator\\Sts2Emulator.csproj" -c Release -r win-x64 '
            '--self-contained -o "out"`.',
        ) from exc

    run_version_func.restype = ctypes.c_int
    run_version_func.argtypes = []
    actual_run_version = int(run_version_func())
    if actual_run_version != _REQUIRED_RUN_NATIVE_API_VERSION:
        raise RuntimeError(
            f"{path} exports run native API version {actual_run_version}, but "
            f"sts2_gym requires {_REQUIRED_RUN_NATIVE_API_VERSION}. Rebuild "
            "the native library with `bash scripts/build.sh win-x64` or "
            '`dotnet publish "src\\Sts2Emulator\\Sts2Emulator.csproj" '
            '-c Release -r win-x64 --self-contained -o "out"`.',
        )


def _load_lib() -> ctypes.CDLL:
    name = _LIB_NAMES.get(sys.platform)
    if name is None:
        raise RuntimeError(f"Unsupported platform: {sys.platform}")

    search = []
    if "STS2_LIB_PATH" in os.environ:
        search.append(Path(os.environ["STS2_LIB_PATH"]) / name)
    search.append(_repo_root() / "out" / name)
    for path in search:
        if path.exists():
            _assert_native_library_is_fresh(path)
            lib = ctypes.CDLL(str(path))
            _assert_native_api_version(lib, path)
            return lib
    raise FileNotFoundError(
        f"Could not find {name}. Run scripts/build.sh first, or set STS2_LIB_PATH.",
    )


_lib = _load_lib()

# ── function signatures ───────────────────────────────────────────────────────

_lib.Sts2_ObsSize.restype = ctypes.c_int
_lib.Sts2_ObsSize.argtypes = []

_lib.Sts2_MaxEnemies.restype = ctypes.c_int
_lib.Sts2_MaxEnemies.argtypes = []

_lib.Sts2_Create.restype = ctypes.c_int
_lib.Sts2_Create.argtypes = [ctypes.c_int]

_lib.Sts2_Reset.restype = None
_lib.Sts2_Reset.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_int)]

_lib.Sts2_ResetEncounter.restype = None
_lib.Sts2_ResetEncounter.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetEncounterWeak.restype = None
_lib.Sts2_ResetEncounterWeak.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeck.restype = None
_lib.Sts2_ResetWithDeck.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeckAndEncounter.restype = None
_lib.Sts2_ResetWithDeckAndEncounter.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeckEncounterAndRelics.restype = None
_lib.Sts2_ResetWithDeckEncounterAndRelics.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_Step.restype = ctypes.c_int
_lib.Sts2_Step.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_float),
]

_lib.Sts2_StepTargeted.restype = ctypes.c_int
_lib.Sts2_StepTargeted.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_float),
]

_lib.Sts2_PlayerWon.restype = ctypes.c_int
_lib.Sts2_PlayerWon.argtypes = [ctypes.c_int]

_lib.Sts2_EncounterId.restype = ctypes.c_int
_lib.Sts2_EncounterId.argtypes = [ctypes.c_int]

_lib.Sts2_ActionCount.restype = ctypes.c_int
_lib.Sts2_ActionCount.argtypes = [ctypes.c_int]

_lib.Sts2_ValidActions.restype = None
_lib.Sts2_ValidActions.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2_GetPile.restype = ctypes.c_int
_lib.Sts2_GetPile.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2_Destroy.restype = None
_lib.Sts2_Destroy.argtypes = [ctypes.c_int]

_lib.Sts2Run_ObsSize.restype = ctypes.c_int
_lib.Sts2Run_ObsSize.argtypes = []

_lib.Sts2Run_MaxActions.restype = ctypes.c_int
_lib.Sts2Run_MaxActions.argtypes = []

_lib.Sts2Run_InfoSize.restype = ctypes.c_int
_lib.Sts2Run_InfoSize.argtypes = []

_lib.Sts2Run_Create.restype = ctypes.c_int
_lib.Sts2Run_Create.argtypes = []

_lib.Sts2Run_Reset.restype = ctypes.c_int
_lib.Sts2Run_Reset.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_ubyte),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2Run_Step.restype = ctypes.c_int
_lib.Sts2Run_Step.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_float),
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2Run_StartCombat.restype = ctypes.c_int
_lib.Sts2Run_StartCombat.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2Run_ActionMask.restype = ctypes.c_int
_lib.Sts2Run_ActionMask.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2Run_GetInfo.restype = ctypes.c_int
_lib.Sts2Run_GetInfo.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2Run_GetStateList.restype = ctypes.c_int
_lib.Sts2Run_GetStateList.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2Run_GetPhase.restype = ctypes.c_int
_lib.Sts2Run_GetPhase.argtypes = [ctypes.c_int]

_lib.Sts2Run_PlayerWon.restype = ctypes.c_int
_lib.Sts2Run_PlayerWon.argtypes = [ctypes.c_int]

_lib.Sts2Run_EncounterId.restype = ctypes.c_int
_lib.Sts2Run_EncounterId.argtypes = [ctypes.c_int]

_lib.Sts2Run_GetShuffleRngCallCount.restype = ctypes.c_int
_lib.Sts2Run_GetShuffleRngCallCount.argtypes = [ctypes.c_int]

_lib.Sts2Run_GetNicheRngCallCount.restype = ctypes.c_int
_lib.Sts2Run_GetNicheRngCallCount.argtypes = [ctypes.c_int]

_lib.Sts2Run_Destroy.restype = None
_lib.Sts2Run_Destroy.argtypes = [ctypes.c_int]

# ── public wrappers ───────────────────────────────────────────────────────────

OBS_SIZE: int = _lib.Sts2_ObsSize()
MAX_ENEMIES: int = _lib.Sts2_MaxEnemies()
RUN_OBS_SIZE: int = _lib.Sts2Run_ObsSize()
RUN_MAX_ACTIONS: int = _lib.Sts2Run_MaxActions()
RUN_INFO_SIZE: int = _lib.Sts2Run_InfoSize()


def create(seed: int) -> int:
    return _lib.Sts2_Create(seed)


def reset(handle: int, obs_buf: ctypes.Array) -> None:
    _lib.Sts2_Reset(handle, obs_buf)


def reset_encounter(
    handle: int,
    encounter_id: int,
    obs_buf: ctypes.Array,
    completed_combat_rooms: int = -1,
) -> None:
    # completed_combat_rooms in [0,3) selects weak encounter variants (fewer/weaker
    # enemies on early floors); -1 keeps the normal variant (unchanged default).
    if completed_combat_rooms == -1:
        _lib.Sts2_ResetEncounter(handle, encounter_id, obs_buf)
    else:
        _lib.Sts2_ResetEncounterWeak(
            handle, encounter_id, completed_combat_rooms, obs_buf
        )


def reset_with_deck(handle: int, deck_ids: list[int], obs_buf: ctypes.Array) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    _lib.Sts2_ResetWithDeck(handle, deck_buf, len(deck_ids), obs_buf)


def reset_with_deck_and_encounter(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    _lib.Sts2_ResetWithDeckAndEncounter(
        handle,
        deck_buf,
        len(deck_ids),
        encounter_id,
        obs_buf,
    )


def reset_with_deck_encounter_and_relics(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    relic_ids: list[int],
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    relic_buf = (ctypes.c_int * len(relic_ids))(*relic_ids)
    _lib.Sts2_ResetWithDeckEncounterAndRelics(
        handle,
        deck_buf,
        len(deck_ids),
        encounter_id,
        relic_buf,
        len(relic_ids),
        obs_buf,
    )


def step(
    handle: int,
    action: int,
    obs_buf: ctypes.Array,
    reward_buf: ctypes.Array,
    target_enemy_index: int = -1,
) -> bool:
    if target_enemy_index >= 0:
        terminal = _lib.Sts2_StepTargeted(
            handle,
            action,
            target_enemy_index,
            obs_buf,
            reward_buf,
        )
    else:
        terminal = _lib.Sts2_Step(handle, action, obs_buf, reward_buf)
    return bool(terminal)


def valid_actions(handle: int, max_actions: int) -> ctypes.Array:
    buf = (ctypes.c_int * max_actions)()
    _lib.Sts2_ValidActions(handle, buf, max_actions)
    return buf


PILE_DRAW = 0
PILE_HAND = 1
PILE_DISCARD = 2
PILE_EXHAUST = 3

_PILE_NAMES = {
    "draw": PILE_DRAW,
    "hand": PILE_HAND,
    "discard": PILE_DISCARD,
    "exhaust": PILE_EXHAUST,
}


def get_pile(handle: int, pile: int | str = PILE_DRAW) -> list[tuple[int, bool]]:
    """Dump a combat pile in true order — index 0 is the top (next card drawn).

    Returns (card_def_id, upgraded) per card. The observation vector only carries
    pile counts, so this is the way to compare exact card sequences against the
    live game.
    """
    pile_id = _PILE_NAMES[pile] if isinstance(pile, str) else pile
    if pile_id not in _PILE_NAMES.values():
        raise ValueError(f"unknown pile {pile!r}; expected one of {sorted(_PILE_NAMES)}")

    capacity = 64
    while True:
        buf = (ctypes.c_int * (capacity * 2))()
        count = _lib.Sts2_GetPile(handle, pile_id, buf, capacity)
        if count < 0:
            raise ValueError(f"native rejected pile id {pile_id}")
        if count <= capacity:
            return [(buf[i * 2], bool(buf[i * 2 + 1])) for i in range(count)]
        capacity = count


def player_won(handle: int) -> bool:
    return bool(_lib.Sts2_PlayerWon(handle))


def encounter_id(handle: int) -> int:
    return int(_lib.Sts2_EncounterId(handle))


def destroy(handle: int) -> None:
    _lib.Sts2_Destroy(handle)


def run_create() -> int:
    return int(_lib.Sts2Run_Create())


def run_reset(handle: int, seed: str, obs_buf: ctypes.Array) -> int:
    seed_bytes = seed.encode("utf-8")
    seed_buf = (ctypes.c_ubyte * len(seed_bytes))(*seed_bytes)
    return int(_lib.Sts2Run_Reset(handle, seed_buf, len(seed_bytes), obs_buf))


def run_step(
    handle: int,
    action: int,
    target_enemy_index: int,
    obs_buf: ctypes.Array,
    reward_buf: ctypes.Array,
    terminal_buf: ctypes.Array,
    truncated_buf: ctypes.Array,
) -> int:
    return int(
        _lib.Sts2Run_Step(
            handle,
            action,
            target_enemy_index,
            obs_buf,
            reward_buf,
            terminal_buf,
            truncated_buf,
        ),
    )


def run_start_combat(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    relic_ids: list[int],
    player_hp: int,
    player_max_hp: int,
    potion_ids: list[int],
    player_gold: int,
    completed_combat_rooms_before_current: int,
    obs_buf: ctypes.Array,
) -> int:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    relic_buf = (ctypes.c_int * len(relic_ids))(*relic_ids)
    potion_buf = (ctypes.c_int * len(potion_ids))(*potion_ids)
    return int(
        _lib.Sts2Run_StartCombat(
            handle,
            deck_buf,
            len(deck_ids),
            encounter_id,
            relic_buf,
            len(relic_ids),
            player_hp,
            player_max_hp,
            potion_buf,
            len(potion_ids),
            player_gold,
            completed_combat_rooms_before_current,
            obs_buf,
        ),
    )


def run_action_mask(handle: int, max_actions: int) -> ctypes.Array:
    buf = (ctypes.c_int * max_actions)()
    status = int(_lib.Sts2Run_ActionMask(handle, buf, max_actions))
    if status != 0:
        raise RuntimeError(f"Sts2Run_ActionMask failed with status {status}.")
    return buf


def run_info(handle: int) -> ctypes.Array:
    buf = (ctypes.c_int * RUN_INFO_SIZE)()
    status = int(_lib.Sts2Run_GetInfo(handle, buf, RUN_INFO_SIZE))
    if status != 0:
        raise RuntimeError(f"Sts2Run_GetInfo failed with status {status}.")
    return buf


def run_state_list(handle: int, list_id: int, capacity: int = 256) -> tuple[int, ...]:
    buf = (ctypes.c_int * capacity)()
    count = int(_lib.Sts2Run_GetStateList(handle, list_id, buf, capacity))
    if count < 0:
        raise RuntimeError(
            f"Sts2Run_GetStateList failed with status {count} for list {list_id}.",
        )
    return tuple(int(buf[i]) for i in range(min(count, capacity)))


def run_phase(handle: int) -> int:
    return int(_lib.Sts2Run_GetPhase(handle))


def run_player_won(handle: int) -> bool:
    return bool(_lib.Sts2Run_PlayerWon(handle))


def run_encounter_id(handle: int) -> int:
    return int(_lib.Sts2Run_EncounterId(handle))


def run_get_shuffle_rng_call_count(handle: int) -> int:
    return int(_lib.Sts2Run_GetShuffleRngCallCount(handle))


def run_get_niche_rng_call_count(handle: int) -> int:
    return int(_lib.Sts2Run_GetNicheRngCallCount(handle))


def run_destroy(handle: int) -> None:
    _lib.Sts2Run_Destroy(handle)
