using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Stars are the Regent's SECOND resource, and the emulator had no notion of a star COST
/// at all — it tracked the counter and let every card be played for free.
/// </summary>
/// <remarks>
/// `CardModel.CanonicalStarCost` defaults to -1 and twenty-one Regent cards override it.
/// `PlayerCombatState.HasEnoughResourcesFor` refuses a play whose star cost exceeds what
/// the player holds, and `CardModel.SpendResources` takes the stars AFTER the energy.
/// Nothing upgrades a star cost — `UpgradeStarCostBy` has no callers — so the printed
/// number is the whole story.
///
/// The excess-energy-for-stars conversion in those two functions is behind
/// `Hook.ShouldPayExcessEnergyCostWithStars`, which is a virtual on AbstractModel that
/// nothing in the game overrides, so there is no conversion to model.
/// </remarks>
public class StarCostTests
{
    private const int FallingStar = 179; // 0 energy, 2 stars
    private const int Stardust = 463; // 0 energy, X stars
    private const int StrikeRegent = 474; // no star cost

    private static Fight WithStars(int stars)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = stars;
        return fight;
    }

    [Fact]
    public void PlayingACardSpendsItsStars()
    {
        var fight = WithStars(3);
        fight.State.Hand.Add(new CardInstance(FallingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(1, fight.State.Stars);
    }

    [Fact]
    public void ACardWithNoStarCostSpendsNone()
    {
        var fight = WithStars(3);
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));

        fight.Play(0, target: 0);

        Assert.Equal(3, fight.State.Stars);
    }

    /// <summary>`HasEnoughResourcesFor` refuses it — the card is not played at all.</summary>
    [Fact]
    public void TooFewStarsRefusesThePlay()
    {
        var fight = WithStars(1);
        fight.State.Hand.Add(new CardInstance(FallingStar, false));

        fight.Play(0, target: 0);

        // Asserted on the BOARD rather than on the StepResult: an invalid step is the
        // default result, which any quiet play also returns.
        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.Single(fight.State.Hand);
        Assert.Equal(1, fight.State.Stars);
    }

    [Fact]
    public void ExactlyEnoughIsEnough()
    {
        var fight = WithStars(2);
        fight.State.Hand.Add(new CardInstance(FallingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.Stars);
        Assert.True(fight.Enemy0.Hp < 500);
    }

    /// <summary>Stardust is the one card with `HasStarCostX`: it spends every star.</summary>
    [Fact]
    public void AnXStarCardSpendsThemAll()
    {
        var fight = WithStars(4);
        fight.State.Hand.Add(new CardInstance(Stardust, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.Stars);
    }

    /// <summary>And at zero stars it is still playable, because zero is what it costs.</summary>
    [Fact]
    public void AnXStarCardIsPlayableAtZero()
    {
        var fight = WithStars(0);
        fight.State.Hand.Add(new CardInstance(Stardust, false));

        fight.Play(0, target: 0);

        Assert.Empty(fight.State.Hand);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Venerate.cs: `PlayerCmd.GainStars(StarsVar 2)`,
// upgrading by 1. That is the whole card.
//
// The emulator granted Strength AND Dexterity, and a live capture of it passed clean —
// the generator asserted only the powers the game DID report, so a power the emulator
// invents was invisible. `Fight.PlayerPowersAre` exists because of this card.
public class VenerateTests
{
    private const int Venerate = 532;

    [Fact]
    public void ItGainsTwoStars()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Venerate, false));

        fight.Play(0);

        Assert.Equal(2, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeGainsThree()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Venerate, true));

        fight.Play(0);

        Assert.Equal(3, fight.State.Stars);
    }

    [Fact]
    public void ItGrantsNoStrengthOrDexterity()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Venerate, false));

        fight.Play(0);

        fight.PlayerPowersAre();
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Eidolon.cs: exhausts the whole hand, and applies
// Intangible 1 ONLY if it exhausted nine or more — `if (exhaustedCount >= 9)`. The
// threshold is why the card carries a `ShouldGlowGold` that watches the hand size.
//
// The emulator granted Intangible for any hand at all, which at a normal five cards is a
// free turn of taking 1 from everything.
public class EidolonTests
{
    private const int Eidolon = 161;
    private const int Defend = 133;

    private static Fight WithHandOf(int count)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        for (int i = 0; i < count; i++)
        {
            fight.State.Hand.Add(new CardInstance(Defend, false));
        }

        fight.State.Hand.Add(new CardInstance(Eidolon, false));
        return fight;
    }

    [Fact]
    public void ASmallHandGetsNoIntangible()
    {
        var fight = WithHandOf(5);

        fight.Play(fight.State.Hand.Count - 1);

        Assert.Empty(fight.State.Hand);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Intangible));
    }

    [Fact]
    public void NineExhaustedEarnsIt()
    {
        var fight = WithHandOf(9);

        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Intangible));
    }

    /// <summary>Eight is not nine — the card played is not one of the exhausted.</summary>
    [Fact]
    public void EightIsNotEnough()
    {
        var fight = WithHandOf(8);

        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Intangible));
    }

    /// <summary>The hand goes; Eidolon itself is not Exhaust, so it discards.</summary>
    [Fact]
    public void ItExhaustsTheWholeHandEitherWay()
    {
        var fight = WithHandOf(5);

        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(5, fight.State.ExhaustPile.Count);
        Assert.Single(fight.State.DiscardPile);
    }
}
