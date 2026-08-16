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


def game_seed(string_seed: str) -> int:
    """The uint gen seed for a run's string seed, e.g. "ABCDEF" -> 3334281563."""
    return deterministic_hash(string_seed) & _MASK32
