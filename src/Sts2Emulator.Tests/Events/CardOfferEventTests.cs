using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The last four Act 1 events: two that open a grid of freshly rolled cards, one that
/// keeps offering until you walk away, and one that answers with a fight.
///
/// All four are ordinary two-option events on their first page, which is all a capture
/// ever sees. What it cannot see is what the option opens -- and for every one of them
/// that is the option's entire content.
/// </summary>
[CoversEvent("BrainLeech")]
[CoversEvent("EndlessConveyor")]
[CoversEvent("PunchOff")]
[CoversEvent("RoomFullOfCheese")]
public class CardOfferEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    private static bool StillInTheEvent(RunState state) =>
        state.Phase == RunPhase.Event && state.EventId != RunConstants.EventResultPending;

    // ── Brain Leech ──────────────────────────────────────────────────────────

    [Fact]
    public void SharingKnowledgeOffersFiveCardsAndKeepsOne()
    {
        var engine = At(RunConstants.EventBrainLeech);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(5, engine.State.PendingOfferCards.Length);
        Assert.Equal(5, engine.State.PendingOfferCards.Distinct().Count());
        Assert.Equal(deck, engine.State.Deck.Count);

        int wanted = engine.State.PendingOfferCards[2];
        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.Equal(deck + 1, engine.State.Deck.Count);
        Assert.Equal(wanted, engine.State.Deck[^1].DefId);
        Assert.False(StillInTheEvent(engine.State));
    }

    [Fact]
    public void TheGridOffersEveryCardAndNothingElse()
    {
        var engine = At(RunConstants.EventBrainLeech);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        for (int i = 0; i < 5; i++)
        {
            Assert.NotEqual(0, mask[i]);
        }

        Assert.Equal(0, mask[5]);
    }

    /// <summary>
    /// Ripping the leech off offers a COLOURLESS card reward. Three card ids stood here,
    /// hard-written, so every run got the same three whatever the seed.
    /// </summary>
    [Fact]
    public void RippingTheLeechOffOffersColourlessCards()
    {
        var colourless = GeneratedData.CardPools.Colorless.ToArray().ToHashSet();

        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            var engine = At(RunConstants.EventBrainLeech, seed);

            Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

            Assert.Equal(59, engine.State.PlayerHp);
            Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
            Assert.True(engine.State.RewardCardPending);

            var offered = engine.State.RewardCards.Where(card => card != 0).ToList();
            Assert.Equal(3, offered.Count);
            Assert.All(offered, card => Assert.Contains(card, colourless));
        }
    }

    [Fact]
    public void TheColourlessRewardIsNotTheSameThreeCardsEveryRun()
    {
        var seen = new HashSet<string>();
        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            var engine = At(RunConstants.EventBrainLeech, seed);
            Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
            seen.Add(string.Join(",", engine.State.RewardCards));
        }

        Assert.True(seen.Count > 1, "every seed offered the same three cards");
    }

    // ── Room Full of Cheese ──────────────────────────────────────────────────

    [Fact]
    public void GorgingOffersEightCommonsAndKeepsTwo()
    {
        var engine = At(RunConstants.EventRoomFullOfCheese);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(8, engine.State.PendingOfferCards.Length);
        Assert.All(
            engine.State.PendingOfferCards,
            card => Assert.Equal(CardRarity.Common, GeneratedData.Cards.Get(card).Rarity)
        );

        // The screen stays open for the second pick.
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(deck + 1, engine.State.Deck.Count);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(deck + 2, engine.State.Deck.Count);
        Assert.False(StillInTheEvent(engine.State));
    }

    /// <summary>A card taken off the grid is gone from it, so two picks are two cards.</summary>
    [Fact]
    public void TheSameCardCannotBeTakenTwice()
    {
        var engine = At(RunConstants.EventRoomFullOfCheese);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        int first = engine.State.PendingOfferCards[0];
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(7, engine.State.PendingOfferCards.Length);
        Assert.DoesNotContain(first, engine.State.PendingOfferCards);
    }

    [Fact]
    public void SearchingCostsFourteenAndBuysTheChosenCheese()
    {
        var engine = At(RunConstants.EventRoomFullOfCheese);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(50, engine.State.PlayerHp);
        Assert.Equal(
            RunNonCombatEffects.NamedRelic("ChosenCheese"),
            engine.State.Relics[^1].DefId
        );
    }

    // ── The Endless Conveyor ─────────────────────────────────────────────────

    /// <summary>
    /// The belt keeps turning: every grab pays for a dish and offers the next one, so the
    /// event is a loop the player leaves rather than a single choice.
    /// </summary>
    [Fact]
    public void TheBeltKeepsOfferingUntilYouLeave()
    {
        var engine = At(RunConstants.EventEndlessConveyor);
        engine.State.Gold = 999;

        for (int i = 0; i < 4; i++)
        {
            Assert.Contains(0, Offered(engine));
            int gold = engine.State.Gold;

            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

            Assert.Equal(gold - RunConstants.ConveyorGrabCost, engine.State.Gold);
            Assert.True(
                StillInTheEvent(engine.State) || engine.State.Phase == RunPhase.TransformSelect,
                $"the belt stopped after {i + 1} grabs"
            );
            if (engine.State.Phase == RunPhase.TransformSelect)
            {
                Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
            }
        }
    }

    [Fact]
    public void GrabbingIsLockedWhenTheRunCannotPay()
    {
        var engine = At(RunConstants.EventEndlessConveyor);
        engine.State.Gold = RunConstants.ConveyorGrabCost - 1;

        Assert.DoesNotContain(0, Offered(engine));
        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));
    }

    /// <summary>
    /// Observe the Chef belongs to the FIRST page only. Once the player has grabbed, the
    /// second option is Leave -- upgrading a card for it paid them to walk away.
    /// </summary>
    [Fact]
    public void LeavingTheBeltUpgradesNothing()
    {
        var engine = At(RunConstants.EventEndlessConveyor);
        engine.State.Gold = 999;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        if (engine.State.Phase == RunPhase.TransformSelect)
        {
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        }

        int upgraded = engine.State.Deck.Count(card => card.Upgraded);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(upgraded, engine.State.Deck.Count(card => card.Upgraded));
        Assert.False(StillInTheEvent(engine.State));
    }

    [Fact]
    public void ObservingTheChefUpgradesOneCardOnTheFirstPage()
    {
        var engine = At(RunConstants.EventEndlessConveyor);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(1, engine.State.Deck.Count(card => card.Upgraded));
        Assert.False(StillInTheEvent(engine.State));
    }

    // ── Punch Off ────────────────────────────────────────────────────────────

    [Fact]
    public void NabbingTakesAnInjuryAndOffersARelic()
    {
        var engine = At(RunConstants.EventPunchOff);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(
            1,
            engine.State.Deck.Count(card =>
                card.DefId == RunNonCombatEffects.NamedCard("Injury")
            )
        );
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.NotEqual(0, engine.State.RelicReward);
        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(64, engine.State.PlayerHp);
    }

    /// <summary>
    /// "I Can Take Them" does not fight: it answers with a page whose only option does,
    /// which is what the capture shows -- the run stays on the event and nothing changes.
    /// </summary>
    [Fact]
    public void TakingThemOnAnswersWithAPageBeforeTheFight()
    {
        var engine = At(RunConstants.EventPunchOff);
        int hp = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.True(StillInTheEvent(engine.State));
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(new[] { 0 }, Offered(engine));
    }

    [Fact]
    public void TheSecondPageStartsTheFight()
    {
        var engine = At(RunConstants.EventPunchOff);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveCombat);
    }

    [Fact]
    public void TheSecondPageOffersNothingElse()
    {
        var engine = At(RunConstants.EventPunchOff);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(-1, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }
}
