using System.Linq;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Every act's rooms are rolled at run start, off one stream, in index order.
/// </summary>
/// <remarks>
/// <c>RunManager.GenerateRooms</c> walks all of <c>State.Acts</c>; the emulator generated
/// the first and stopped, which left its UpFront two acts' worth of draws behind the
/// game's for the rest of the run. Nothing caught it because no committed trace reads
/// UpFront after generation — Scroll Boxes, Hefty Tablet and Lead Paperweight all moved to
/// PlayerRng.Rewards, and Lantern Key and Prismatic Gem have never come up.
/// </remarks>
public class ActGenerationTests
{
    private static RunState Generated(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        return engine.State;
    }

    [Theory]
    [InlineData("3PFLW9XC5D")]
    [InlineData("7WGQ2VNJ4M")]
    [InlineData("J09SPL8Y3V")]
    public void TheLaterActsAreGeneratedUpFront(string seed)
    {
        var state = Generated(seed);

        // Hive then Glory, whichever region act 1 turned out to be.
        Assert.Equal(
            [RunConstants.ActHive, RunConstants.ActGlory],
            state.LaterActRooms.Select(rooms => rooms.Act)
        );
    }

    /// <summary>
    /// Hive declares <c>NumberOfWeakEncounters = 2</c> and <c>BaseNumberOfRooms = 14</c>,
    /// against act 1's 3 and 15. The emulator hardcoded act 1's numbers, which is right
    /// for the only act it used to generate and wrong for every act after it.
    /// </summary>
    [Fact]
    public void HiveHasItsOwnRoomCountsAndPools()
    {
        var state = Generated("3PFLW9XC5D");
        var hive = state.LaterActRooms.Single(rooms => rooms.Act == RunConstants.ActHive);

        Assert.Equal(14, hive.NormalEncounters.Length);
        Assert.Equal(RunConstants.EliteSequenceLength, hive.EliteEncounters.Length);

        var weak = RunConstants.HiveWeakEncounters.ToArray();
        var normal = RunConstants.HiveNormalEncounters.ToArray();
        Assert.All(hive.NormalEncounters.Take(2), enc => Assert.Contains(enc, weak));
        Assert.All(hive.NormalEncounters.Skip(2), enc => Assert.Contains(enc, normal));
        Assert.All(
            hive.EliteEncounters,
            enc => Assert.Contains(enc, RunConstants.HiveEliteEncounters.ToArray())
        );
        Assert.Contains(hive.BossEncounterId, RunConstants.HiveBossEncounters.ToArray());
    }

    /// <summary>
    /// Hive's ten events plus the eighteen shared ones. The count is what pins the draw
    /// budget; which order they come out in is the shuffle's business.
    /// </summary>
    [Fact]
    public void HivesEventPoolIsItsOwnPlusTheSharedBlock()
    {
        var state = Generated("3PFLW9XC5D");
        var hive = state.LaterActRooms.Single(rooms => rooms.Act == RunConstants.ActHive);

        Assert.Equal(28, hive.Events.Length);
        Assert.Equal(hive.Events.Length, hive.Events.Distinct().Count());
    }

    /// <summary>
    /// Generating the later acts must not move the FIRST act by a single draw — that is
    /// the whole risk of the change, and thirty-one traces are the other half of the
    /// answer.
    /// </summary>
    [Theory]
    [InlineData("3PFLW9XC5D")]
    [InlineData("J09SPL8Y3V")]
    public void TheFirstActIsUnaffectedByWhatFollowsIt(string seed)
    {
        var state = Generated(seed);

        Assert.NotEmpty(state.NormalEncounterSequence);
        Assert.Equal(15, state.NormalEncounterSequence.Length);
        Assert.Equal(RunConstants.EliteSequenceLength, state.EliteEncounterSequence.Length);
        Assert.NotEqual(0, state.BossEncounterId);
        Assert.DoesNotContain(
            state.BossEncounterId,
            RunConstants.HiveBossEncounters.ToArray()
        );
    }
}
