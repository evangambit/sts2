using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust and Retain.
// MegaCrit.Sts2.Core.Models.Cards/Purity.cs exhausts up to CardsVar(3) cards chosen from
// hand; OnUpgrade raises the limit by 2.
//
// A multi-card choice is modelled as the screen reopening: each pick resolves and the
// next opens, until the picks are spent or the hand empties.
public class PurityTests
{
    [Fact]
    public void AsksForTheFirstOfThreePicks()
    {
        var fight = Fight
            .Hand(Card(CL.Purity), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(CardSelectionKind.ExhaustFromHandRepeated, fight.Pending?.Kind);
        Assert.Equal(3, fight.Pending?.Amount);
        Assert.Equal(3, fight.Pending?.Candidates.Count);
    }

    [Fact]
    public void ExhaustsEachChosenCardAndReopensUntilThePicksAreSpent()
    {
        var fight = Fight
            .Hand(Card(CL.Purity), Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Energy(1)
            .Enemy(hp: 40);
        fight.Play();

        fight.Choose(1); // the Strike
        Assert.Equal(2, fight.Pending?.Amount);
        fight.Choose(1); // the Anger, now that the Strike is gone
        Assert.Equal(1, fight.Pending?.Amount);
        fight.Choose(0); // the Bash

        Assert.Null(fight.Pending);
        Assert.Empty(fight.State.Hand);
        // Purity exhausts itself as it finishes being played, before any pick resolves.
        Assert.Equal(
            [CL.Purity, IC.StrikeIronclad, IC.Anger, IC.Bash],
            Fight.Ids(fight.State.ExhaustPile)
        );
    }

    [Fact]
    public void StopsAskingOnceTheHandIsEmpty()
    {
        var fight = Fight.Hand(Card(CL.Purity), Card(IC.Bash)).Energy(1).Enemy(hp: 40);
        fight.Play();

        fight.Choose(0);

        Assert.Null(fight.Pending);
        Assert.Equal([CL.Purity, IC.Bash], Fight.Ids(fight.State.ExhaustPile));
    }

    [Fact]
    public void UpgradedOffersFivePicks()
    {
        var fight = Fight
            .Hand(Card(CL.Purity, upgraded: true), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.Pending?.Amount);
    }

    [Fact]
    public void AsksNothingWithAnEmptyHand()
    {
        var fight = Fight.Hand(Card(CL.Purity)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Null(fight.Pending);
    }

    /// <summary>
    /// `CardSelectorPrefs(prompt, 0, Cards)` — the MINIMUM is zero, so declining is a legal
    /// answer. The screen used to demand all three, which on a card whose whole use is
    /// trimming exactly what you want gone is close to the opposite of the effect.
    /// </summary>
    [Fact]
    public void TheScreenCanBeSkipped()
    {
        var fight = Fight
            .Hand(
                Card(CL.Purity),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Bludgeon)
            )
            .Energy(3);

        fight.Play();

        Assert.NotNull(fight.State.PendingSelection);
        Assert.True(fight.State.PendingSelection!.Skippable);
    }

    /// <summary>Skipping exhausts nothing and closes the screen.</summary>
    [Fact]
    public void SkippingExhaustsNothing()
    {
        var fight = Fight
            .Hand(
                Card(CL.Purity),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Bludgeon)
            )
            .Energy(3);
        fight.Play();

        // The skip is the action one past the last candidate.
        int skip = fight.State.PendingSelection!.Candidates.Count;
        CombatEngine.Step(fight.State, skip, new Random(0));

        Assert.Null(fight.State.PendingSelection);
        // Purity itself is Exhaust, so it is in there -- and nothing else is.
        Assert.Single(fight.State.ExhaustPile, c => c.DefId == CL.Purity);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Equal(3, fight.State.Hand.Count);
    }

    /// <summary>And stopping after ONE is legal too, which is the usual play.</summary>
    [Fact]
    public void ItCanStopAfterOne()
    {
        var fight = Fight
            .Hand(
                Card(CL.Purity),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Bludgeon)
            )
            .Energy(3);
        fight.Play();

        fight.Choose(0);
        Assert.NotNull(fight.State.PendingSelection);

        // The candidate list shrinks with each pick, so the skip action moves with it.
        int skip = fight.State.PendingSelection!.Candidates.Count;
        CombatEngine.Step(fight.State, skip, new Random(0));

        Assert.Null(fight.State.PendingSelection);
        // Purity plus the one card that was chosen before stopping.
        Assert.Equal(2, fight.State.ExhaustPile.Count);
        Assert.Equal(2, fight.State.Hand.Count);
    }
}
