using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class FightMeTests
{
    [Fact]
    public void HitsTwiceAndAppliesStrengthToBothSides()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.FightMe, false)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(90, state.Enemies[0].Hp);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
    }
}
