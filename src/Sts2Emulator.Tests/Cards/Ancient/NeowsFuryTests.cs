using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class NeowsFuryTests
{
    [Fact]
    public void DamagesTargetMovesDiscardCardsToHandAndExhausts()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(AN.NeowsFury, false)];
        state.DiscardPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(30, state.Enemies[0].Hp);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.Equal([IC.Bash], state.DiscardPile.Select(card => card.DefId));
        Assert.Contains(state.ExhaustPile, card => card.DefId == AN.NeowsFury);
    }

    [Fact]
    public void UpgradedMovesThreeCardsAndRespectsHandCap()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(AN.NeowsFury, true),
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
        state.DiscardPile =
        [
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.Bash, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(26, state.Enemies[0].Hp);
        Assert.Equal(10, state.Hand.Count);
        Assert.Equal(
            [
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.StrikeIronclad,
                IC.DefendIronclad,
            ],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.Bash], state.DiscardPile.Select(card => card.DefId));
    }
}
