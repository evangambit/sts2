using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/
// HowlFromBeyond.cs: DamageVar(16m) to all opponents; OnUpgrade raises it by 5.
//
// The card also overrides AfterAutoPostPlayPhaseEntered to auto-play itself from the
// exhaust pile, which the emulator does not model.
public class HowlFromBeyondTests
{
    [Fact]
    public void DealsSixteenToEveryEnemy()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Enemy(hp: 40).Enemy(hp: 30);

        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
        Assert.Equal(14, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedDealsTwentyOne()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond, upgraded: true))
            .Energy(3)
            .Enemy(hp: 40)
            .Enemy(hp: 30);

        fight.Play();

        Assert.Equal(19, fight.Enemy0.Hp);
        Assert.Equal(9, fight.Enemy1.Hp);
    }

    [Fact]
    public void EachEnemysOwnVulnerableRaisesItsShare()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)])
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.Enemy0.Hp);
        Assert.Equal(24, fight.Enemy1.Hp);
    }
}
