using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class OneTwoPunchTests
{
    [Fact]
    public void DuplicatesNextAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.OneTwoPunch, false),
            new CardInstance(IC.StrikeIronclad, false),
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
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(88, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }

    [Fact]
    public void UpgradedDuplicatesNextTwoAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.OneTwoPunch, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 3;
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

        Assert.Equal(76, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }

    [Fact]
    public void ExpiresAtEndOfPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.OneTwoPunch, false)];
        state.DrawPile.Clear();
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
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.OneTwoPunch));
    }
}
