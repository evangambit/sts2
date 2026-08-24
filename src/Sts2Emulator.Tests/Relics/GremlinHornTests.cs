using System;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Gremlin Horn: EnergyVar(1) and CardsVar(1) every time something on the other side dies.
/// </summary>
/// <remarks>
/// It was not modelled at all — no reference to it anywhere in the emulator. A live
/// capture (`8QKMNR4T2W`, a Large Capsule run that was handed it on floor one) plays two
/// Strikes on its first turn and ends with the same energy it started the second with,
/// because the kill refunded the second one and drew a card. That is the kind of relic
/// only a capture that HOLDS it can catch, which is the argument for buffed runs: this one
/// showed up at step 7.
/// </remarks>
public class GremlinHornTests
{
    private static CombatState WithHorn(int enemyHp)
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Relics.Add(new RelicInstance(RelicEffects.GremlinHorn, 0));
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.DrawPile.Clear();
        state.DrawPile.Add(new CardInstance(IC.DefendIronclad, false));
        state.DrawPile.Add(new CardInstance(IC.DefendIronclad, false));
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 21,
                Hp = enemyHp,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Attack, 5),
                Buffs = [],
            },
        ];
        state.Energy = 3;
        return state;
    }

    [Fact]
    public void AKillRefundsTheEnergyAndDrawsACard()
    {
        var state = WithHorn(enemyHp: 1);
        int drawBefore = state.DrawPile.Count;

        CombatEngine.Step(state, 0, new Random(0));

        // Strike costs 1 and the death gives it straight back.
        Assert.Equal(3, state.Energy);
        Assert.Equal(drawBefore - 1, state.DrawPile.Count);
    }

    [Fact]
    public void AnEnemyLeftStandingPaysNothing()
    {
        var state = WithHorn(enemyHp: 30);
        int drawBefore = state.DrawPile.Count;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal(drawBefore, state.DrawPile.Count);
    }

    [Fact]
    public void WithoutTheRelicNothingHappens()
    {
        var state = WithHorn(enemyHp: 1);
        state.Relics.Clear();
        int drawBefore = state.DrawPile.Count;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal(drawBefore, state.DrawPile.Count);
    }
}
