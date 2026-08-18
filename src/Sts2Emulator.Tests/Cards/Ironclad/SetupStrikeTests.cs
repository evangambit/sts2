using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class SetupStrikeTests
{
    [Fact]
    public void AppliesTemporaryStrengthUntilEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.SetupStrike, false)];
        state.Energy = 1;
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

        Assert.Equal(93, state.Enemies[0].Hp);
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));
    }
}
