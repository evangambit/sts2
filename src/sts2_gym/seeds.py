"""Derive the game's uint *gen seed* from a run's string seed.

The game takes a string seed (e.g. "ABCDEF") and hashes it into the uint that
actually seeds every named RNG stream. Passing that uint to ``Sts2CombatEnv``
makes the emulator's streams line up with the live run.

Port of ``Sts2Emulator.Core.Rng.DeterministicHash`` / ``RunRngSet``; pinned by
tests/python/test_seeds.py against the known values.
"""

from __future__ import annotations

_MASK32 = 0xFFFFFFFF


def _to_int32(value: int) -> int:
    value &= _MASK32
    return value - (1 << 32) if value >= (1 << 31) else value


def deterministic_hash(value: str) -> int:
    """C#'s ``GetDeterministicHashCode`` — signed 32-bit, wrapping arithmetic."""
    hash1 = 352654597
    hash2 = 352654597

    for i in range(0, len(value), 2):
        hash1 = _to_int32(((hash1 << 5) + hash1) ^ ord(value[i]))
        if i == len(value) - 1:
            break
        hash2 = _to_int32(((hash2 << 5) + hash2) ^ ord(value[i + 1]))

    return _to_int32(hash1 + _to_int32(hash2 * 1566083941))


def canonicalize(string_seed: str) -> str:
    """Fold a typed seed the way the game does before hashing it.

    Port of ``SeedHelper.CanonicalizeSeed``. The game's seed alphabet has no ``I`` and
    no ``O``, so both fold into digits; every chosen seed goes through this in
    ``StartRunLobby.BeginRunLocally``. Hashing the raw string instead makes any seed
    with lowercase, ``I``, ``O`` or stray whitespace derive the wrong gen seed.
    """
    return string_seed.upper().replace("O", "0").replace("I", "1").strip()


def game_seed(string_seed: str) -> int:
    """Convert a run's string seed to its uint gen seed, e.g. "ABCDEF" -> 3334281563."""
    return deterministic_hash(canonicalize(string_seed)) & _MASK32
