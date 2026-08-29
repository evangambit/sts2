using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ExpectAFightTests
{
    [Fact]
    public void GainsEnergyForAttacksInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.ExpectAFight, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.SwordBoomerang, false),
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

        Assert.Equal(2, state.Energy);
    }

    /// <summary>
    /// `PowerCmd.Apply&lt;NoEnergyGainPower&gt;(..., Owner, ...)` is the second half of the
    /// card and was absent: it was a free burst with no downside. The lockout is applied
    /// AFTER the card's own gain, so the card pays itself and then shuts the tap.
    /// </summary>
    [Fact]
    public void ItLocksOutFurtherEnergyGainsThisTurn()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.ExpectAFight, false), new CardInstance(IC.StrikeIronclad, false))
            .Energy(3);

        fight.Play(0);
        int afterTheCard = fight.State.Energy;

        // Its own gain landed...
        Assert.True(afterTheCard > 1);

        // ...and nothing after it does.
        CardEffects.GainEnergy(fight.State, 5);
        Assert.Equal(afterTheCard, fight.State.Energy);
    }

    /// <summary>
    /// `AfterSideTurnEnd` removes it, so the lockout lasts the turn it was played and no
    /// longer -- a permanent one would make the card unplayable rather than costly.
    /// </summary>
    [Fact]
    public void TheLockoutIsGoneNextTurn()
    {
        var fight = Fight.Hand(new CardInstance(IC.ExpectAFight, false)).Energy(3);
        fight.Play(0);
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NoEnergyGain));

        int before = fight.State.Energy;
        CardEffects.GainEnergy(fight.State, 2);
        Assert.Equal(before + 2, fight.State.Energy);
    }

    /// <summary>The turn-start RESET is a different path, and the lockout does not touch it.</summary>
    [Fact]
    public void ItDoesNotStopTheTurnStartReset()
    {
        var fight = Fight.Hand(new CardInstance(IC.ExpectAFight, false)).Energy(3);
        fight.Play(0);
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.Equal(fight.State.MaxEnergy, fight.State.Energy);
    }
}
