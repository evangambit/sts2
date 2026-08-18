using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class BolasTests
{
    [Fact]
    public void DamagesTargetAndReturnsBeforeNextDraw()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Bolas, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(17, state.Enemies[0].Hp);
        Assert.Contains(state.DiscardPile, card => card.DefId == CL.Bolas);
        Assert.Single(state.ReturnToHandBeforeDraw);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(CL.Bolas, state.Hand[0].DefId);
        Assert.Equal(6, state.Hand.Count);
        Assert.DoesNotContain(state.DiscardPile, card => card.DefId == CL.Bolas);
        Assert.Empty(state.ReturnToHandBeforeDraw);
    }

    [Fact]
    public void UpgradedUsesUpgradedDamageAndReturnsUpgraded()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(CL.Bolas, true)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 0;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(16, state.Enemies[0].Hp);
        Assert.Single(state.Hand);
        Assert.Equal(CL.Bolas, state.Hand[0].DefId);
        Assert.True(state.Hand[0].Upgraded);
    }

    [Fact]
    public void ReturnsOnceWhenAttackEffectIsDuplicated()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.OneTwoPunch, false), new CardInstance(CL.Bolas, false)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(14, state.Enemies[0].Hp);
        Assert.Single(state.ReturnToHandBeforeDraw);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, state.Hand.Count(card => card.DefId == CL.Bolas));
        Assert.Empty(state.ReturnToHandBeforeDraw);
    }
}
