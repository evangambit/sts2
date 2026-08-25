using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Every encounter that rolls its own composition must actually read its own stream.
/// </summary>
/// <remarks>
/// The plumbing has two halves and only one of them fails loudly. A builder can be handed
/// its <c>encounterRngSeed</c> and still fall back to the combat rng, because the seed is
/// only ever non-null if the encounter appears in <c>EncounterRng</c>'s table — so wiring
/// the builder and forgetting the table leaves the code looking correct and behaving
/// exactly as it did before. That is E90, and it is what happened to the Bowlbugs.
///
/// The bar here is the one that catches it: not "does the builder mention the seed" but
/// "does the ROSTER change when the seed does".
/// </remarks>
public class EncounterCompositionStreamTests
{
    /// <summary>
    /// Encounter ids whose <c>GenerateMonsters</c> draws, and whether a shared id needs
    /// the weak flag to name the right model.
    /// </summary>
    public static TheoryData<int, bool> RollsOwnComposition =>
        new()
        {
            { RunConstants.SlimesWeakEncounterId, true },
            { RunConstants.SlimesNormalEncounterId, false },
            { RunConstants.FlyconidNormalEncounterId, false },
            { RunConstants.TwoTailedRatsEncounterId, false },
            { RunConstants.RubyRaidersEncounterId, false },
            { RunConstants.SlitheringStranglerEncounterId, false },
            { RunConstants.CorpseSlugsEncounterId, true },
            { RunConstants.CorpseSlugsEncounterId, false },
            { RunConstants.BowlbugsWeakEncounterId, true },
            { RunConstants.BowlbugsNormalEncounterId, false },
            { RunConstants.DecimillipedeEncounterId, false },
            { RunConstants.ScrollsWeakEncounterId, true },
            { RunConstants.ScrollsNormalEncounterId, false },
            { RunConstants.PunchOffEncounterId, false },
        };

    [Theory]
    [MemberData(nameof(RollsOwnComposition))]
    public void EveryRollingEncounterHasAnEntryId(int encounterId, bool weakVariant)
    {
        Assert.NotNull(EncounterRng.EntryId(encounterId, weakVariant));
        Assert.NotNull(EncounterRng.SeedFor(12345, totalFloor: 3, encounterId, weakVariant));
    }

    /// <summary>
    /// The entries that were hand-transcribed and checked against live captures, pinned
    /// so the generated slugify cannot quietly disagree with them.
    /// </summary>
    [Theory]
    [InlineData("SlimesWeak", "SLIMES_WEAK")]
    [InlineData("SlimesNormal", "SLIMES_NORMAL")]
    [InlineData("CorpseSlugsWeak", "CORPSE_SLUGS_WEAK")]
    [InlineData("CorpseSlugsNormal", "CORPSE_SLUGS_NORMAL")]
    [InlineData("FlyconidNormal", "FLYCONID_NORMAL")]
    [InlineData("TwoTailedRatsNormal", "TWO_TAILED_RATS_NORMAL")]
    [InlineData("RubyRaidersNormal", "RUBY_RAIDERS_NORMAL")]
    [InlineData("SlitheringStranglerNormal", "SLITHERING_STRANGLER_NORMAL")]
    public void TheGeneratedEntriesMatchTheVerifiedOnes(string model, string expected)
    {
        Assert.Equal(expected, GeneratedData.EncounterTags.EntryForModel(model));
    }

    [Fact]
    public void AnEncounterThatDoesNotRollGetsNoStream()
    {
        Assert.Null(GeneratedData.EncounterTags.EntryForModel("FogmogNormal"));
        Assert.Null(EncounterRng.SeedFor(12345, 3, RunConstants.PunchConstructEncounterId, false));
    }

    /// <summary>
    /// The real bar: a different encounter seed produces a different roster. A builder
    /// that quietly fell back to the combat rng would answer the same every time.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.BowlbugsWeakEncounterId)]
    [InlineData(RunConstants.BowlbugsNormalEncounterId)]
    [InlineData(RunConstants.ScrollsWeakEncounterId)]
    [InlineData(RunConstants.ScrollsNormalEncounterId)]
    [InlineData(RunConstants.DecimillipedeEncounterId)]
    [InlineData(RunConstants.PunchOffEncounterId)]
    public void TheSeedChangesTheFight(int encounterId)
    {
        var shapes = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.EncounterWithStream(encounterId, seed);
            // Def ids for a composition roll, opening intents and HP for the rest --
            // between them they cover every roll these six make.
            shapes.Add(
                string.Join(
                    ",",
                    fight.State.Enemies.Select(e =>
                        $"{e.DefId}:{e.CurrentIntent.Type}:{e.CurrentIntent.Magnitude}:{e.Hp}"
                    )
                )
            );
        }

        Assert.True(
            shapes.Count > 1,
            $"encounter {encounterId} built the same fight for 24 different encounter seeds, "
                + "so its roll is not reading the encounter stream"
        );
    }
}
