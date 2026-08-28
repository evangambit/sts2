using System.Linq;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The generated encounter tables against the hand-verified constants they replace.
/// </summary>
/// <remarks>
/// Two sources for the same fact is normally a smell; here it is the argument. Act 1's
/// and Hive's pools are the ones that have been checked against live captures, and the
/// generator reproducing all sixteen of them EXACTLY is what makes Glory's four -- which
/// no capture has ever reached -- trustworthy enough to generate act 3 from. If the two
/// ever disagree, one of them has drifted and this says which.
/// </remarks>
public class EncounterTagsAndPoolsTests
{
    public static TheoryData<int, string, int[]> VerifiedPools =>
        new()
        {
            { RunConstants.ActOvergrowth, "Weak", RunConstants.OvergrowthWeakEncounters.ToArray() },
            {
                RunConstants.ActOvergrowth,
                "Normal",
                RunConstants.OvergrowthNormalEncounters.ToArray()
            },
            {
                RunConstants.ActOvergrowth,
                "Elite",
                RunConstants.OvergrowthEliteEncounters.ToArray()
            },
            { RunConstants.ActOvergrowth, "Boss", RunConstants.OvergrowthBossEncounters.ToArray() },
            { RunConstants.ActUnderdocks, "Weak", RunConstants.UnderdocksWeakEncounters.ToArray() },
            {
                RunConstants.ActUnderdocks,
                "Normal",
                RunConstants.UnderdocksNormalEncounters.ToArray()
            },
            {
                RunConstants.ActUnderdocks,
                "Elite",
                RunConstants.UnderdocksEliteEncounters.ToArray()
            },
            { RunConstants.ActUnderdocks, "Boss", RunConstants.UnderdocksBossEncounters.ToArray() },
            { RunConstants.ActHive, "Weak", RunConstants.HiveWeakEncounters.ToArray() },
            { RunConstants.ActHive, "Normal", RunConstants.HiveNormalEncounters.ToArray() },
            { RunConstants.ActHive, "Elite", RunConstants.HiveEliteEncounters.ToArray() },
            { RunConstants.ActHive, "Boss", RunConstants.HiveBossEncounters.ToArray() },
        };

    [Theory]
    [MemberData(nameof(VerifiedPools))]
    public void TheGeneratorReproducesEveryVerifiedPool(int act, string kind, int[] expected)
    {
        Assert.Equal(expected, GeneratedData.EncounterTags.Pool(act, kind));
    }

    [Fact]
    public void GloryNoLongerBorrowsHivesEncounters()
    {
        foreach (string kind in new[] { "Weak", "Normal", "Elite", "Boss" })
        {
            var glory = GeneratedData.EncounterTags.Pool(RunConstants.ActGlory, kind);
            Assert.NotEmpty(glory);
            Assert.NotEqual(GeneratedData.EncounterTags.Pool(RunConstants.ActHive, kind), glory);
        }
    }

    [Fact]
    public void EveryActsPoolsAreDisjointAndNonEmpty()
    {
        int[] acts =
        [
            RunConstants.ActOvergrowth,
            RunConstants.ActUnderdocks,
            RunConstants.ActHive,
            RunConstants.ActGlory,
        ];
        foreach (int act in acts)
        {
            var elite = GeneratedData.EncounterTags.Pool(act, "Elite");
            var boss = GeneratedData.EncounterTags.Pool(act, "Boss");
            Assert.Equal(3, elite.Length);
            Assert.Equal(3, boss.Length);
            // A weak encounter is never a boss, and an id never appears twice in a pool.
            Assert.Empty(elite.Intersect(boss));
            foreach (string kind in new[] { "Weak", "Normal", "Elite", "Boss" })
            {
                var pool = GeneratedData.EncounterTags.Pool(act, kind);
                Assert.Equal(pool.Length, pool.Distinct().Count());
            }
        }
    }

    /// <summary>
    /// The four entries the hand-written tag switch had lost.
    /// </summary>
    /// <remarks>
    /// Three of them are Glory's, which is act 3 waiting to be handed exactly what E66
    /// did to act 2 — a missing tag changes how many draws a grab COSTS, so the boss,
    /// the ancient and the next act all land somewhere else.
    /// </remarks>
    [Theory]
    [InlineData(34, "Burrower", "Chomper")] // TunnelerNormal, which the weak one is not
    [InlineData(49, "Scrolls")] // ScrollsOfBitingWeak
    [InlineData(50, "Scrolls")] // ScrollsOfBitingNormal
    [InlineData(70, "Knights")] // KnightsElite
    public void TheTagsTheTranscriptionLostAreThere(int encounterId, params string[] expected)
    {
        Assert.Equal(expected, GeneratedData.EncounterTags.For(encounterId));
    }

    [Fact]
    public void AnUntaggedEncounterHasNoTags()
    {
        // NibbitsNormal declares no Tags -- only NibbitsWeak is tagged Nibbit. Tagging it
        // wrongly blocked the game's legitimate NibbitsWeak -> NibbitsNormal run.
        Assert.Empty(GeneratedData.EncounterTags.For(15));
    }
}
