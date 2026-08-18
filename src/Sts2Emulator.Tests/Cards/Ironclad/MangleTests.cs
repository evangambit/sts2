using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class MangleTests
{
    [Fact]
    public void DamagesAndAppliesTempStrength()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Mangle, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.True(enemy.Hp < hpBefore);
        Assert.Equal(-10, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
        Assert.Equal(10, BuffSystem.Get(enemy.Buffs, BuffId.TemporaryStrength));
    }
}
