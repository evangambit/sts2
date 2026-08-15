namespace Sts2Emulator.Core.Rng;

public sealed class GameRng
{
    private readonly DotNetRandom _rng;

    public int RawSeed { get; }
    public int CallCount => _rng.CallCount;

    public GameRng(uint seed, string name = "")
    {
        uint rawSeed = string.IsNullOrEmpty(name)
            ? seed
            : unchecked(seed + (uint)DeterministicHash.GetDeterministicHashCode(name));
        RawSeed = unchecked((int)rawSeed);
        _rng = new DotNetRandom(RawSeed);
    }

    public GameRng(int seed)
        : this(unchecked((uint)seed)) { }

    public int NextInt(int maxExclusive) => _rng.Next(maxExclusive);

    public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    public bool NextBool() => _rng.NextBool();

    public double NextDouble() => _rng.NextDouble();

    public T NextItem<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot choose an item from an empty list.", nameof(items));
        }

        return items[NextInt(items.Count)];
    }

    public int NextGaussianInt(int mean, int stdDev, int min, int max)
    {
        while (true)
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double sample = Math.Sqrt(-2.0 * Math.Log(d)) * Math.Sin(Math.PI * 2.0 * num);
            int result = (int)Math.Round(mean + stdDev * sample, MidpointRounding.AwayFromZero);
            if (min <= result && result <= max)
            {
                return result;
            }
        }
    }

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
            NextDouble();
        }
    }
}
