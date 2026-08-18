using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class StampedeTests
{
    [Fact]
    public void AppliesTrackedPower()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stampede, false)];
        state.Energy = 2;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Stampede));
    }

    [Fact]
    public void AutoPlaysAttackFromRemainingHandAtEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Stampede, false),
            new CardInstance(IC.HowlFromBeyond, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 2, new Random(0));

        Assert.Equal(14, state.Enemies[0].Hp);
    }

    [Fact]
    public void RepeatsForStackCountAndSkipsUnplayableCards()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.AscendersBane, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.PlayerBuffs = [new BuffState(BuffId.Stampede, 2)];
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

        CombatEngine.Step(state, state.Hand.Count, new Random(0));

        Assert.Equal(38, state.Enemies[0].Hp);
    }
}
