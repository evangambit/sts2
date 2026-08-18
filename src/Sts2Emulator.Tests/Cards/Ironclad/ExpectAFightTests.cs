using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ExpectAFightTests
{
    [Fact]
    public void GainsEnergyForAttacksInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.ExpectAFight, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.SwordBoomerang, false),
        ];
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

        Assert.Equal(2, state.Energy);
    }
}
