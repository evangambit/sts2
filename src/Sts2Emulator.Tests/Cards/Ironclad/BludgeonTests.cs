using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class BludgeonTests
{
    [Fact]
    public void Deals32Damage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100;
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Bludgeon, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 32, enemy.Hp);
    }
}
