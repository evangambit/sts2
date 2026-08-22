using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Tea Master: three named teas at three prices, one of them free.
///
/// The action mask already read <c>BoneTeaCost</c>, but the step charged the Ember price
/// for both paid teas -- so a run holding 50 gold was offered Bone Tea and then refused
/// when it took it. All three teas also handed over a relic rolled from the reward pool
/// rather than the tea itself.
///
/// Ember Tea has no live fixture: the capture was taken with 99 gold, which locks it. Its
/// price and its relic come from the event's own DynamicVars.
/// </summary>
public class TeaMasterTests
{
    private static RunEngine AtTheTeaMaster(int gold)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Gold = gold;
        engine.State.EventId = RunConstants.EventTeaMaster;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] OfferedOptions(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    [Fact]
    public void BoneTeaCostsFiftyNotTheEmberPrice()
    {
        var engine = AtTheTeaMaster(50);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(0, engine.State.Gold);
        Assert.Equal(RunNonCombatEffects.NamedRelic("BoneTea"), engine.State.Relics[^1].DefId);
    }

    [Fact]
    public void EmberTeaCostsOneHundredAndFifty()
    {
        var engine = AtTheTeaMaster(150);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(0, engine.State.Gold);
        Assert.Equal(RunNonCombatEffects.NamedRelic("EmberTea"), engine.State.Relics[^1].DefId);
    }

    [Fact]
    public void TeaOfDiscourtesyIsFree()
    {
        var engine = AtTheTeaMaster(0);

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.Equal(0, engine.State.Gold);
        Assert.Equal(
            RunNonCombatEffects.NamedRelic("TeaOfDiscourtesy"),
            engine.State.Relics[^1].DefId
        );
    }

    /// <summary>
    /// What is offered and what is takeable have to agree. Each paid tea unlocks at its
    /// own price, and a tea that is offered is never refused.
    /// </summary>
    [Theory]
    [InlineData(0, new[] { 2 })]
    [InlineData(49, new[] { 2 })]
    [InlineData(50, new[] { 0, 2 })]
    [InlineData(149, new[] { 0, 2 })]
    [InlineData(150, new[] { 0, 1, 2 })]
    public void EachTeaUnlocksAtItsOwnPrice(int gold, int[] expected)
    {
        Assert.Equal(expected, OfferedOptions(AtTheTeaMaster(gold)));

        foreach (int option in expected)
        {
            var engine = AtTheTeaMaster(gold);
            Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(49, 0)]
    [InlineData(0, 1)]
    [InlineData(149, 1)]
    public void ATeaTheRunCannotAffordIsRefused(int gold, int option)
    {
        var engine = AtTheTeaMaster(gold);

        Assert.Equal(-1, engine.Step(option, -1, out _, out _, out _));

        Assert.Equal(gold, engine.State.Gold);
        Assert.Single(engine.State.Relics);
    }

    [Fact]
    public void TheThreeTeasAreThreeDifferentRelics()
    {
        var teas = new[] { "BoneTea", "EmberTea", "TeaOfDiscourtesy" }
            .Select(RunNonCombatEffects.NamedRelic)
            .ToArray();

        Assert.Equal(3, teas.Distinct().Count());
    }
}
