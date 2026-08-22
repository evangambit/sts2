using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Waterlogged Scriptorium: Max HP for free, or Steady on one card for 55 gold, or on
/// two for 99.
///
/// All three options were wrong. Bloody Ink is the FREE option and comes first; the
/// emulator had it second. The 55-gold option upgraded a card instead of enchanting one,
/// and the 99-gold option was a card reward. This is also the only Act 1 event that takes
/// TWO cards in one selection, so it is what pins the multi-card path.
/// </summary>
public class WaterloggedScriptoriumTests
{
    private static RunEngine AtTheScriptorium(int gold = 99)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Gold = gold;
        engine.State.EventId = RunConstants.EventWaterloggedScriptorium;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int FirstSelectable(RunEngine engine) =>
        Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));

    [Fact]
    public void BloodyInkIsFreeAndGrantsSixMaxHp()
    {
        var engine = AtTheScriptorium();
        int maxHp = engine.State.PlayerMaxHp;
        int hp = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(maxHp + 6, engine.State.PlayerMaxHp);
        Assert.Equal(hp + 6, engine.State.PlayerHp);
        Assert.Equal(99, engine.State.Gold);
    }

    [Fact]
    public void TheTentacleQuillCostsFiftyFiveAndEnchantsOneCard()
    {
        var engine = AtTheScriptorium();

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(44, engine.State.Gold);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(1, engine.State.PendingSelectionCount);

        int index = FirstSelectable(engine);
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(Enchantment.Steady, engine.State.Deck[index].Enchantment);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Single(engine.State.Deck.Where(card => card.Enchantment == Enchantment.Steady));
    }

    [Fact]
    public void ThePricklySpongeCostsNinetyNineAndEnchantsTwoCards()
    {
        var engine = AtTheScriptorium();

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));
        Assert.Equal(0, engine.State.Gold);
        Assert.Equal(2, engine.State.PendingSelectionCount);

        // The screen stays open for the second card.
        int first = FirstSelectable(engine);
        Assert.Equal(0, engine.Step(first, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(1, engine.State.PendingSelectionCount);

        int second = FirstSelectable(engine);
        Assert.NotEqual(first, second);
        Assert.Equal(0, engine.Step(second, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(2, engine.State.Deck.Count(card => card.Enchantment == Enchantment.Steady));
    }

    /// <summary>
    /// An already-Steady card is not offered again, so the sponge cannot spend both of
    /// its picks on one card.
    /// </summary>
    [Fact]
    public void TheSecondPickCannotBeTheFirstCard()
    {
        var engine = AtTheScriptorium();
        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        int first = FirstSelectable(engine);
        Assert.Equal(0, engine.Step(first, -1, out _, out _, out _));

        Assert.Equal(-1, engine.Step(first, -1, out _, out _, out _));
        Assert.Equal(1, engine.State.PendingSelectionCount);
    }

    [Theory]
    [InlineData(54, 1)]
    [InlineData(0, 1)]
    [InlineData(98, 2)]
    [InlineData(54, 2)]
    public void APaidOptionTheRunCannotAffordIsRefusedAndCostsNothing(int gold, int option)
    {
        var engine = AtTheScriptorium(gold);

        Assert.Equal(-1, engine.Step(option, -1, out _, out _, out _));

        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    /// <summary>
    /// The gold is spent when the selector opens and the game does not refund it, so a
    /// selection that cannot open must not have taken it either.
    /// </summary>
    [Fact]
    public void ARefusedSelectionDoesNotSpendTheGold()
    {
        var engine = AtTheScriptorium();
        engine.State.Deck.Clear();

        Assert.Equal(-1, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }
}
