using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class VolleyTests
{
    [Fact]
    public void SpendsAllEnergyForRepeatedRandomEnemyHits()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Volley, false)];
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.Energy);
        Assert.Equal(10, state.Enemies[0].Hp);
    }

    [Fact]
    public void UpgradedUsesUpgradedDamagePerEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Volley, true)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(12, state.Enemies[0].Hp);
    }
}
