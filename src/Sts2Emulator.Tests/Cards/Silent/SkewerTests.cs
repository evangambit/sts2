using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// X-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Skewer.cs: HasEnergyCostX, DamageVar(8m)
// with hitCount = ResolveEnergyXValue(); OnUpgrade raises the damage by 3.
public class SkewerTests
{
    [Fact]
    public void HitsOncePerEnergySpent()
    {
        var fight = Fight.Hand(Card(SI.Skewer)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(36, fight.Enemy0.Hp);
    }

    [Fact]
    public void SpendsAllRemainingEnergy()
    {
        var fight = Fight.Hand(Card(SI.Skewer)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(0, fight.State.Energy);
    }

    [Fact]
    public void UpgradedHitsForEleven()
    {
        var fight = Fight.Hand(Card(SI.Skewer, upgraded: true)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(38, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithNoEnergy()
    {
        var fight = Fight.Hand(Card(SI.Skewer)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }
}
