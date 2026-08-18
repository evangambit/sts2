using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class IronWaveTests
{
    [Fact]
    public void GainsBlockBeforeDealingDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.IronWave, false)];
        state.Energy = 1;
        state.PlayerBuffs = [new BuffState(BuffId.Juggernaut, 5)];
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                Block = 5,
                Buffs = [new BuffState(BuffId.Vulnerable, 1)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Equal(23, state.Enemies[0].Hp);
        Assert.Equal(0, state.Enemies[0].Block);
    }

    [Fact]
    public void UpgradedUsesUpgradedBlockAndDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.IronWave, true)];
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
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(7, state.PlayerBlock);
        Assert.Equal(23, state.Enemies[0].Hp);
    }
}
