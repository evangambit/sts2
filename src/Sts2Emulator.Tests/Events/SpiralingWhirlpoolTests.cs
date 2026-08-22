using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Spiraling Whirlpool: enchant a starter card with Spiral, or drink a third of your
/// Max HP back.
///
/// The emulator had the two options' effects swapped and both amounts wrong -- Observe
/// healed the rest-site amount and Drink transformed a card. Drink actually heals
/// <c>MaxHp * 0.33m</c> through <c>DynamicVar.IntValue</c>, which is a plain
/// <c>(int)</c> cast and so truncates.
/// </summary>
public class SpiralingWhirlpoolTests
{
    private static RunEngine AtTheWhirlpool()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.EventId = RunConstants.EventSpiralingWhirlpool;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    [Fact]
    public void ObservingOpensACardSelectionForSpiral()
    {
        var engine = AtTheWhirlpool();
        int hp = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(DeckSelection.Enchant, engine.State.PendingSelectionKind);
        Assert.Equal((int)Enchantment.Spiral, engine.State.PendingSelectionArg);
        Assert.Equal(hp, engine.State.PlayerHp);
    }

    /// <summary>
    /// Spiral only takes a Basic Strike or Defend, so the selection offers exactly the
    /// starter deck's Strikes and Defends -- not Bash, and not Ascender's Bane.
    /// </summary>
    [Fact]
    public void OnlyBasicStrikesAndDefendsAreOffered()
    {
        var engine = AtTheWhirlpool();
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        var offered = Enumerable
            .Range(0, engine.State.Deck.Count)
            .Where(i => mask[i] != 0)
            .Select(i => GeneratedData.Cards.Get(engine.State.Deck[i].DefId).Entry)
            .ToList();

        Assert.NotEmpty(offered);
        Assert.All(
            offered,
            entry => Assert.True(entry.StartsWith("STRIKE_") || entry.StartsWith("DEFEND_"), entry)
        );
        Assert.DoesNotContain("BASH", offered);
        Assert.DoesNotContain("ASCENDERS_BANE", offered);
    }

    [Fact]
    public void TakingTheSelectionEnchantsThatCardAndReturnsToTheEvent()
    {
        var engine = AtTheWhirlpool();
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(Enchantment.Spiral, engine.State.Deck[index].Enchantment);
        Assert.Equal(1, engine.State.Deck[index].EnchantAmount);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(DeckSelection.None, engine.State.PendingSelectionKind);
    }

    [Fact]
    public void ACardTheSelectionWouldNotTakeIsRefused()
    {
        var engine = AtTheWhirlpool();
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        int bash = engine
            .State.Deck.FindIndex(card =>
                GeneratedData.Cards.Get(card.DefId).Entry == "BASH"
            );
        Assert.True(bash >= 0);

        Assert.Equal(-1, engine.Step(bash, -1, out _, out _, out _));

        Assert.Equal(Enchantment.None, engine.State.Deck[bash].Enchantment);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
    }

    /// <summary>
    /// A deck with nothing Spiral can take refuses the option outright rather than
    /// opening an empty selection the player cannot leave.
    /// </summary>
    [Fact]
    public void ObservingIsRefusedWhenNoCardCanCarrySpiral()
    {
        var engine = AtTheWhirlpool();
        engine.State.Deck.RemoveAll(card =>
            Enchantments.CanEnchant(card, Enchantment.Spiral)
        );

        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(DeckSelection.None, engine.State.PendingSelectionKind);
    }

    [Theory]
    [InlineData(80, 26)]
    [InlineData(75, 24)] // 24.75 truncates, it does not round
    [InlineData(100, 33)]
    public void DrinkingHealsATruncatedThirdOfMaxHp(int maxHp, int expected)
    {
        var engine = AtTheWhirlpool();
        engine.State.PlayerMaxHp = maxHp;
        engine.State.PlayerHp = 1;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(1 + expected, engine.State.PlayerHp);
    }

    [Fact]
    public void DrinkingNeverHealsPastTheCap()
    {
        var engine = AtTheWhirlpool();

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(engine.State.PlayerMaxHp, engine.State.PlayerHp);
    }
}
