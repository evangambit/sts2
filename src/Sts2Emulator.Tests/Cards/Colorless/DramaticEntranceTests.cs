using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class DramaticEntranceTests
{
    [Fact]
    public void DamagesAllEnemiesAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DramaticEntrance, false)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([19, 19], state.Enemies.Select(enemy => enemy.Hp));
        Assert.Empty(state.Hand);
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.DramaticEntrance);
    }

    [Fact]
    public void UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.DramaticEntrance, true)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([15, 15], state.Enemies.Select(enemy => enemy.Hp));
    }
}
