using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class JugglingTests
{
    [Fact]
    public void CopiesThirdAttackPlayedEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Juggling, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 4;
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
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling));
        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }
}
