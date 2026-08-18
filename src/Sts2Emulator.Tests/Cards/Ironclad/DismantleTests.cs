using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Dismantle.cs: DamageVar(8m) with
// hitCount = target.HasPower<VulnerablePower>() ? 2 : 1; OnUpgrade raises damage by 2.
public class DismantleTests
{
    [Fact]
    public void HitsOnceAgainstAnUnafflictedEnemy()
    {
        var fight = Fight.Hand(Card(IC.Dismantle)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(52, fight.Enemy0.Hp);
    }

    [Fact]
    public void HitsTwiceAgainstAVulnerableEnemy()
    {
        var fight = Fight
            .Hand(Card(IC.Dismantle))
            .Energy(1)
            .Enemy(hp: 60, buffs: [new BuffState(BuffId.Vulnerable, 1)]);

        // Two hits of 8, each raised 50% by Vulnerable.
        fight.Play();

        Assert.Equal(36, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsTen()
    {
        var fight = Fight.Hand(Card(IC.Dismantle, upgraded: true)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(50, fight.Enemy0.Hp);
    }
}
