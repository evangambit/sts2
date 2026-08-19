using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Taunt.cs: BlockVar(7m) and
// PowerVar<VulnerablePower>(1m) on the target; OnUpgrade raises BOTH by 1.
public class TauntTests
{
    [Fact]
    public void GainsSevenBlockAndAppliesOneVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Taunt)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradedGainsEightBlockAndAppliesTwoVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Taunt, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void DebuffsOnlyTheTargetedEnemy()
    {
        var fight = Fight.Hand(Card(IC.Taunt)).Energy(1).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }
}
