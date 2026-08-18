using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class BreakTests
{
    [Fact]
    public void DealsBaseAndAppliesVulnerable5()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Break, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.True(enemy.Hp < hpBefore); // took damage
        Assert.Equal(5, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void Upgraded_AppliesVulnerable7()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Break, true));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(7, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }
}
