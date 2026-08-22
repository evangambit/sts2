using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What the run observation tells an agent about what it is carrying.
///
/// Everything outside a fight is a decision about the deck -- which of three cards to
/// take, what to buy, what to upgrade at a fire, what to transform -- and until the deck
/// was in here the observation reported its size and nothing else, so none of those
/// decisions had the information they turn on.
/// </summary>
public class RunObservationTests
{
    private static int[] Observe(RunEngine engine)
    {
        var obs = new int[RunConstants.RunObsSize];
        engine.WriteObservation(obs);
        return obs;
    }

    private static int DeckSlot(int[] obs, int index, int field) =>
        obs[
            RunConstants.CombatObsSize
                + RunConstants.DeckObsOffset
                + index * RunConstants.DeckSlotSize
                + field
        ];

    private static int RelicSlot(int[] obs, int index, int field) =>
        obs[
            RunConstants.CombatObsSize
                + RunConstants.RelicObsOffset
                + index * RunConstants.RelicSlotSize
                + field
        ];

    [Fact]
    public void TheDeckIsCarriedCardByCardInDeckOrder()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        var obs = Observe(engine);

        Assert.NotEmpty(engine.State.Deck);
        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            Assert.Equal(engine.State.Deck[i].DefId, DeckSlot(obs, i, 0));
        }

        // One past the deck is empty, so a reader can stop at the first zero.
        Assert.Equal(0, DeckSlot(obs, engine.State.Deck.Count, 0));
    }

    /// <summary>
    /// The slot index IS the action index at a card-select screen. Sorting the deck into a
    /// canonical multiset would read more tidily and would leave the agent unable to say
    /// which card it meant.
    /// </summary>
    [Fact]
    public void ADeckSlotIsTheActionThatSelectsThatCard()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Deck.Add(new CardInstance(RunNonCombatEffects.NamedCard("Bash"), true));
        engine.State.PendingSelectionKind = DeckSelection.Remove;
        engine.State.PendingSelectionCount = 1;
        engine.State.Phase = RunPhase.TransformSelect;

        var obs = Observe(engine);
        int bashIndex = engine.State.Deck.Count - 1;
        Assert.Equal(engine.State.Deck[bashIndex].DefId, DeckSlot(obs, bashIndex, 0));

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        Assert.Equal(1, mask[bashIndex]);

        Assert.Equal(0, engine.Step(bashIndex, -1, out _, out _, out _));
        Assert.DoesNotContain(engine.State.Deck, card => card.Upgraded);
    }

    /// <summary>
    /// Upgrades and enchantments survive a fight, so they are part of what the deck view
    /// owes: a Sharp Strike is a different card from a Strike, and the amount it was
    /// enchanted at is not always 1.
    /// </summary>
    [Fact]
    public void ACardCarriesItsUpgradeAndItsEnchantment()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        int strike = engine.State.Deck[0].DefId;
        engine.State.Deck.Clear();
        engine.State.Deck.Add(
            new CardInstance(
                strike,
                Upgraded: true,
                Enchantment: Enchantment.Sharp,
                EnchantAmount: 2
            )
        );

        var obs = Observe(engine);
        Assert.Equal(1, DeckSlot(obs, 0, 1));
        Assert.Equal((int)Enchantment.Sharp, DeckSlot(obs, 0, 2));
        Assert.Equal(2, DeckSlot(obs, 0, 3));
    }

    /// <summary>
    /// A relic's counter is half of what it is worth, and a used-up one is still in the
    /// list doing nothing -- both of which the agent has to be able to tell.
    /// </summary>
    [Fact]
    public void ARelicCarriesItsCounterAndWhetherItIsSpent()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Relics.Clear();
        int spent = RunNonCombatEffects.CircletRelic;
        engine.State.Relics.Add(new RelicInstance(RunConstants.RelicSilverCrucible, Counter: 3));
        engine.State.Relics.Add(new RelicInstance(spent));
        engine.State.UsedUpRelics.Add(spent);

        var obs = Observe(engine);
        Assert.Equal(RunConstants.RelicSilverCrucible, RelicSlot(obs, 0, 0));
        Assert.Equal(3, RelicSlot(obs, 0, 1));
        Assert.Equal(0, RelicSlot(obs, 0, 2));
        Assert.Equal(spent, RelicSlot(obs, 1, 0));
        Assert.Equal(1, RelicSlot(obs, 1, 2));
    }

    /// <summary>
    /// A deck past the cap is truncated rather than overflowing into the relics, and the
    /// count at offset 3 still reports the real size -- so the truncation is visible.
    /// </summary>
    [Fact]
    public void ADeckPastTheCapIsTruncatedVisiblyAndDoesNotOverrun()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        int strike = engine.State.Deck[0].DefId;
        engine.State.Deck.Clear();
        for (int i = 0; i < RunConstants.MaxObservedDeck + 10; i++)
        {
            engine.State.Deck.Add(new CardInstance(strike, Upgraded: false));
        }

        engine.State.Relics.Clear();
        var obs = Observe(engine);

        Assert.Equal(engine.State.Deck.Count, obs[RunConstants.CombatObsSize + 3]);
        Assert.Equal(strike, DeckSlot(obs, RunConstants.MaxObservedDeck - 1, 0));
        Assert.Equal(0, RelicSlot(obs, 0, 0));
    }

    /// <summary>
    /// During a fight the deck view is still the run's deck: the piles are the combat's
    /// business, and what the agent owns does not change mid-encounter.
    /// </summary>
    [Fact]
    public void TheDeckIsStillReportedInsideACombat()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Phase = RunPhase.Map;
        while (engine.State.Phase != RunPhase.Combat)
        {
            var mask = new int[RunConstants.MaxActions];
            engine.WriteActionMask(mask);
            int action = Array.IndexOf(mask, 1);
            if (action < 0 || engine.Step(action, -1, out _, out bool terminal, out _) != 0)
            {
                break;
            }

            if (terminal)
            {
                break;
            }
        }

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        var obs = Observe(engine);
        Assert.Equal(engine.State.Deck[0].DefId, DeckSlot(obs, 0, 0));
    }

    private static int ShopSlot(int[] obs, int action, int field) =>
        obs[
            RunConstants.CombatObsSize
                + RunConstants.ShopObsOffset
                + action * RunConstants.ShopSlotSize
                + field
        ];

    /// <summary>
    /// Every slot a merchant sells, priced, and lined up with the action that buys it.
    /// Three of the seven cards were in the observation and none of the prices, so an
    /// agent could buy shop slot 5 without ever being shown what was on it -- and could
    /// not tell a 50-gold card from a 300-gold one on any slot.
    /// </summary>
    [Fact]
    public void TheShopBlockIsEveryPricedSlotInActionOrder()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        RunRewardGenerator.EnterShop(engine.State);

        var obs = Observe(engine);
        for (int i = 0; i < engine.State.ShopCards.Length; i++)
        {
            Assert.Equal(engine.State.ShopCards[i], ShopSlot(obs, i, 0));
            Assert.Equal(engine.State.ShopCosts[i], ShopSlot(obs, i, 1));
        }

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(engine.State.ShopRelics[i], ShopSlot(obs, 7 + i, 0));
            Assert.Equal(engine.State.ShopCosts[7 + i], ShopSlot(obs, 7 + i, 1));
            Assert.Equal(engine.State.ShopPotions[i], ShopSlot(obs, 10 + i, 0));
            Assert.Equal(engine.State.ShopCosts[10 + i], ShopSlot(obs, 10 + i, 1));
        }

        // The removal service has a price and nothing on it.
        Assert.Equal(0, ShopSlot(obs, RunConstants.ShopRemoveAction, 0));
        Assert.Equal(
            engine.State.ShopCosts[RunConstants.ShopRemoveAction],
            ShopSlot(obs, RunConstants.ShopRemoveAction, 1)
        );

        // Everything stocked really is stocked, so none of this is an empty board.
        Assert.All(engine.State.ShopCards, card => Assert.NotEqual(0, card));
    }

    /// <summary>
    /// A slot the agent can afford is a slot the mask offers, and the price it reads is the
    /// one the purchase charges -- which is the whole point of carrying the price.
    /// </summary>
    [Fact]
    public void TheShopBlockAgreesWithTheMaskAndWithWhatBuyingCosts()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        RunRewardGenerator.EnterShop(engine.State);

        var obs = Observe(engine);
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        for (int action = 0; action < engine.State.ShopCards.Length; action++)
        {
            bool affordable = engine.State.Gold >= ShopSlot(obs, action, 1);
            Assert.Equal(affordable ? 1 : 0, mask[action]);
        }

        int cheapest = Enumerable
            .Range(0, engine.State.ShopCards.Length)
            .OrderBy(action => ShopSlot(obs, action, 1))
            .First();
        engine.State.Gold = ShopSlot(obs, cheapest, 1);

        Assert.Equal(0, engine.Step(cheapest, -1, out _, out _, out _));
        Assert.Equal(0, engine.State.Gold);
    }

    /// <summary>
    /// The last-resort guard on the layout: the blocks sit end to end and the final one
    /// ends exactly at the observation's width, so none of them can overrun another.
    /// </summary>
    [Fact]
    public void TheBlocksTileTheObservationExactly()
    {
        Assert.Equal(RunConstants.RunScalarObsSize, RunConstants.DeckObsOffset);
        Assert.Equal(
            RunConstants.DeckObsOffset + RunConstants.MaxObservedDeck * RunConstants.DeckSlotSize,
            RunConstants.RelicObsOffset
        );
        Assert.Equal(
            RunConstants.RelicObsOffset
                + RunConstants.MaxObservedRelics * RunConstants.RelicSlotSize,
            RunConstants.ShopObsOffset
        );
        Assert.Equal(
            RunConstants.ShopObsOffset + RunConstants.ShopSlots * RunConstants.ShopSlotSize,
            RunConstants.RunExtraObsSize
        );
    }
}
