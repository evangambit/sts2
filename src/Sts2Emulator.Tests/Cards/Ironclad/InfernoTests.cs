using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class InfernoTests
{
    [Fact]
    public void TriggersWhenPlayerLosesHpOnPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Inferno, false), new CardInstance(IC.Hemokinesis, false)];
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

        Assert.Equal(48, state.PlayerHp);
        Assert.Equal(6, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.InfernoSelfDamage));
        Assert.Equal(79, state.Enemies[0].Hp);
        Assert.Equal(94, state.Enemies[1].Hp);
    }

    [Fact]
    public void DamagesPlayerAndEnemiesAtStartOfPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Inferno, true)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 1;
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
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal(9, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(91, state.Enemies[0].Hp);
        Assert.Equal(91, state.Enemies[1].Hp);
    }
}
