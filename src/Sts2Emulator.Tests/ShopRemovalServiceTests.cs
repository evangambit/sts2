using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The merchant's card-removal service asks the player which card goes.
/// </summary>
/// <remarks>
/// It used to call <c>RemoveLowestPriorityCard</c>, which walked an ordering the emulator
/// invented — Ascender's Bane, then Defend, then Strike, then whatever sat last in the
/// deck. That is a policy, not a rule the game has: the merchant opens a removal screen
/// and the choice is the entire thing the gold buys. Worse, the ordering led with a card
/// the real screen will not even offer, so an agent paying 175 gold got a removal it
/// could not have asked for.
/// </remarks>
public class ShopRemovalServiceTests
{
    private static RunEngine ShopWithGold(int gold)
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.Phase = RunPhase.Shop;
        engine.State.Gold = gold;
        engine.State.ShopCosts[RunConstants.ShopRemoveAction] = 100;
        return engine;
    }

    [Fact]
    public void BuyingRemoval_OpensTheSelectionScreenInsteadOfChoosing()
    {
        var engine = ShopWithGold(200);
        int deckSize = engine.State.Deck.Count;

        int status = engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);

        Assert.Equal(0, status);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(DeckSelection.Remove, engine.State.PendingSelectionKind);
        // Nothing is gone yet -- the player has not picked.
        Assert.Equal(deckSize, engine.State.Deck.Count);
        Assert.Equal(100, engine.State.Gold);
    }

    [Fact]
    public void TheSelectionTakesTheCardThePlayerPicked()
    {
        var engine = ShopWithGold(200);
        engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);

        // Index 3, not the front and not the back: neither end of the old ordering. The
        // starting deck holds duplicates, so count copies rather than membership.
        var doomed = engine.State.Deck[3];
        int deckSize = engine.State.Deck.Count;
        int copies = engine.State.Deck.Count(card => card == doomed);

        int status = engine.Step(3, -1, out _, out _, out _);

        Assert.Equal(0, status);
        Assert.Equal(deckSize - 1, engine.State.Deck.Count);
        Assert.Equal(copies - 1, engine.State.Deck.Count(card => card == doomed));
    }

    [Fact]
    public void TheSelectionReturnsToTheShop_NotToAnEvent()
    {
        // The reason the conversion needed SelectionReturn at all: every selection used
        // to land on RunPhase.Event, which for a shop is a screen that is not there.
        var engine = ShopWithGold(200);
        engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Shop, engine.State.Phase);
        Assert.Equal(DeckSelection.None, engine.State.PendingSelectionKind);
    }

    [Fact]
    public void TheServiceIsStockedOncePerVisit()
    {
        // MerchantCardRemovalEntry: IsStocked => !Used. The gold is not the only limit --
        // a second removal at the same merchant is not for sale at any price, which the
        // emulator did not model at all until the conversion went looking.
        var engine = ShopWithGold(500);
        engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);
        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(1, engine.State.ShopRemovalsUsed);
        Assert.True(engine.State.ShopRemovalUsedThisVisit);
        Assert.Equal(RunPhase.Shop, engine.State.Phase);

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        Assert.Equal(0, mask[RunConstants.ShopRemoveAction]);
        // The rest of the shop is still open for business.
        Assert.Equal(1, mask[RunConstants.ShopSkipAction]);

        int deckSize = engine.State.Deck.Count;
        Assert.Equal(-1, engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _));
        Assert.Equal(deckSize, engine.State.Deck.Count);
        Assert.Equal(400, engine.State.Gold);
    }

    [Fact]
    public void ThePriceRisesWithTheRunsRemovals_AndTheNextShopStocksItAgain()
    {
        var engine = ShopWithGold(500);
        engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);
        engine.Step(0, -1, out _, out _, out _);

        RunRewardGenerator.EnterShop(engine.State);

        Assert.False(engine.State.ShopRemovalUsedThisVisit);
        // BaseCost + PriceIncrease * CardShopRemovalsUsed, and the count is the RUN's.
        Assert.Equal(150, engine.State.ShopCosts[RunConstants.ShopRemoveAction]);
    }

    [Fact]
    public void TooLittleGold_BuysNothingAndOpensNothing()
    {
        var engine = ShopWithGold(50);
        int deckSize = engine.State.Deck.Count;

        int status = engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);

        Assert.Equal(-1, status);
        Assert.Equal(RunPhase.Shop, engine.State.Phase);
        Assert.Equal(50, engine.State.Gold);
        Assert.Equal(deckSize, engine.State.Deck.Count);
    }

    [Fact]
    public void ADeckWithNothingToTake_ChargesNoGold()
    {
        // Opening comes before charging, so a screen that cannot appear is not a sale.
        var engine = ShopWithGold(200);
        engine.State.Deck.Clear();

        int status = engine.Step(RunConstants.ShopRemoveAction, -1, out _, out _, out _);

        Assert.Equal(-1, status);
        Assert.Equal(200, engine.State.Gold);
        Assert.Equal(0, engine.State.ShopRemovalsUsed);
    }
}
