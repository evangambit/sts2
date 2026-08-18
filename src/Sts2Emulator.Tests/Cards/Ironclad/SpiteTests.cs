using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class SpiteTests
{
    [Fact]
    public void HitsOnceBeforePlayerLosesHpThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Spite, false)];
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

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(45, state.Enemies[0].Hp);
    }

    [Fact]
    public void HitsTwiceAfterCardHpLossThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Bloodletting, false), new CardInstance(IC.Spite, false)];
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

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(47, state.PlayerHp);
        Assert.Equal(40, state.Enemies[0].Hp);
    }

    [Fact]
    public void UpgradedHitsThreeTimesAfterHpLossThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, false), new CardInstance(IC.Spite, true)];
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
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal(26, state.Enemies[0].Hp);
    }

    [Fact]
    public void HpLossConditionResetsOnNextPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Bloodletting, false)];
        state.DrawPile = [new CardInstance(IC.Spite, false)];
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(45, state.Enemies[0].Hp);
    }
}
