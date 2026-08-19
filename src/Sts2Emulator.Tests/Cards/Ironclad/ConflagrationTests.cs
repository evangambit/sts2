using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/
// Conflagration.cs: DamageVar(2m) with RepeatVar(4) against all opponents; OnUpgrade
// raises the repeat by 1, not the damage.
public class ConflagrationTests
{
    [Fact]
    public void HitsEveryEnemyFourTimesForTwo()
    {
        var fight = Fight.Hand(Card(IC.Conflagration)).Energy(1).Enemy(hp: 40).Enemy(hp: 30);

        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
        Assert.Equal(22, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedHitsAFifthTime()
    {
        var fight = Fight.Hand(Card(IC.Conflagration, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(30, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachHitIsRaisedSeparatelyByVulnerable()
    {
        var fight = Fight
            .Hand(Card(IC.Conflagration))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)]);

        // Four hits of 2, each multiplied by 1.5 and truncated.
        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }
}
