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

        Assert.Contains(engine.State.BossEncounterId, RunConstants.HiveBossEncounters.ToArray());
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

    /// <summary>
    /// A new act opens on its MAP with the run not yet standing on it, so the only thing
    /// to travel to is the starting point — which <c>StandardActMap</c> stamps as an
    /// Ancient in every act, after every other assignment. Act 1 hides this because the
    /// run begins standing on Neow.
    /// </summary>
    [Fact]
    public void ANewActOffersOnlyItsAncient()
    {
        var engine = Started("ACT2TEST01");

        engine.EnterNextAct();

        Assert.True(engine.State.AwaitingActStartNode);
        Assert.Equal(RunConstants.NodeAncient, engine.State.MapNodeTypes[0]);
        Assert.Equal(RunConstants.NodeNone, engine.State.MapNodeTypes[1]);
    }

    [Fact]
    public void TravellingToItOpensTheAncient()
    {
        var engine = Started("ACT2TEST01");
        engine.EnterNextAct();

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
        Assert.False(engine.State.AwaitingActStartNode);
    }

    /// <summary>
    /// <c>AncientEventModel.BeforeEventStarted</c> heals toward full, and A8's
    /// WearyTraveler pays only 80% of what is missing. A capture crosses into act 2 on
    /// 264/280 and stands on Pael at 276 — sixteen missing, twelve given back.
    /// </summary>
    [Fact]
    public void EnteringAnAncientHealsEightyPercentOfWhatIsMissing()
    {
        var engine = Started("ACT2TEST01");
        engine.EnterNextAct();
        engine.State.PlayerMaxHp = 280;
        engine.State.PlayerHp = 264;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(276, engine.State.PlayerHp);
    }

    /// <summary>
    /// It is the same rule that gives a run its opening HP: Neow zeroes the player first,
    /// so the heal is 80% of the whole 80 max. That is where the hardcoded 64 comes from.
    /// </summary>
    [Fact]
    public void ThatRuleIsWhereTheRunsOpeningHpComesFrom()
    {
        var engine = Started("ACT2TEST01");

        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);
        Assert.Equal(engine.State.PlayerHp, (int)(engine.State.PlayerMaxHp * 0.8));
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
