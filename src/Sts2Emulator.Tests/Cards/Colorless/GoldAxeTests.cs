using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/GoldAxe.cs: CalculationBaseVar(0m) plus
// ExtraDamageVar(1m) per card play finished this combat; OnUpgrade adds CardKeyword.Retain
// rather than damage.
public class GoldAxeTests
{
    [Fact]
    public void DealsNothingAsTheFirstCardPlayed()
    {
        var fight = Fight.Hand(Card(CL.GoldAxe)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }

    [Fact]
    public void GainsOneDamagePerCardAlreadyPlayed()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(CL.GoldAxe))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);
        fight.Play(index: 0);

        // Two Strikes for 6 each, then Gold Axe for the two plays behind it.
        fight.Play(index: 0);

        Assert.Equal(46, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradeRetainsRatherThanHittingHarder()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad), Card(CL.GoldAxe, upgraded: true))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        fight.Play(index: 0);

        // 6 from the Strike, then 1 for that single prior play.
        Assert.Equal(53, fight.Enemy0.Hp);
    }
}
