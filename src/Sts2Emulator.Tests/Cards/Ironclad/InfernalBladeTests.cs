using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class InfernalBladeTests
{
    [Fact]
    public void AddsRandomAttackFreeThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
        Assert.True(state.Hand[0].FreeThisTurn);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.InfernalBlade);
    }

    [Fact]
    public void GeneratedAttackCanBePlayedWithoutEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(41, state.Enemies[0].Hp);
        // AshenStrike mentions Exhaust only in a hover tip; it declares no Exhaust
        // keyword and its OnPlay just deals damage, so it discards.
        Assert.Contains(
            state.DiscardPile,
            card => card.DefId == IC.AshenStrike && !card.FreeThisTurn
        );
    }

    [Fact]
    public void UpgradedCostsZero()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, true)];
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
    }
}
