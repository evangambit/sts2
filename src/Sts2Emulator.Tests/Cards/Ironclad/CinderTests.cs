using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class CinderTests
{
    [Fact]
    public void DamagesTargetAndExhaustsRandomCardFromHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Cinder, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 2;
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

        Assert.Equal(32, state.Enemies[0].Hp);
        Assert.Empty(state.Hand);
        // Cinder exhausts a random card from hand, not itself — it declares no
        // Exhaust keyword, so it discards like any other attack.
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.Cinder);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Cinder);
    }

    [Fact]
    public void UpgradedUsesUpgradedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Cinder, true)];
        state.Energy = 2;
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

        Assert.Equal(26, state.Enemies[0].Hp);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.Cinder);
    }
}
