using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class BreakthroughTests
{
    [Fact]
    public void LosesHpAndDamagesAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, false)];
        state.Energy = 1;
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

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([21, 21], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, true)];
        state.Energy = 1;
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

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([17, 17], state.Enemies.Select(enemy => enemy.Hp));
    }
}
