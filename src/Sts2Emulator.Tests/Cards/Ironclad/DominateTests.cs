using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Dominate.cs applies
// PowerVar<VulnerablePower>(1m) to the target, then Strength to the player equal to the
// target's Vulnerable AFTER that application; OnUpgrade raises the Vulnerable to 2.
public class DominateTests
{
    [Fact]
    public void AppliesOneVulnerableAndOneStrength()
    {
        var fight = Fight.Hand(Card(IC.Dominate)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void UpgradedAppliesTwoVulnerableAndTwoStrength()
    {
        var fight = Fight.Hand(Card(IC.Dominate, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void TheStrengthCountsVulnerableTheEnemyAlreadyHad()
    {
        var fight = Fight
            .Hand(Card(IC.Dominate))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 2)]);

        // 2 already there plus the 1 this applies.
        fight.Play();

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Strength));
    }
}
