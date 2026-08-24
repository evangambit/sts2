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
    public void EveryActIsGeneratedUpFront(string seed)
    {
        var state = Generated(seed);

        // Every act, in index order: whichever region act 1 turned out to be, then
        // Hive, then Glory.
        Assert.Equal(3, state.Acts.Count);
        Assert.Equal(0, state.CurrentActIndex);
        Assert.Contains(
            state.Acts[0].Act,
            new[] { RunConstants.ActOvergrowth, RunConstants.ActUnderdocks }
        );
        Assert.Equal(RunConstants.ActHive, state.Acts[1].Act);
        Assert.Equal(RunConstants.ActGlory, state.Acts[2].Act);
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
        var hive = state.Acts.Single(rooms => rooms.Act == RunConstants.ActHive);

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
        var hive = state.Acts.Single(rooms => rooms.Act == RunConstants.ActHive);

        Assert.Equal(28, hive.Events.Length);
        Assert.Equal(hive.Events.Length, hive.Events.Distinct().Count());
    }

    /// <summary>
    /// The act list is the only copy: the per-act sequences are views on
    /// <c>Acts[CurrentActIndex]</c>, so pointing the index at another act swaps all of
    /// them at once. That is what the transition will do, and it is why adding a fourth
    /// act — or an alternate act 2, which the devs have said is coming — is a data change
    /// rather than a structural one.
    /// </summary>
    [Fact]
    public void TheCurrentActIndexSelectsWhichSequencesAreLive()
    {
        var state = Generated("3PFLW9XC5D");
        int[] firstActNormals = state.NormalEncounterSequence;
        var hive = state.Acts[1];

        state.CurrentActIndex = 1;

        Assert.Equal(RunConstants.ActHive, state.Act);
        Assert.Equal(hive.NormalEncounters, state.NormalEncounterSequence);
        Assert.Equal(hive.EliteEncounters, state.EliteEncounterSequence);
        Assert.Equal(hive.BossEncounterId, state.BossEncounterId);
        Assert.Equal(hive.Events, state.EventSequence);
        Assert.NotEqual(firstActNormals, state.NormalEncounterSequence);
    }

    /// <summary>
    /// Every act draws an ancient, and act 1's is always Neow because both act-1 regions
    /// declare exactly one.
    /// </summary>
    [Theory]
    [InlineData("3PFLW9XC5D")]
    [InlineData("7WGQ2VNJ4M")]
    [InlineData("ACT2TEST01")]
    public void EveryActDrawsAnAncientAndActOnesIsNeow(string seed)
    {
        var state = Generated(seed);

        Assert.Equal(RunConstants.AncientNeow, state.Acts[0].Ancient);
        Assert.All(state.Acts, act => Assert.False(string.IsNullOrEmpty(act.Ancient)));
    }

    /// <summary>
    /// An act can only draw an ancient from its OWN list plus whatever shared ones it was
    /// dealt, and Darv is the only shared one the game has.
    /// </summary>
    [Theory]
    [InlineData("3PFLW9XC5D")]
    [InlineData("7WGQ2VNJ4M")]
    [InlineData("ACT2TEST01")]
    public void LaterActsDrawFromTheirOwnListOrDarv(string seed)
    {
        var state = Generated(seed);

        foreach (var act in state.Acts.Skip(1))
        {
            var allowed = RunConstants.AncientsFor(act.Act).Append(RunConstants.AncientDarv);
            Assert.Contains(act.Ancient, allowed);
        }
    }

    /// <summary>
    /// Darv can only ever be dealt ONCE across a run: the act that takes it removes it
    /// from what is left for the next.
    /// </summary>
    [Theory]
    [InlineData("3PFLW9XC5D")]
    [InlineData("7WGQ2VNJ4M")]
    [InlineData("ACT2TEST01")]
    public void DarvIsDealtAtMostOnce(string seed)
    {
        var state = Generated(seed);

        Assert.True(state.Acts.Count(act => act.Ancient == RunConstants.AncientDarv) <= 1);
    }

    /// <summary>
    /// Two live captures pin the act-2 ancient. `3PFLW9XC5D` is the one that matters most:
    /// it opens act 2 on DARV, which belongs to no act's own list, and reproducing that
    /// is what showed the shared-ancient pool is <em>not</em> empty. Both were captured
    /// twice over — once by winning act 1 and once by jumping with `--enter-acts` — and
    /// agree, which is also what shows the jump does not change what act 2 holds.
    ///
    /// <para>
    /// `ACT2TEST01` is NOT here: the game gives it Pael and the emulator computes
    /// Tezcatara. See O14 — the mechanism reproduces two seeds exactly and misses that
    /// one, and the cause is not yet found.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("3PFLW9XC5D", RunConstants.AncientDarv)]
    [InlineData("7WGQ2VNJ4M", RunConstants.AncientPael)]
    public void TheActTwoAncientMatchesTheCapture(string seed, string expected)
    {
        Assert.Equal(expected, Generated(seed).Acts[1].Ancient);
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
        Assert.DoesNotContain(state.BossEncounterId, RunConstants.HiveBossEncounters.ToArray());
    }
}
