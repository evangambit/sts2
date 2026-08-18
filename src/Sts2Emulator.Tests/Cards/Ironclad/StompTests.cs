using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class StompTests
{
    [Fact]
    public void DamagesAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stomp, false)];
        state.Energy = 3;
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

        Assert.Equal([18, 18], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stomp, true)];
        state.Energy = 3;
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

    [Fact]
    public void CostIsReducedByAttacksPlayedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.Stomp, false),
        ];
        state.Energy = 3;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, state.Energy);
        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(76, state.Enemies[0].Hp);
        Assert.Equal(88, state.Enemies[1].Hp);
    }
}
