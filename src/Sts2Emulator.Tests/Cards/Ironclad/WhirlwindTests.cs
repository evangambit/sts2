using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// X-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/Whirlwind.cs:
// HasEnergyCostX, DamageVar(5m) with hitCount = ResolveEnergyXValue() against all
// opponents; OnUpgrade raises the damage by 3.
public class WhirlwindTests
{
    [Fact]
    public void HitsEveryEnemyOncePerEnergySpent()
    {
        var fight = Fight.Hand(Card(IC.Whirlwind)).Energy(3).Enemy(hp: 40).Enemy(hp: 40);

        // 5 damage x 3 energy, to each enemy.
        fight.Play();

        Assert.Equal(25, fight.Enemy0.Hp);
        Assert.Equal(25, fight.Enemy1.Hp);
    }

    [Fact]
    public void SpendsAllRemainingEnergy()
    {
        var fight = Fight.Hand(Card(IC.Whirlwind)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(0, fight.State.Energy);
    }

    [Fact]
    public void UpgradedDealsEightPerHit()
    {
        var fight = Fight.Hand(Card(IC.Whirlwind, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithNoEnergy()
    {
        var fight = Fight.Hand(Card(IC.Whirlwind)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }
}
