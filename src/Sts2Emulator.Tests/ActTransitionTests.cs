using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Beating a boss ends the ACT, and only ends the RUN in the last one.
/// </summary>
/// <remarks>
/// <c>RunManager.EnterNextAct</c> goes to the next act unless
/// <c>CurrentActIndex >= Acts.Count - 1</c>. <c>AdvanceAfterRelicReward</c> treated every
/// boss as terminal, which is what made act 2 unreachable.
/// </remarks>
public class ActTransitionTests
{
    private static RunEngine Started(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        return engine;
    }

    [Fact]
    public void EnteringTheNextActLandsOnItsMap()
    {
        var engine = Started("3PFLW9XC5D");
        int firstAct = engine.State.Act;

        Assert.True(engine.EnterNextAct());

        Assert.Equal(1, engine.State.CurrentActIndex);
        Assert.Equal(RunConstants.ActHive, engine.State.Act);
        Assert.NotEqual(firstAct, engine.State.Act);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    /// <summary>
    /// Moving the index swaps which act's rooms are live, because the sequences are views
    /// on <c>Acts[CurrentActIndex]</c> rather than copies.
    /// </summary>
    [Fact]
    public void TheNewActsOwnEncountersBecomeLive()
    {
        var engine = Started("3PFLW9XC5D");
        engine.EnterNextAct();

        Assert.Contains(
            engine.State.BossEncounterId,
            RunConstants.HiveBossEncounters.ToArray()
        );
        Assert.All(
            engine.State.EliteEncounterSequence,
            enc => Assert.Contains(enc, RunConstants.HiveEliteEncounters.ToArray())
        );
    }

    /// <summary>
    /// The FLOOR carries on. A live capture crosses into act 2 still on floor 17 and
    /// counts up from there; resetting it would renumber the rest of the run.
    /// </summary>
    [Fact]
    public void TheFloorCarriesAcrossTheBoundary()
    {
        var engine = Started("3PFLW9XC5D");
        engine.State.Floor = 17;

        engine.EnterNextAct();

        Assert.Equal(17, engine.State.Floor);
    }

    /// <summary>
    /// <c>SetActInternal</c> calls <c>Odds.UnknownMapPoint.ResetToBase()</c> — the odds
    /// climb as a run walks question marks and start each act fresh.
    /// </summary>
    [Fact]
    public void TheUnknownMapPointOddsResetPerAct()
    {
        var engine = Started("3PFLW9XC5D");
        engine.State.UnknownMapPointsVisited = 5;
        engine.State.UnknownMapPointMonsterOdds = 0.9;
        engine.State.UnknownMapPointShopOdds = 0.5;

        engine.EnterNextAct();

        Assert.Equal(0, engine.State.UnknownMapPointsVisited);
        Assert.Equal(0.1, engine.State.UnknownMapPointMonsterOdds);
        Assert.Equal(0.03, engine.State.UnknownMapPointShopOdds);
    }

    /// <summary>Each act's map comes off its own <c>act_N_map</c> stream.</summary>
    [Fact]
    public void EachActGetsItsOwnMap()
    {
        var engine = Started("3PFLW9XC5D");
        var firstMap = engine.State.MapNodes.Keys.ToHashSet();
        var firstTypes = engine.State.MapNodes.ToDictionary(n => n.Key, n => n.Value.NodeType);

        engine.EnterNextAct();

        var secondTypes = engine.State.MapNodes.ToDictionary(n => n.Key, n => n.Value.NodeType);
        Assert.NotEqual(firstTypes, secondTypes);
        Assert.NotEmpty(secondTypes);
    }

    /// <summary>The last act has nowhere to go, and that is where a run ends.</summary>
    [Fact]
    public void TheLastActRefusesToAdvance()
    {
        var engine = Started("3PFLW9XC5D");

        Assert.True(engine.EnterNextAct());
        Assert.True(engine.EnterNextAct());
        Assert.False(engine.EnterNextAct());
        Assert.Equal(engine.State.Acts.Count - 1, engine.State.CurrentActIndex);
    }
}
