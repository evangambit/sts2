using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Trial: Accept draws a DEFENDANT off the event's own stream — merchant, noble or
/// nondescript — and each is a page of Guilty / Innocent with its own pair of outcomes.
/// Reject answers with a page offering Accept after all, or Double Down, which ends the
/// run.
/// </summary>
/// <remarks>
/// The emulator had two options: 10 HP for an upgraded random card, or 100 gold. Neither
/// is any of the eight things this event can do, and it never drew a defendant at all.
///
/// A live capture at seed ABCDEF drew the NOBLE and offered "Heal 10 HP" against "Add
/// Regret to your Deck. Obtain 300 Gold", which is what page 2 pays here.
/// </remarks>
[CoversEvent("Trial")]
public class TrialTests
{
    private static RunEngine At(string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventTrial;
        return engine;
    }

    private static int Card(string name) => RunNonCombatEffects.NamedCard(name);

    /// <summary>Forces a defendant, so each verdict can be tested on its own page.</summary>
    private static RunEngine OnPage(int page)
    {
        var engine = At();
        engine.State.EventPage = page;
        return engine;
    }

    [Fact]
    public void AcceptDrawsOneOfThreeDefendants()
    {
        var engine = At();

        engine.Step(0, -1, out _, out _, out _);

        Assert.InRange(engine.State.EventPage, 1, 3);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void RejectOpensThePageThatOffersAcceptAgain()
    {
        var engine = At();

        engine.Step(1, -1, out _, out _, out _);

        Assert.Equal(4, engine.State.EventPage);

        engine.Step(0, -1, out _, out _, out _);
        Assert.InRange(engine.State.EventPage, 1, 3);
    }

    /// <summary>`.ThatWillKillPlayerIf(_ => true)`, and the popup abandons the run.</summary>
    [Fact]
    public void DoubleDownEndsTheRunAsALoss()
    {
        var engine = At();
        engine.Step(1, -1, out _, out _, out _);

        engine.Step(1, -1, out _, out bool terminal, out _);

        Assert.True(terminal);
        Assert.Equal(0, engine.State.PlayerHp);
        Assert.False(engine.State.LastPlayerWon);
    }

    // ── the six verdicts ─────────────────────────────────────────────────────

    [Fact]
    public void MerchantGuiltyIsARegretAndTwoRelics()
    {
        var engine = OnPage(1);
        int relics = engine.State.Relics.Count;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Regret"));
        Assert.Equal(relics + 2, engine.State.Relics.Count);
    }

    [Fact]
    public void MerchantInnocentIsAShameAndTwoCardsThePlayerUpgrades()
    {
        var engine = OnPage(1);

        engine.Step(1, -1, out _, out _, out _);

        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Shame"));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        int upgradedBefore = engine.State.Deck.Count(c => c.Upgraded);
        for (int i = 0; i < 2; i++)
        {
            int index = Enumerable
                .Range(0, engine.State.Deck.Count)
                .First(j => RunNonCombatEffects.CanSelectCard(engine.State, j));
            engine.Step(index, -1, out _, out _, out _);
        }

        Assert.Equal(upgradedBefore + 2, engine.State.Deck.Count(c => c.Upgraded));
    }

    /// <summary>The only verdict that costs nothing — no curse anywhere in it.</summary>
    [Fact]
    public void NobleGuiltyHealsTenAndAddsNoCurse()
    {
        var engine = OnPage(2);
        engine.State.PlayerHp = 30;
        int deck = engine.State.Deck.Count;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(40, engine.State.PlayerHp);
        Assert.Equal(deck, engine.State.Deck.Count);
    }

    [Fact]
    public void NobleInnocentIsARegretAndThreeHundredGold()
    {
        var engine = OnPage(2);
        int gold = engine.State.Gold;

        engine.Step(1, -1, out _, out _, out _);

        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Regret"));
        Assert.Equal(gold + 300, engine.State.Gold);
    }

    [Fact]
    public void NondescriptGuiltyIsADoubtAndTwoCardOffers()
    {
        var engine = OnPage(3);

        engine.Step(0, -1, out _, out _, out _);

        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Doubt"));
        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        Assert.Single(engine.State.PendingCardOffers);
        Assert.All(engine.State.RewardCards, id => Assert.NotEqual(0, id));
    }

    [Fact]
    public void NondescriptInnocentIsADoubtAndTwoRandomTransforms()
    {
        var engine = OnPage(3);
        var before = engine.State.Deck.Select(c => c.DefId).ToList();

        engine.Step(1, -1, out _, out _, out _);

        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Doubt"));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        for (int i = 0; i < 2; i++)
        {
            int index = Enumerable
                .Range(0, engine.State.Deck.Count)
                .First(j => RunNonCombatEffects.CanSelectCard(engine.State, j));
            engine.Step(index, -1, out _, out _, out _);
        }

        // Two of the originals are gone, replaced by whatever the roll gave.
        Assert.NotEqual(before, engine.State.Deck.Select(c => c.DefId).ToList());
    }
}
