using System.Numerics;
using Sts2Emulator.Core.Rng;
using Xunit;
using Xunit.Abstractions;

namespace Sts2Emulator.Tests;

/// <summary>
/// Investigation: the emulator's RNG is a port of .NET's legacy subtractive
/// Random (Core/Rng/DotNetRandom.cs), but the game uses Xoshiro256** seeded via
/// Splitmix64 (MegaCrit.Sts2.Core.Random/MegaRandom.cs). Same seed derivation,
/// completely different generator. This checks that against a real captured
/// shuffle from the live "ABCDEF" run.
/// </summary>
public class MegaRandomHypothesisTests
{
    private readonly ITestOutputHelper _output;

    public MegaRandomHypothesisTests(ITestOutputHelper output) => _output = output;

    private const uint AbcdefGenSeed = 3334281563u;

    /// <summary>Port of the game's MegaRandom (Xoshiro256**).</summary>
    private sealed class MegaRandom
    {
        private ulong _s0,
            _s1,
            _s2,
            _s3;

        public MegaRandom(ulong seed)
        {
            _s0 = Splitmix64(ref seed);
            _s1 = Splitmix64(ref seed);
            _s2 = Splitmix64(ref seed);
            _s3 = Splitmix64(ref seed);
        }

        private static ulong Splitmix64(ref ulong x)
        {
            ulong num = (x += 11400714819323198485uL);
            num = (num ^ (num >> 30)) * 13787848793156543929uL;
            num = (num ^ (num >> 27)) * 10723151780598845931uL;
            return num ^ (num >> 31);
        }

        private ulong NextULongInner()
        {
            ulong s = _s0,
                s2 = _s1,
                s3 = _s2,
                s4 = _s3;
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

        public double NextDouble() => (NextULongInner() >> 11) * 1.1102230246251565E-16;

        public int Next(int maxValue) => (int)(NextDouble() * maxValue);
    }

    // Pre-shuffle deck order, confirmed against the live save's "deck" array.
    private static int[] StarterDeck() => [472, 472, 472, 472, 472, 131, 131, 131, 131, 30, 10001];

    // Live capture: hand (5) then draw pile (6), top-first.
    private static readonly int[] LiveOrder =
    [
        131,
        131,
        472,
        30,
        472,
        472,
        131,
        472,
        131,
        10001,
        472,
    ];

    /// <summary>The game's ListExtensions.UnstableShuffle.</summary>
    private static void UnstableShuffle(IList<int> list, Func<int, int> nextInt)
    {
        int num = list.Count;
        while (num > 1)
        {
            num--;
            int num2 = nextInt(num + 1);
            (list[num2], list[num]) = (list[num], list[num2]);
        }
    }

    [Fact]
    public void MegaRandomReproducesTheLiveShuffle()
    {
        uint rawSeed = unchecked(
            AbcdefGenSeed + (uint)DeterministicHash.GetDeterministicHashCode("shuffle")
        );
        var rng = new MegaRandom(rawSeed);

        var deck = StarterDeck().ToList();
        UnstableShuffle(deck, rng.Next);

        _output.WriteLine($"raw shuffle seed : {rawSeed}");
        _output.WriteLine($"MegaRandom order : {string.Join(",", deck)}");
        _output.WriteLine($"live order       : {string.Join(",", LiveOrder)}");

        Assert.Equal(LiveOrder, deck);
    }
}
