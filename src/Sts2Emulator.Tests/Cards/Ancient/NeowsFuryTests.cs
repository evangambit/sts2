using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, Exhaust. MegaCrit.Sts2.Core.Models.Cards/NeowsFury.cs: DamageVar(10m)
// then `CardSelectCmd.FromCombatPile(discard, prefs(prompt, 0, num))` where num is
// CardsVar(2) capped by the room left in hand. OnUpgrade raises both by 4 and 1.
//
// The player CHOOSES, and the minimum is zero so the screen can be declined. These tests
// used to assert that the first two cards off the top of the discard pile came back,
// which is what the emulator did.
public class NeowsFuryTests
{
    private static Fight WithDiscard() =>
        Fight
            .Hand(Card(AN.NeowsFury))
            .Energy(1)
            .Discard(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(hp: 40);

    [Fact]
    public void DealsTenAndThenAsks()
    {
        var fight = WithDiscard();

        fight.Play();

        Assert.Equal(30, fight.Enemy0.Hp);
        Assert.NotNull(fight.State.PendingSelection);
        Assert.Equal(CardSelectionKind.DiscardToHand, fight.State.PendingSelection!.Kind);
        Assert.Equal(3, fight.State.PendingSelection.Candidates.Count);
    }

    /// <summary>The chosen cards come back — including ones the old reading could not reach.</summary>
    [Fact]
    public void TheChosenCardsAreTheOnesRecovered()
    {
        var fight = WithDiscard();
        fight.Play();

        // Bash is LAST in the discard pile; taking the first two by index never reached it.
        fight.Choose(2);
        Assert.Contains(fight.State.Hand, c => c.DefId == IC.Bash);

        int last = fight.State.PendingSelection!.Candidates.Count;
        CombatEngine.Step(fight.State, last, new Random(0));

        Assert.Null(fight.State.PendingSelection);
        Assert.Single(fight.State.Hand, c => c.DefId == IC.Bash);
        Assert.Equal(2, fight.State.DiscardPile.Count);
    }

    [Fact]
    public void ItTakesAtMostTwoAndThreeUpgraded()
    {
        var plain = WithDiscard();
        plain.Play();
        Assert.Equal(2, plain.State.PendingSelection!.Amount);

        var upgraded = Fight
            .Hand(Card(AN.NeowsFury, upgraded: true))
            .Energy(1)
            .Discard(Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(hp: 40);
        upgraded.Play();
        Assert.Equal(3, upgraded.State.PendingSelection!.Amount);
    }

    /// <summary>The minimum is zero, so declining is legal.</summary>
    [Fact]
    public void TheScreenCanBeDeclined()
    {
        var fight = WithDiscard();
        fight.Play();

        Assert.True(fight.State.PendingSelection!.Skippable);
        int skip = fight.State.PendingSelection.Candidates.Count;
        CombatEngine.Step(fight.State, skip, new Random(0));

        Assert.Null(fight.State.PendingSelection);
        Assert.Equal(3, fight.State.DiscardPile.Count);
    }

    /// <summary>
    /// `num` is `MaxCardsInHand - hand.Count`, not the flat 2 — and it is measured at
    /// OnPlay, when the card itself has already left the hand. A hand that was full before
    /// the play therefore has room for exactly one, which is what both sides compute.
    /// </summary>
    [Fact]
    public void AFullHandAsksForOnlyWhatFits()
    {
        var fight = WithDiscard();
        while (fight.State.Hand.Count < CardEffects.MaxCardsInHand)
        {
            fight.State.Hand.Add(Card(IC.StrikeIronclad));
        }

        fight.Play(0);

        Assert.NotNull(fight.State.PendingSelection);
        Assert.Equal(1, fight.State.PendingSelection!.Amount);
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = WithDiscard();

        fight.Play();
        int skip = fight.State.PendingSelection!.Candidates.Count;
        CombatEngine.Step(fight.State, skip, new Random(0));

        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == AN.NeowsFury);
    }
}
