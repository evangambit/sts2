using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class OmnisliceTests
{
    [Fact]
    public void SplashesEffectiveFirstHitDamageToOtherEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Omnislice, false)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 5,
                MaxHp = 5,
                Buffs = [],
            },
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
                Block = 3,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([0, 22, 25], state.Enemies.Select(enemy => enemy.Hp));
        Assert.Empty(state.Hand);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Omnislice);
    }

    [Fact]
    public void UpgradedSplashIsUnpowered()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Omnislice, true)];
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
        ];
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 2);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal([31, 31], state.Enemies.Select(enemy => enemy.Hp));
    }
}
