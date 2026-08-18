using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

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

    /// <summary>
    /// Cinder.cs picks with Rng.CombatCardSelection.NextItem(hand), so the draw has to
    /// come off that stream — using the combat rng would desynchronise everything after
    /// it, the same failure TargetRng was introduced to fix.
    /// </summary>
    [Fact]
    public void DrawsItsExhaustFromTheCardSelectionStream()
    {
        var fight = Fight
            .Hand(Card(IC.Cinder), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(2)
            .Enemy(hp: 60);
        var selectionRng = new CountingRandom(5);
        fight.State.CardSelectionRng = selectionRng;

        fight.Play();

        Assert.Equal(1, selectionRng.CallCount);
        Assert.Single(fight.State.ExhaustPile);
    }
}
