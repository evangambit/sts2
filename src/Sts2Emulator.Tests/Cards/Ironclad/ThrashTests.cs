using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ThrashTests
{
    [Fact]
    public void TwoHitsAndExhaustsRandomAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        int hpBefore = enemy.Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Thrash, false));
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false)); // Attack to exhaust
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, should not be exhausted
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        // Thrash deals 4×2=8 damage
        Assert.Equal(hpBefore - 8, enemy.Hp);
        // The Attack (Strike) should be exhausted, Skill (Defend) should remain
        Assert.Equal(0, state.ExhaustPile.Count(c => c.DefId == IC.Thrash)); // Thrash does not exhaust itself
        Assert.DoesNotContain(state.Hand, c => c.DefId == IC.StrikeIronclad);
    }

    /// <summary>
    /// The exhaust is only half the card. `base.DynamicVars.Damage.BaseValue += damage`:
    /// Thrash permanently gains the damage of the Attack it ate, so it grows all combat.
    /// The emulator exhausted the card and threw the number away.
    /// </summary>
    [Fact]
    public void ItAbsorbsTheDamageOfTheAttackItExhausts()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Thrash, false), new CardInstance(IC.Bludgeon, false))
            .Energy(3)
            .Enemy(hp: 300);

        fight.Play(0);

        // Bludgeon is the only Attack in hand, so it is what gets eaten: +32.
        var grown = fight.State.DiscardPile.Single(c => c.DefId == IC.Thrash);
        Assert.Equal(32, grown.BonusDamage);
    }

    /// <summary>The growth rides the copy, so the next play hits for the larger number.</summary>
    [Fact]
    public void TheGrowthShowsOnTheNextPlay()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Thrash, false), new CardInstance(IC.Bludgeon, false))
            .Energy(6)
            .Enemy(hp: 400);
        fight.Play(0);
        int afterFirst = fight.Enemy0.Hp;

        // Replay the grown copy out of the discard pile.
        var grown = fight.State.DiscardPile.Single(c => c.DefId == IC.Thrash);
        fight.State.DiscardPile.Remove(grown);
        fight.State.Hand.Add(grown);
        fight.State.Energy = 3;
        fight.Play(fight.State.Hand.Count - 1);

        int secondVolley = afterFirst - fight.Enemy0.Hp;
        // Two hits of (4 printed + 32 absorbed).
        Assert.Equal(2 * 36, secondVolley);
    }

    [Fact]
    public void WithNoAttackInHandItGrowsNothing()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Thrash, false), new CardInstance(IC.DefendIronclad, false))
            .Energy(3)
            .Enemy(hp: 300);

        fight.Play(0);

        var played = fight.State.DiscardPile.Single(c => c.DefId == IC.Thrash);
        Assert.Equal(0, played.BonusDamage);
    }

    /// <summary>
    /// Measured against the running game, not read: fed a Body Slam with ten block on the
    /// board, the game's Thrash went from "Deal 4 damage twice" to "Deal 14 damage twice".
    /// It absorbs the CALCULATED value, so a printed-damage reading grows it by nothing.
    /// </summary>
    [Fact]
    public void ItAbsorbsACalculatedCardsComputedValue()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Thrash, false), new CardInstance(IC.BodySlam, false))
            .Energy(3)
            .Enemy(hp: 300);
        fight.State.PlayerBlock = 10;

        fight.Play(0);

        var grown = fight.State.DiscardPile.Single(c => c.DefId == IC.Thrash);
        Assert.Equal(10, grown.BonusDamage);
    }

    /// <summary>And the growth is what the next play hits for: 4 printed + 10 absorbed.</summary>
    [Fact]
    public void TheAbsorbedCalculationShowsOnTheNextPlay()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Thrash, false), new CardInstance(IC.BodySlam, false))
            .Energy(9)
            .Enemy(hp: 400);
        fight.State.PlayerBlock = 10;
        fight.Play(0);
        int afterFirst = fight.Enemy0.Hp;

        var grown = fight.State.DiscardPile.Single(c => c.DefId == IC.Thrash);
        fight.State.DiscardPile.Remove(grown);
        fight.State.Hand.Add(grown);
        fight.State.Energy = 3;
        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(2 * 14, afterFirst - fight.Enemy0.Hp);
    }
}
