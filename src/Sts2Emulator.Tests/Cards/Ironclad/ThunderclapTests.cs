using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/Thunderclap.cs:
// DamageVar(4m) and PowerVar<VulnerablePower>(1m), applied to CombatState.HittableEnemies.
// OnUpgrade raises damage by 3 only — the Vulnerable stays at 1.
public class ThunderclapTests
{
    [Fact]
    public void DealsFourToEveryEnemyAndAppliesVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Thunderclap)).Energy(1).Enemy(hp: 40).Enemy(hp: 30);

        fight.Play();

        Assert.Equal(36, fight.Enemy0.Hp);
        Assert.Equal(26, fight.Enemy1.Hp);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }

    [Fact]
    public void UpgradedDealsSeven()
    {
        var fight = Fight
            .Hand(Card(IC.Thunderclap, upgraded: true))
            .Energy(1)
            .Enemy(hp: 40)
            .Enemy(hp: 30);

        fight.Play();

        Assert.Equal(33, fight.Enemy0.Hp);
        Assert.Equal(23, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradeDoesNotRaiseTheVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Thunderclap, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void VulnerableStacksOnTopOfWhatTheEnemyAlreadyHas()
    {
        var fight = Fight
            .Hand(Card(IC.Thunderclap))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 2)]);

        // 4 damage, raised 50% by the Vulnerable already on the target.
        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }
}
