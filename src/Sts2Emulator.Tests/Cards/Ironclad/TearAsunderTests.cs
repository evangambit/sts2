using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class TearAsunderTests
{
    [Fact]
    public void HitsOnceWithNoUnblockedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TearAsunder, false));
        state.Energy = 3;
        // UnblockedDamageHitCount = 0 → 1 hit

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 5, enemy.Hp); // 5 dmg × 1 hit
    }

    [Fact]
    public void HitsMoreWithUnblockedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.UnblockedDamageHitCount = 2;
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TearAsunder, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(hpBefore - 15, enemy.Hp); // 5 dmg × 3 hits
    }
}
