using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Wood Carvings: three carvings that do three different things to one starter card.
///
/// The emulator did the same thing for all three -- transform the first card in the deck
/// into a rolled one. Bird and Torus transform a card the player CHOOSES into one named
/// card each; Snake enchants with Slither.
///
/// Slither's combat behaviour is not modelled (see <c>Enchantments.InertInCombat</c>), so
/// this suite covers what the run layer does, which is the part the event owns.
/// </summary>
public class WoodCarvingsTests
{
    private static RunEngine AtTheCarvings()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.EventId = RunConstants.EventWoodCarvings;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int FirstSelectable(RunEngine engine) =>
        Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));

    [Theory]
    [InlineData(0, "Peck")]
    [InlineData(2, "ToricToughness")]
    public void CarvingTransformsTheChosenCardIntoTheCarvingsOwnCard(int option, string card)
    {
        var engine = AtTheCarvings();

        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(DeckSelection.TransformTo, engine.State.PendingSelectionKind);

        int index = FirstSelectable(engine);
        int deckSize = engine.State.Deck.Count;
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(RunNonCombatEffects.NamedCard(card), engine.State.Deck[index].DefId);
        Assert.Equal(deckSize, engine.State.Deck.Count);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void TheSnakeEnchantsTheChosenCardWithSlither()
    {
        var engine = AtTheCarvings();

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal((int)Enchantment.Slither, engine.State.PendingSelectionArg);

        int index = FirstSelectable(engine);
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(Enchantment.Slither, engine.State.Deck[index].Enchantment);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    /// <summary>
    /// Bird and Torus carve a STARTER card -- the game filters the selector to
    /// <c>Rarity == Basic</c>, so Ascender's Bane is out even though it sits in the deck.
    /// </summary>
    [Fact]
    public void OnlyBasicCardsCanBeCarved()
    {
        var engine = AtTheCarvings();
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            var def = GeneratedData.Cards.Get(engine.State.Deck[i].DefId);
            Assert.Equal(def.Rarity == CardRarity.Basic, mask[i] != 0);
        }
    }

    /// <summary>
    /// The two selections read different rules, so they part company as soon as the deck
    /// holds a card that is playable but not Basic: the Snake will take it and the
    /// carvings will not. The starter deck alone cannot tell them apart -- every card in
    /// it is either Basic-and-enchantable or Ascender's Bane, which both refuse.
    /// </summary>
    [Fact]
    public void TheSnakeTakesAPlayableNonBasicCardAndTheCarvingsDoNot()
    {
        int peck = RunNonCombatEffects.NamedCard("Peck");
        Assert.NotEqual(CardRarity.Basic, GeneratedData.Cards.Get(peck).Rarity);

        var carve = AtTheCarvings();
        carve.State.Deck.Add(new CardInstance(peck, Upgraded: false));
        Assert.Equal(0, carve.Step(0, -1, out _, out _, out _));
        Assert.False(RunNonCombatEffects.CanSelectCard(carve.State, carve.State.Deck.Count - 1));

        var snake = AtTheCarvings();
        snake.State.Deck.Add(new CardInstance(peck, Upgraded: false));
        Assert.Equal(0, snake.Step(1, -1, out _, out _, out _));
        Assert.True(RunNonCombatEffects.CanSelectCard(snake.State, snake.State.Deck.Count - 1));
    }

    /// <summary>
    /// Ascender's Bane is out of both selections, but for two different reasons: it is
    /// not Basic, and it is a Curse that Slither would refuse anyway.
    /// </summary>
    [Fact]
    public void AscendersBaneIsOutOfEverySelection()
    {
        int bane = AtTheCarvings()
            .State.Deck.FindIndex(card =>
                GeneratedData.Cards.Get(card.DefId).Entry == "ASCENDERS_BANE"
            );
        Assert.True(bane >= 0);

        foreach (int option in new[] { 0, 1, 2 })
        {
            var engine = AtTheCarvings();
            Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
            Assert.False(RunNonCombatEffects.CanSelectCard(engine.State, bane));
        }
    }
}
