using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ProlongTests
{
    [Fact]
    public void GainsCurrentBlockAfterNextTurnBlockClear()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 12;
        state.PlayerBuffs = [new BuffState(BuffId.Dexterity, 3)];
        state.Hand = [new CardInstance(CL.Prolong, false)];
        state.DrawPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(12, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
        Assert.Contains(state.ExhaustPile, card => card.DefId == CL.Prolong);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(12, state.PlayerBlock);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
    }

    [Fact]
    public void UpgradedDoesNotExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 4;
        state.Hand = [new CardInstance(CL.Prolong, true)];
        state.Energy = 0;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == CL.Prolong);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Prolong && card.Upgraded);
        Assert.Equal(4, BuffSystem.Get(state.PlayerBuffs, BuffId.BlockNextTurn));
    }
}
