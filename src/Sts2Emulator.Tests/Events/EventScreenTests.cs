using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The two screens events open that the emulator had no shape for: a grid of cards the
/// event rolled, and a second page of its own options.
///
/// Both used to be resolved by the emulator on the player's behalf -- Brain Leech picked
/// a card itself, Punch Off refused the option outright -- which is the same defect in
/// two directions: an offer the agent never gets to answer.
/// </summary>
public class EventScreenTests
{
    private static RunEngine AtEvent(int eventId)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] LegalActions(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return [.. Enumerable.Range(0, mask.Length).Where(i => mask[i] != 0)];
    }

    [Fact]
    public void ShareKnowledgeOffersFiveCardsAndKeepsOne()
    {
        var engine = AtEvent(RunConstants.EventBrainLeech);
        int deckBefore = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(5, engine.State.PendingOfferCards.Length);
        Assert.Equal(deckBefore, engine.State.Deck.Count);
        // The grid is the action space now, not the deck.
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, LegalActions(engine));

        int chosen = engine.State.PendingOfferCards[2];
        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.Equal(deckBefore + 1, engine.State.Deck.Count);
        Assert.Equal(chosen, engine.State.Deck[^1].DefId);
        Assert.Empty(engine.State.PendingOfferCards);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void GorgeOffersEightCommonsAndKeepsTwo()
    {
        var engine = AtEvent(RunConstants.EventRoomFullOfCheese);
        int deckBefore = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(8, engine.State.PendingOfferCards.Length);
        Assert.All(
            engine.State.PendingOfferCards,
            cardId => Assert.Equal(CardRarity.Common, GeneratedData.Cards.Get(cardId).Rarity)
        );

        // The first pick leaves the grid and the screen stays up for the second.
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(7, engine.State.PendingOfferCards.Length);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(deckBefore + 2, engine.State.Deck.Count);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void ICanTakeThemOpensAPageWhoseOnlyOptionIsTheFight()
    {
        var engine = AtEvent(RunConstants.EventPunchOff);
        int hpBefore = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        // The option itself changes nothing; it just turns the page.
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(hpBefore, engine.State.PlayerHp);
        Assert.Equal(1, engine.State.EventPage);
        // The fight, plus the leave-without-choosing action the emulator offers at every
        // event and the game offers at almost none -- its own divergence, tracked by
        // EventOptionGatingTests rather than here.
        Assert.Equal(new[] { 0, RunConstants.EventSkipAction }, LegalActions(engine));
    }

    [Fact]
    public void TheFightOnThatPageIsARealFight()
    {
        var engine = AtEvent(RunConstants.EventPunchOff);
        engine.Step(1, -1, out _, out _, out _);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveCombat);
        Assert.NotEmpty(engine.State.ActiveCombat!.Enemies);
    }

    [Fact]
    public void AFreshEventStartsOnItsFirstPage()
    {
        var engine = AtEvent(RunConstants.EventPunchOff);
        engine.Step(1, -1, out _, out _, out _);
        Assert.Equal(1, engine.State.EventPage);

        RunNonCombatEffects.EnterEvent(engine.State);

        Assert.Equal(0, engine.State.EventPage);
    }
}
