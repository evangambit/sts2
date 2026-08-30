using System.Linq;
using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Dredge.cs: `CardSelectCmd.FromCombatPile` over the
// DISCARD pile for `Math.Min(Cards(3), MaxCardsInHand - hand.Count)` — the player picks
// which come back, and a full hand shrinks the ask before the screen opens. Upgrading adds
// RETAIN and does not change the count.
//
// The emulator took the three oldest discards and read the upgrade as a different number
// of cards.
public class DredgeTests
{
    private const int Dredge = 154;
    private const int Strike = 473;
    private const int Defend = 132;

    private static Fight WithDiscard(int count, bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(Dredge, upgraded)).Energy(9).Enemy(hp: 200);
        for (int i = 0; i < count; i++)
        {
            fight.State.DiscardPile.Add(new CardInstance(i == 0 ? Defend : Strike, false));
        }

        return fight;
    }

    [Fact]
    public void ItAsksRatherThanTakingTheOldest()
    {
        var fight = WithDiscard(4);

        fight.Play();

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.DiscardToHand, fight.Pending!.Kind);
        Assert.Equal(4, fight.Pending.Candidates.Count);
    }

    [Fact]
    public void ThreePicksComeBackToHand()
    {
        var fight = WithDiscard(4);
        fight.Play();

        fight.Choose(3);
        fight.Choose(0);
        fight.Choose(0);

        Assert.Equal(3, fight.State.Hand.Count);
        Assert.Single(fight.State.DiscardPile);
        Assert.Null(fight.Pending);
    }

    /// <summary>The pick is the player's: the last discard can come back first.</summary>
    [Fact]
    public void ThePlayerChoosesWhichCard()
    {
        var fight = WithDiscard(4);
        fight.Play();

        fight.Choose(0);

        Assert.Equal(Defend, fight.State.Hand[0].DefId);
    }

    /// <summary>Nothing in the discard pile is no screen at all.</summary>
    [Fact]
    public void AnEmptyDiscardPileAsksNothing()
    {
        var fight = WithDiscard(0);

        fight.Play();

        Assert.Null(fight.Pending);
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = WithDiscard(0);

        fight.Play();

        Assert.Single(fight.State.ExhaustPile);
    }

    /// <summary>Upgrading buys RETAIN, not a fourth card.</summary>
    [Fact]
    public void UpgradingBuysRetain()
    {
        Assert.False(new CardInstance(Dredge, false).IsRetained());
        Assert.True(new CardInstance(Dredge, true).IsRetained());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Graveblast.cs: 4 damage upgrading by 2, then ONE card
// CHOSEN from the discard pile comes to hand. Upgrading also drops Exhaust. The emulator
// took the oldest discard.
public class GraveblastTests
{
    private const int Graveblast = 227;
    private const int Strike = 473;
    private const int Defend = 132;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(Graveblast, upgraded)).Energy(9).Enemy(hp: 200);
        fight.State.DiscardPile.Add(new CardInstance(Defend, false));
        fight.State.DiscardPile.Add(new CardInstance(Strike, false));
        fight.Play();
        return fight;
    }

    [Fact]
    public void ItHitsForFourAndSixUpgraded()
    {
        Assert.Equal(196, Played().Enemy0.Hp);
        Assert.Equal(194, Played(upgraded: true).Enemy0.Hp);
    }

    [Fact]
    public void ItAsksWhichCardComesBack()
    {
        var fight = Played();

        Assert.NotNull(fight.Pending);
        Assert.Equal(2, fight.Pending!.Candidates.Count);

        fight.Choose(1);

        Assert.Equal(Strike, fight.State.Hand[0].DefId);
        Assert.Null(fight.Pending);
    }

    /// <summary>One card, not a repeating screen.</summary>
    [Fact]
    public void ItTakesOneCardOnly()
    {
        var fight = Played();
        fight.Choose(0);

        Assert.Single(fight.State.Hand);
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void UpgradingDropsExhaust()
    {
        Assert.True(new CardInstance(Graveblast, false).IsExhaust());
        Assert.False(new CardInstance(Graveblast, true).IsExhaust());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DrainPower.cs: 10 damage upgrading by 2, then
// `discard.Where(IsUpgradable).TakeRandom(Cards(2), Rng.CombatCardSelection)` upgraded in
// place — CardsVar upgrades to 3. A SHUFFLE, not a walk from the front.
public class DrainPowerTests
{
    private const int DrainPower = 152;
    private const int Strike = 473;

    private static Fight Played(int discardCount, bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(DrainPower, upgraded)).Energy(9).Enemy(hp: 200).Seed(7);
        for (int i = 0; i < discardCount; i++)
        {
            fight.State.DiscardPile.Add(new CardInstance(Strike, false));
        }

        fight.Play();
        return fight;
    }

    [Fact]
    public void ItHitsForTenAndTwelveUpgraded()
    {
        Assert.Equal(190, Played(0).Enemy0.Hp);
        Assert.Equal(188, Played(0, upgraded: true).Enemy0.Hp);
    }

    [Fact]
    public void ItUpgradesTwoDiscardedCards()
    {
        var fight = Played(5);

        Assert.Equal(2, fight.State.DiscardPile.Count(c => c.Upgraded));
    }

    [Fact]
    public void UpgradedItUpgradesThree()
    {
        var fight = Played(5, upgraded: true);

        // Four upgraded cards in the pile: the three it picked, plus the upgraded Drain
        // Power itself, which lands there when the play finishes.
        Assert.Equal(4, fight.State.DiscardPile.Count(c => c.Upgraded));
    }

    /// <summary>Fewer candidates than picks upgrades what there is.</summary>
    [Fact]
    public void ItUpgradesWhatItCan()
    {
        var fight = Played(1);

        Assert.Equal(1, fight.State.DiscardPile.Count(c => c.Upgraded));
    }

    /// <summary>An already-upgraded pile is not a candidate list.</summary>
    [Fact]
    public void AnUpgradedPileIsLeftAlone()
    {
        var fight = Fight.Hand(new CardInstance(DrainPower, false)).Energy(9).Enemy(hp: 200);
        fight.State.DiscardPile.Add(new CardInstance(Strike, true));

        fight.Play();

        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.True(fight.State.DiscardPile[0].Upgraded);
        Assert.False(fight.State.DiscardPile[1].Upgraded);
    }
}
