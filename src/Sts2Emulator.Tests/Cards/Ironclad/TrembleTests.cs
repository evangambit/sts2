using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Tremble.cs:
// PowerVar<VulnerablePower>(3m) on the target; OnUpgrade raises it by 1.
public class TrembleTests
{
    [Fact]
    public void AppliesThreeVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Tremble)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradedAppliesFour()
    {
        var fight = Fight.Hand(Card(IC.Tremble, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void ExhaustsItselfAndDealsNoDamage()
    {
        var fight = Fight.Hand(Card(IC.Tremble)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == IC.Tremble);
        Assert.DoesNotContain(fight.State.DiscardPile, card => card.DefId == IC.Tremble);
    }

    [Fact]
    public void HitsOnlyTheTargetedEnemy()
    {
        var fight = Fight.Hand(Card(IC.Tremble)).Energy(1).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }
}
