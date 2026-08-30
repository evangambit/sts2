using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/PullFromBelow.cs: DamageVar(5) upgrading by 2, and a
// CalculatedVar whose multiplier counts `CardPlayFinishedEntry`s with `WasEthereal` for
// this owner — over the whole COMBAT history. CalculationBase is 0 and CalculationExtra
// is 1, so the hit count IS that number, with no floor.
//
// The emulator counted `EtherealExhaustCount` — cards exhausted BY Ethereal at end of
// turn, a per-turn counter — and floored it at one. Played first in a combat the live
// game dealt nothing at all and the emulator hit for 5.
public class PullFromBelowTests
{
    private const int PullFromBelow = 371;
    private const int Defile = 135; // Ethereal Attack
    private const int Poke = 357; // not Ethereal

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 200);

    [Fact]
    public void WithNoEtherealPlayedItDealsNothing()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(PullFromBelow, false));

        fight.Play(0, target: 0);

        Assert.Equal(200, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItHitsOncePerEtherealCardPlayed()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Defile, false));
        fight.State.Hand.Add(new CardInstance(Defile, false));
        fight.Play(0, target: 0);
        fight.Play(0, target: 0);
        int afterDefiles = fight.Enemy0.Hp;

        fight.State.Hand.Add(new CardInstance(PullFromBelow, false));
        fight.Play(0, target: 0);

        Assert.Equal(afterDefiles - 10, fight.Enemy0.Hp);
    }

    /// <summary>Non-Ethereal plays do not feed it.</summary>
    [Fact]
    public void OrdinaryCardsDoNotCount()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Poke, false));
        fight.Play(0, target: 0);
        int afterPoke = fight.Enemy0.Hp;

        fight.State.Hand.Add(new CardInstance(PullFromBelow, false));
        fight.Play(0, target: 0);

        Assert.Equal(afterPoke, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The entry is written when the play FINISHES, so an Ethereal Pull From Below would
    /// not count itself — and neither does this one count its own play.
    /// </summary>
    [Fact]
    public void ItDoesNotCountItsOwnPlay()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Defile, false));
        fight.Play(0, target: 0);
        int afterDefile = fight.Enemy0.Hp;

        fight.State.Hand.Add(new CardInstance(PullFromBelow, false));
        fight.Play(0, target: 0);

        Assert.Equal(afterDefile - 5, fight.Enemy0.Hp);
    }

    /// <summary>The count is per COMBAT, not per turn — it survives the turn boundary.</summary>
    [Fact]
    public void TheCountSurvivesTheTurn()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Defile, false));
        fight.Play(0, target: 0);
        int afterDefile = fight.Enemy0.Hp;
        fight.EndTurn();

        fight.State.Hand.Add(new CardInstance(PullFromBelow, false));
        fight.State.Energy = 9;
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(afterDefile - 5, fight.Enemy0.Hp);
    }
}
