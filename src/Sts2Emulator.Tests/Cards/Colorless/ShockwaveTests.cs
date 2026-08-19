using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill, CardKeyword.Exhaust, TargetType.AllEnemies.
// MegaCrit.Sts2.Core.Models.Cards/Shockwave.cs applies both WeakPower and
// VulnerablePower at DynamicVar("Power", 3m) to every hittable enemy; OnUpgrade raises
// that amount by 2.
public class ShockwaveTests
{
    [Fact]
    public void AppliesThreeWeakAndThreeVulnerableToEveryEnemy()
    {
        var fight = Fight.Hand(Card(CL.Shockwave)).Energy(2).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Weak, 1));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }

    [Fact]
    public void UpgradedAppliesFiveOfEach()
    {
        var fight = Fight.Hand(Card(CL.Shockwave, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(5, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void ExhaustsItselfAndDealsNoDamage()
    {
        var fight = Fight.Hand(Card(CL.Shockwave)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal([CL.Shockwave], Fight.Ids(fight.State.ExhaustPile));
    }
}
