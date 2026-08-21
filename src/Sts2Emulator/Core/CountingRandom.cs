namespace Sts2Emulator.Core;

using Rng;

/// <summary>
/// Wraps the game's deterministic RNG and counts calls so the caller can sync
/// run-level streams after combat-local RNG consumption.
///
/// Backed by <see cref="MegaRandom" /> (Xoshiro256**), the generator the game
/// actually uses. It is a <see cref="Random" /> subclass purely so existing
/// helpers like CardEffects.ShufflePile can take it; the values come from
/// MegaRandom, never from the base class.
/// </summary>
public sealed class CountingRandom : Random
{
    private readonly MegaRandom _rng;

    /// <summary>The stream's raw seed, kept so the stream can be cloned.</summary>
    public int Seed { get; }

    public int CallCount { get; private set; }

    /// <summary>
    /// Seed is the stream's raw seed. It goes through uint so it is zero-extended
    /// into MegaRandom's ulong, matching the game's <c>new MegaRandom(uintSeed)</c>.
    /// </summary>
    public CountingRandom(int seed)
    {
        Seed = seed;
        _rng = new MegaRandom(unchecked((uint)seed));
    }

    public override int Next(int maxValue)
    {
        CallCount++;
        return _rng.Next(maxValue);
    }

    public override int Next()
    {
        CallCount++;
        return _rng.Next(int.MaxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        CallCount++;
        return _rng.Next(minValue, maxValue);
    }

    public override double NextDouble()
    {
        CallCount++;
        return _rng.NextDouble();
    }
}
