namespace Sts2Emulator.Core.Rng;

public sealed class DotNetRandom
{
    private const int Mbig = int.MaxValue;
    private const int Mseed = 161803398;

    private readonly int[] _seedArray = new int[56];
    private int _inext;
    private int _inextp = 21;

    public int CallCount { get; private set; }

    public DotNetRandom(int seed)
    {
        Initialize(seed);
    }

    public int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (int)(Sample() * (1.0 / Mbig) * maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minValue),
                "minValue cannot be greater than maxValue."
            );
        }

        long range = (long)maxValue - minValue;
        if (range <= int.MaxValue)
        {
            return (int)(Sample() * (1.0 / Mbig) * range) + minValue;
        }

        return (int)((long)(NextDouble() * range) + minValue);
    }

    public bool NextBool() => Next(2) == 0;

    public double NextDouble() => Sample() * (1.0 / Mbig);

    private int Sample()
    {
        CallCount++;

        int inext = _inext + 1;
        if (inext >= 56)
        {
            inext = 1;
        }

        int inextp = _inextp + 1;
        if (inextp >= 56)
        {
            inextp = 1;
        }

        int retVal = _seedArray[inext] - _seedArray[inextp];
        if (retVal == Mbig)
        {
            retVal--;
        }

        if (retVal < 0)
        {
            retVal += Mbig;
        }

        _seedArray[inext] = retVal;
        _inext = inext;
        _inextp = inextp;
        return retVal;
    }

    private void Initialize(int seed)
    {
        int subtraction = seed == int.MinValue ? Mbig : Math.Abs(seed);
        int mj = Mseed - subtraction;
        _seedArray[55] = mj;

        int mk = 1;
        int ii = 0;
        for (int i = 1; i < 55; i++)
        {
            ii += 21;
            if (ii >= 55)
            {
                ii -= 55;
            }

            _seedArray[ii] = mk;
            mk = mj - mk;
            if (mk < 0)
            {
                mk += Mbig;
            }

            mj = _seedArray[ii];
        }

        for (int k = 1; k < 5; k++)
        {
            for (int i = 1; i < 56; i++)
            {
                int n = i + 30;
                if (n >= 55)
                {
                    n -= 55;
                }
                unchecked
                {
                    _seedArray[i] -= _seedArray[1 + n];
                }
                if (_seedArray[i] < 0)
                {
                    _seedArray[i] += Mbig;
                }
            }
        }
    }
}
