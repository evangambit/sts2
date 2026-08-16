using System.Numerics;

namespace Sts2Emulator.Core.Rng;

/// <summary>
/// Faithful port of the game's <c>MegaCrit.Sts2.Core.Random.MegaRandom</c>: a
/// Xoshiro256** generator seeded through Splitmix64.
///
/// This is the generator the game actually uses for every seeded decision. The
/// emulator previously used a port of .NET's legacy subtractive Random, which
/// produces a completely different stream and is why shuffles diverged from the
/// live game — see MegaRandomHypothesisTests. That class has been deleted so it
/// cannot be wired back in.
///
/// Keep this bit-faithful: the range mapping goes through NextDouble rather than
/// a modulo or rejection scheme, and reproducing the game means reproducing that
/// exactly, including its slight bias.
/// </summary>
public sealed class MegaRandom
{
    private const double IncrDouble = 1.1102230246251565E-16;

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public MegaRandom(ulong seed) => Reinitialise(seed);

    /// <summary>Splitmix64 PRNG, used to expand the seed into the four lanes.</summary>
    public static ulong Splitmix64(ref ulong x)
    {
        ulong num = (x += 11400714819323198485uL);
        num = (num ^ (num >> 30)) * 13787848793156543929uL;
        num = (num ^ (num >> 27)) * 10723151780598845931uL;
        return num ^ (num >> 31);
    }

    public void Reinitialise(ulong seed)
    {
        _s0 = Splitmix64(ref seed);
        _s1 = Splitmix64(ref seed);
        _s2 = Splitmix64(ref seed);
        _s3 = Splitmix64(ref seed);
    }

    private ulong NextULongInner()
    {
        ulong s = _s0;
        ulong s2 = _s1;
        ulong s3 = _s2;
        ulong s4 = _s3;
        ulong result = BitOperations.RotateLeft(s2 * 5, 7) * 9;
        ulong num = s2 << 17;
        s3 ^= s;
        s4 ^= s2;
        s2 ^= s3;
        s ^= s4;
        s3 ^= num;
        s4 = BitOperations.RotateLeft(s4, 45);
        _s0 = s;
        _s1 = s2;
        _s2 = s3;
        _s3 = s4;
        return result;
    }

    public ulong NextULong() => NextULongInner();

    public double NextDouble() => (NextULongInner() >> 11) * IncrDouble;

    /// <summary>Inclusive of int.MaxValue, matching the game.</summary>
    public int NextInt() => (int)(NextULongInner() >> 33);

    public uint NextUInt() => (uint)NextULongInner();

    public bool NextBool() => (NextULongInner() & 0x8000000000000000uL) != 0;

    private int NextInner(int maxValue) => (int)(NextDouble() * maxValue);

    private long NextInner(long maxValue) => (long)(NextDouble() * maxValue);

    public int Next(int maxValue)
    {
        if (maxValue < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxValue),
                maxValue,
                "maxValue must be > 0"
            );
        }

        return NextInner(maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxValue),
                maxValue,
                "maxValue must be > minValue"
            );
        }

        long range = (long)maxValue - minValue;
        return range <= int.MaxValue
            ? NextInner((int)range) + minValue
            : (int)(NextInner(range) + minValue);
    }
}
