using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Snap.cs: OstyDamageVar(7) upgrading by 3 and tagged
// OstyAttack, so it does nothing without a living Osty. Then
// `CardSelectCmd.FromHand(filter: c => !c.Keywords.Contains(Retain))` and
// `CardCmd.ApplyKeyword(chosen, Retain)` — a card in HAND is made to Retain.
//
// The emulator added a retaining SOUL to the discard pile: a different card, a different
// pile, and no choice offered at all.
public class SnapTests
{
    private const int Snap = 443;
    private const int Strike = 473;
    private const int Soul = 446;

    private static Fight WithOsty()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        CardEffects.SummonOsty(fight.State, 10);
        return fight;
    }

    [Fact]
    public void ItAsksWhichCardInHandToKeep()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Snap, false));

        fight.Play(1, target: 0);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.RetainForNextTurn, fight.Pending!.Kind);
        Assert.False(fight.Pending.Skippable);
    }

    [Fact]
    public void TheChosenCardRetains()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Snap, false));

        fight.Play(1, target: 0);
        fight.Choose(0);

        Assert.True(fight.State.Hand[0].RetainThisTurn);
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void ItPutsNoSoulAnywhere()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Snap, false));

        fight.Play(1, target: 0);

        Assert.DoesNotContain(fight.State.DiscardPile, card => card.DefId == Soul);
    }

    [Fact]
    public void ItAsksNothingWhenEveryCardInHandAlreadyRetains()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Strike, false) { RetainThisTurn = true });
        fight.State.Hand.Add(new CardInstance(Snap, false));

        fight.Play(1, target: 0);

        Assert.Null(fight.Pending);
    }

    /// <summary>`FromOsty`, so it is the pet attacking and the damage is the Osty var.</summary>
    [Fact]
    public void ItHitsForSevenAndTenUpgraded()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Snap, false));
        fight.Play(0, target: 0);
        Assert.Equal(193, fight.Enemy0.Hp);

        var upgraded = WithOsty();
        upgraded.State.Hand.Add(new CardInstance(Snap, true));
        upgraded.Play(0, target: 0);
        Assert.Equal(190, upgraded.Enemy0.Hp);
    }

    /// <summary>
    /// The missing-Osty check guards only the DAMAGE — the CardSelect sits outside the
    /// `if`, and Snap has no `IsPlayable` override to stop the play. So a petless Snap
    /// hits nobody and still hands out Retain.
    /// </summary>
    [Fact]
    public void WithNoOstyItStillAsks()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.State.Hand.Add(new CardInstance(Snap, false));

        fight.Play(1, target: 0);

        Assert.Equal(200, fight.Enemy0.Hp);
        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.RetainForNextTurn, fight.Pending!.Kind);
    }
}
