using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class PillageTests
{
    [Fact]
    public void DamagesAndDrawsUntilNonAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Pillage, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
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

        Assert.Equal(44, state.Enemies[0].Hp);
        Assert.Equal(
            [IC.StrikeIronclad, IC.Bash, IC.DefendIronclad],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.StrikeIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void UpgradedUsesUpgradedDamageAndStopsAtFullHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Pillage, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
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

        Assert.Equal(41, state.Enemies[0].Hp);
        Assert.Equal(10, state.Hand.Count);
        Assert.Equal([IC.DefendIronclad], state.DrawPile.Select(card => card.DefId));
    }
}
