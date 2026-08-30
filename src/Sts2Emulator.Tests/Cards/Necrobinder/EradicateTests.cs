using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Eradicate.cs: `HasEnergyCostX`, Retain, DamageVar 11
// upgrading by 3, and `WithHitCount(ResolveEnergyXValue())` — one hit per energy spent, all
// at the same target.
//
// The emulator dealt a single hit and never spent the energy. A live capture of it killed
// a 90 HP elite outright, which is how the miss was noticed at all: at nine energy the
// game hits for 99 and the emulator hit for 11.
public class EradicateTests
{
    private const int Eradicate = 171;

    [Fact]
    public void ItHitsOncePerEnergySpent()
    {
        var fight = Fight.Hand().Energy(4).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Eradicate, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 44, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.Energy);
    }

    [Fact]
    public void TheUpgradeRaisesTheDamagePerHit()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Eradicate, true));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 42, fight.Enemy0.Hp);
    }

    /// <summary>Separate hits, so block is spent on each of them.</summary>
    [Fact]
    public void EachHitIsItsOwn()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 500);
        fight.Enemy0.Block = 11;
        fight.State.Hand.Add(new CardInstance(Eradicate, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.Enemy0.Block);
        Assert.Equal(500 - 22, fight.Enemy0.Hp);
    }

    /// <summary>Nothing to spend is nothing to deal.</summary>
    [Fact]
    public void AtZeroEnergyItDoesNothing()
    {
        var fight = Fight.Hand().Energy(0).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Eradicate, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
    }

    /// <summary>Strength is per HIT, as it is for every multi-hit attack.</summary>
    [Fact]
    public void StrengthCountsOnEveryHit()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 2);
        fight.State.Hand.Add(new CardInstance(Eradicate, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 39, fight.Enemy0.Hp);
    }
}
