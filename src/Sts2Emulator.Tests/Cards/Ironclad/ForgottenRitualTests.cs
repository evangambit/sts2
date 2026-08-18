using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ForgottenRitualTests
{
    [Fact]
    public void DoesNotGainEnergyWithoutPriorExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.ForgottenRitual, false)];
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

        Assert.Equal(0, state.Energy);
        Assert.Equal(1, state.CardsExhaustedThisTurn);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.ForgottenRitual);
    }

    [Fact]
    public void GainsEnergyAfterCardExhaustedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.ForgottenRitual, true)];
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
        CardEffects.ExhaustCard(state, new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(4, state.Energy);
        Assert.Equal(2, state.CardsExhaustedThisTurn);
    }
}
