namespace Sts2Emulator.Core;

using Rng;

/// <summary>
/// Wraps the game's deterministic RNG and counts calls so the caller can sync
/// run-level streams after combat-local RNG consumption.
/// </summary>
public sealed class CountingRandom : Random
{
    private readonly DotNetRandom _rng;

    public int CallCount => _rng.CallCount;

    public CountingRandom(int seed)
    {
        _rng = new DotNetRandom(seed);
    }

    public override int Next(int maxValue)
    {
        return _rng.Next(maxValue);
    }

    public override int Next()
    {
        return _rng.Next(int.MaxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        return _rng.Next(minValue, maxValue);
    }

    public override double NextDouble()
    {
        return _rng.NextDouble();
    }
}
