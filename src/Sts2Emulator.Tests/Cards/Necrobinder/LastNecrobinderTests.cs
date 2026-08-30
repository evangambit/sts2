using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// The four Necrobinder cards no capture can reach: each raises a card-selection screen, and
// the capture tool has no way to answer one, so the play never settles and the fixture is
// never written. Read from source instead.

// MegaCrit.Sts2.Core.Models.Cards/CaptureSpirit.cs: a DamageVar of 3 marked
// `Unblockable | Unpowered | Move` aimed at `cardPlay.Target`, plus CardsVar 3 Souls. Both
// vars upgrade by 1. The emulator took the damage out of the PLAYER: right number, wrong
// creature.
public class CaptureSpiritTests
{
    private const int CaptureSpirit = 79;
    private const int Soul = 446;

    [Fact]
    public void ItHitsTheEnemyAndNotThePlayer()
    {
        var fight = Fight.Hand().Energy(9).PlayerHp(50, 80).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(CaptureSpirit, false));

        fight.Play(0, target: 0);

        Assert.Equal(497, fight.Enemy0.Hp);
        Assert.Equal(50, fight.State.PlayerHp);
    }

    [Fact]
    public void TheUpgradeHitsForFourAndMakesFourSouls()
    {
        var fight = Fight.Hand().Energy(9).PlayerHp(50, 80).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(CaptureSpirit, true));

        fight.Play(0, target: 0);

        Assert.Equal(496, fight.Enemy0.Hp);
        Assert.Equal(4, fight.State.DrawPile.Count(c => c.DefId == Soul));
    }

    /// <summary>Unblockable: a shield does not stop it.</summary>
    [Fact]
    public void BlockDoesNotStopIt()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.Enemy0.Block = 100;
        fight.State.Hand.Add(new CardInstance(CaptureSpirit, false));

        fight.Play(0, target: 0);

        Assert.Equal(497, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Cleanse.cs: summon 3, upgrading by 2, then EXHAUST a card
// CHOSEN from the draw pile. The emulator exhausted the top one.
public class CleanseTests
{
    private const int Cleanse = 90;
    private const int Strike = 473;
    private const int Defend = 132;

    private static Fight WithDrawPile() =>
        Fight
            .Hand()
            .Energy(9)
            .Draw(new CardInstance(Strike, false), new CardInstance(Defend, false))
            .Enemy(hp: 500);

    [Fact]
    public void ItSummonsAndAsksWhichCardToExhaust()
    {
        var fight = WithDrawPile();
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Cleanse, false));

        fight.Play(0);

        Assert.Equal(4, fight.State.OstyMaxHp);
        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.ExhaustFromDrawPile, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardIsTheOneExhausted()
    {
        var fight = WithDrawPile();
        fight.State.Hand.Add(new CardInstance(Cleanse, false));

        fight.Play(0);
        fight.Choose(1);

        Assert.Equal(Defend, fight.State.ExhaustPile.Single().DefId);
        Assert.Equal(Strike, fight.State.DrawPile.Single().DefId);
    }

    [Fact]
    public void TheUpgradeSummonsForFive()
    {
        var fight = WithDrawPile();
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Cleanse, true));

        fight.Play(0);

        Assert.Equal(6, fight.State.OstyMaxHp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SculptingStrike.cs: 9 damage upgrading by 3, then a card
// CHOSEN from hand — filtered to those not already Ethereal — gains ETHEREAL. The emulator
// gave the leftmost card RETAIN: a different keyword on a card nobody picked.
public class SculptingStrikeTests
{
    private const int SculptingStrike = 412;
    private const int Strike = 473;
    private const int Defile = 135; // already Ethereal

    [Fact]
    public void ItHitsAndAsksWhichCardToMakeEthereal()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(SculptingStrike, false));

        fight.Play(1, target: 0);

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(CardSelectionKind.GrantEtherealInHand, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardBecomesEthereal()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(SculptingStrike, false));
        fight.Play(1, target: 0);

        fight.Choose(0);

        Assert.True(fight.State.Hand[0].IsEthereal());
        Assert.False(fight.State.Hand[0].Retain);
    }

    /// <summary>The filter drops cards that already have it, so it never asks for nothing.</summary>
    [Fact]
    public void ACardThatIsAlreadyEtherealIsNotOffered()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Defile, false));
        fight.State.Hand.Add(new CardInstance(SculptingStrike, false));

        fight.Play(1, target: 0);

        Assert.Null(fight.Pending);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Transfigure.cs: a card CHOSEN from hand gains a REPLAY
// (`BaseReplayCount++`) and, unless it costs X or its cost is negative, costs one more for
// the combat. Its upgrade only drops Exhaust.
//
// The emulator transformed a card at random into a different card and handed out a point of
// energy — neither of which this card does.
public class TransfigureTests
{
    private const int Transfigure = 514;
    private const int Strike = 473;
    private const int AscendersBane = 10001; // unplayable, cost -1

    [Fact]
    public void ItAsksWhichCardToTransfigure()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Transfigure, false));

        fight.Play(1);

        Assert.Equal(CardSelectionKind.TransfigureInHand, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardReplaysAndCostsOneMore()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Transfigure, false));
        fight.Play(1);

        fight.Choose(0);

        Assert.Equal(1, fight.State.Hand[0].ReplayCount);
        Assert.Equal(2, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));
    }

    /// <summary>The replay is real: the card plays twice.</summary>
    [Fact]
    public void TheTransfiguredCardPlaysTwice()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(Strike, false));
        control.Play(0, target: 0);
        int once = 500 - control.Enemy0.Hp;

        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Transfigure, false));
        fight.Play(1);
        fight.Choose(0);
        fight.Play(0, target: 0);

        Assert.Equal(500 - once * 2, fight.Enemy0.Hp);
    }

    /// <summary>A cost below zero is left alone — an unplayable is not made to cost 0.</summary>
    [Fact]
    public void ANegativeCostIsNotRaised()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(AscendersBane, false));
        fight.State.Hand.Add(new CardInstance(Transfigure, false));
        fight.Play(1);

        fight.Choose(0);

        Assert.Equal(0, fight.State.Hand[0].CostBump);
        Assert.Equal(1, fight.State.Hand[0].ReplayCount);
    }
}
