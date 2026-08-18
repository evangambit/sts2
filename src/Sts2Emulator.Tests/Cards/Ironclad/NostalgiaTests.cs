using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class NostalgiaTests
{
    [Fact]
    public void PutsFirstAttackOrSkillEachTurnOnTopOfDrawPile()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Nostalgia, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile = [new CardInstance(IC.Bash, false)];
        state.DiscardPile.Clear();
        state.Energy = 3;
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
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Nostalgia));
        Assert.Equal(IC.StrikeIronclad, state.DrawPile[0].DefId);
        Assert.DoesNotContain(state.DiscardPile, card => card.DefId == IC.StrikeIronclad);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.DefendIronclad);
    }

    [Fact]
    public void UpgradedCostsZeroAndResetsEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Nostalgia, true)];
        state.DrawPile = [];
        state.DiscardPile.Clear();
        state.Energy = 0;
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

        Assert.Contains(0, CombatEngine.ValidActions(state));
        CombatEngine.Step(state, 0, new Random(0));
        state.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        state.Energy = 1;
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        state.Hand = [new CardInstance(IC.DefendIronclad, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(IC.DefendIronclad, state.DrawPile[0].DefId);
    }
}
