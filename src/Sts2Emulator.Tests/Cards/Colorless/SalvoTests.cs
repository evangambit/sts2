using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class SalvoTests
{
    [Fact]
    public void DamagesTargetAndRetainsRemainingHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(CL.Salvo, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.DiscardPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 40,
                MaxHp = 40,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(28, state.Enemies[0].Hp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.RetainHand));
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Salvo);

        CombatEngine.Step(state, state.Hand.Count, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.RetainHand));
        Assert.Equal(
            [IC.StrikeIronclad, IC.DefendIronclad],
            state.Hand.Take(2).Select(card => card.DefId)
        );
        Assert.DoesNotContain(
            state.DiscardPile,
            card => card.DefId is IC.StrikeIronclad or IC.DefendIronclad
        );
    }

    [Fact]
    public void UpgradedUsesUpgradedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Salvo, true)];
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

        Assert.Equal(24, state.Enemies[0].Hp);
    }
}
