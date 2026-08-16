namespace Sts2Emulator.Core.Rng;

/// <summary>
/// Port of the game's <c>MegaCrit.Sts2.Core.Random.Rng</c>: a named, counted
/// stream over <see cref="MegaRandom" /> (Xoshiro256**).
///
/// Method-for-method faithful to the game, because every call both produces a
/// value and advances shared stream state — a wrapper that consumes a different
/// number of draws desynchronises everything downstream even when each
/// individual value looks reasonable.
/// </summary>
public sealed class GameRng
{
    private readonly MegaRandom _rng;

    public int RawSeed { get; }

    /// <summary>The game's <c>Rng.Counter</c>: how many values this stream has produced.</summary>
    public int CallCount { get; private set; }

    public GameRng(uint seed, string name = "")
    {
        uint rawSeed = string.IsNullOrEmpty(name)
            ? seed
            : unchecked(seed + (uint)DeterministicHash.GetDeterministicHashCode(name));
        RawSeed = unchecked((int)rawSeed);
        // The game constructs MegaRandom from the uint seed; go through uint so the
        // value is zero-extended rather than sign-extended.
        _rng = new MegaRandom(rawSeed);
    }

    public GameRng(int seed)
        : this(unchecked((uint)seed)) { }

    public int NextInt(int maxExclusive = int.MaxValue)
    {
        CallCount++;
        return _rng.Next(maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minInclusive),
                "Minimum must be lower than maximum."
            );
        }

        CallCount++;
        return _rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Matches the game's <c>Rng.NextBool</c>, which is <c>Next(2) == 0</c> — NOT
    /// MegaRandom's own sign-bit NextBool, which would consume the same draw but
    /// return the opposite answer roughly half the time.
    /// </summary>
    public bool NextBool()
    {
        CallCount++;
        return _rng.Next(2) == 0;
    }

    public double NextDouble()
    {
        CallCount++;
        return _rng.NextDouble();
    }

    public T NextItem<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot choose an item from an empty list.", nameof(items));
        }

        return items[NextInt(items.Count)];
    }

    /// <summary>
    /// Box–Muller, matching the game. Note the game uses plain <c>Math.Round</c>
    /// (banker's rounding, to-even) — not away-from-zero.
    /// </summary>
    public int NextGaussianInt(int mean, int stdDev, int min, int max)
    {
        while (true)
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double sample = Math.Sqrt(-2.0 * Math.Log(d)) * Math.Sin(Math.PI * 2.0 * num);
            int result = (int)Math.Round(mean + stdDev * sample);
            if (min <= result && result <= max)
            {
                return result;
            }
        }
    }

    /// <summary>The game's <c>ListExtensions.UnstableShuffle</c>.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public void StableShuffle<T>(List<T> items, IComparer<T> comparer)
    {
        items.Sort(comparer);
        Shuffle(items);
    }

    /// <summary>The game's <c>Rng.FastForwardCounter</c>.</summary>
    public void AdvanceToCallCount(int callCount)
    {
        if (callCount < CallCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callCount),
                "Cannot rewind an RNG stream."
            );
        }

        while (CallCount < callCount)
        {
            CallCount++;
            _rng.NextInt();
        }
    }
}
