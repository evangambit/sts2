using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Colossal Flower: a three-rung ladder of 35 / 75 / 135 gold, each rung left costing
/// 5 / 6 / 7 unblockable damage, and the bottom swapping the gold for Pollinous Core.
/// </summary>
/// <remarks>
/// The emulator had two flat options -- 125 gold or a rolled potion -- with no ladder, no
/// damage, a number the event never pays and a potion it never offers. `NumberOfDigs` is
/// the whole event, and it was not modelled at all.
///
/// The damage indexing is the trap: `ReachDeeper` awaits `DealReachDeeperDamage()` and
/// increments AFTERWARDS, so the cost of a dig is the rung you are LEAVING, not the one
/// you arrive at. First dig 5, second 6, and the Pollinous Core dig 7.
/// </remarks>
[CoversEvent("ColossalFlower")]
public class ColossalFlowerTests
{
    private static RunEngine At()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventColossalFlower;
        return engine;
    }

    [Fact]
    public void TheFirstPrizeIsThirtyFive()
    {
        var engine = At();
        int gold = engine.State.Gold;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(gold + 35, engine.State.Gold);
    }

    /// <summary>Each dig costs the rung it leaves, and the prize grows with the rung.</summary>
    [Theory]
    [InlineData(1, 5, 75)]
    [InlineData(2, 11, 135)]
    public void DiggingDeeperCostsHpAndRaisesThePrize(int digs, int damage, int prize)
    {
        var engine = At();
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;

        for (int i = 0; i < digs; i++)
        {
            Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        }

        Assert.Equal(digs, engine.State.EventPage);
        Assert.Equal(hp - damage, engine.State.PlayerHp);

        engine.Step(0, -1, out _, out _, out _);
        Assert.Equal(gold + prize, engine.State.Gold);
    }

    /// <summary>
    /// The third dig is Pollinous Core: 7 more damage, no gold, and the relic.
    /// </summary>
    [Fact]
    public void TheBottomOfTheLadderPaysARelicNotGold()
    {
        var engine = At();
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;
        int relics = engine.State.Relics.Count;

        engine.Step(1, -1, out _, out _, out _);
        engine.Step(1, -1, out _, out _, out _);
        engine.Step(1, -1, out _, out _, out _);

        Assert.Equal(hp - 18, engine.State.PlayerHp);
        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(relics + 1, engine.State.Relics.Count);
        Assert.Contains(
            engine.State.Relics,
            relic => relic.DefId == RunNonCombatEffects.NamedRelic("PollinousCore")
        );
    }

    /// <summary>
    /// `ExtractInstead` on the last page pays the SAME 135 as `ExtractCurrentPrize` --
    /// two methods, one number, because the choice there is gold OR the relic.
    /// </summary>
    [Fact]
    public void ExtractingInsteadOfTakingTheCorePaysTheSameOneThirtyFive()
    {
        var engine = At();
        engine.Step(1, -1, out _, out _, out _);
        engine.Step(1, -1, out _, out _, out _);
        int gold = engine.State.Gold;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(gold + 135, engine.State.Gold);
        Assert.DoesNotContain(
            engine.State.Relics,
            relic => relic.DefId == RunNonCombatEffects.NamedRelic("PollinousCore")
        );
    }

    /// <summary>
    /// `CurrentHp >= 19` is one more than the whole 18-point climb, so the ladder can
    /// never be the thing that kills you -- and a run at 18 is not shown the flower.
    /// </summary>
    [Theory]
    [InlineData(19, true)]
    [InlineData(18, false)]
    public void ItOnlyTurnsUpForARunThatCanSurviveTheWholeClimb(int hp, bool allowed)
    {
        var engine = At();
        engine.State.PlayerHp = hp;

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForRun(engine.State, RunConstants.EventColossalFlower)
        );
    }
}
