using System;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using static Sts2Emulator.Tests.TestDeck;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/BulletTime.cs: every card in hand is made free for the
// turn EXCEPT an X-cost one — `if (!card.EnergyCost.CostsX)` — and then NoDrawPower 1.
//
// Making an X card free is not a smaller version of the rule, it is the opposite of it: an
// X card spends what is left, so a free one spends nothing and does nothing. The emulator
// freed the whole hand.
public class BulletTimeXCostTests
{
    [Fact]
    public void AnXCostCardInHandIsNotMadeFree()
    {
        var fight = Fight
            .Hand(Card(SI.BulletTime), Card(SI.StrikeSilent), Card(SI.Malaise))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();

        var strike = fight.State.Hand.Single(c => c.DefId == SI.StrikeSilent);
        var malaise = fight.State.Hand.Single(c => c.DefId == SI.Malaise);
        Assert.True(strike.FreeThisTurn);
        Assert.False(malaise.FreeThisTurn);
    }

    /// <summary>The X card still spends the energy it is played with.</summary>
    [Fact]
    public void TheXCardStillSpendsTheEnergy()
    {
        var fight = Fight
            .Hand(Card(SI.BulletTime), Card(SI.Malaise))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();
        int before = fight.State.Energy;
        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.Energy);
        Assert.Equal(before, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Pinpoint.cs: 15 damage upgrading by 4, and its cost is
// the rest of the card — two hooks that both call `EnergyCost.AddThisTurn(-1)` per SKILL
// played this turn. `AfterCardPlayed` pays each new one, `AfterCardEnteredCombat` pays the
// backlog for a copy that arrives late.
//
// The emulator had the damage and a comment saying the cost reduces "in game".
public class PinpointCostTests
{
    [Fact]
    public void UnplayedItCostsThree()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);

        Assert.Equal(3, CombatEngine.EffectiveCost(new CardInstance(SI.Pinpoint, false), fight.State));
    }

    [Fact]
    public void EachSkillTakesOneOffIt()
    {
        var fight = Fight.Hand(Card(SI.DefendSilent), Card(SI.DefendSilent)).Energy(9).Enemy(hp: 200);

        fight.Play();
        Assert.Equal(2, CombatEngine.EffectiveCost(new CardInstance(SI.Pinpoint, false), fight.State));

        fight.Play();
        Assert.Equal(1, CombatEngine.EffectiveCost(new CardInstance(SI.Pinpoint, false), fight.State));
    }

    /// <summary>ATTACKS do not — the hook filters on `Card.Type == Skill`.</summary>
    [Fact]
    public void AttacksDoNotDiscountIt()
    {
        var fight = Fight.Hand(Card(SI.StrikeSilent)).Energy(9).Enemy(hp: 200);

        fight.Play(0, target: 0);

        Assert.Equal(3, CombatEngine.EffectiveCost(new CardInstance(SI.Pinpoint, false), fight.State));
    }

    /// <summary>`AddThisTurn`, so it is full price again next turn.</summary>
    [Fact]
    public void ItIsFullPriceAgainNextTurn()
    {
        var fight = Fight.Hand(Card(SI.DefendSilent)).Energy(9).Enemy(hp: 200);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(3, CombatEngine.EffectiveCost(new CardInstance(SI.Pinpoint, false), fight.State));
    }

    /// <summary>
    /// A copy that arrives after the Skills is priced the same — the entered-combat hook
    /// pays the backlog, which is why the discount is derived rather than stamped.
    /// </summary>
    [Fact]
    public void ACopyThatArrivesLateIsPricedTheSame()
    {
        var fight = Fight.Hand(Card(SI.DefendSilent)).Energy(9).Enemy(hp: 200);
        fight.Play();

        var arrived = new CardInstance(SI.Pinpoint, false);
        fight.State.Hand.Add(arrived);

        Assert.Equal(2, CombatEngine.EffectiveCost(arrived, fight.State));
    }
}

// Flanking and Sneaky are `MultiplayerOnly` and `CanPlay` refuses them in a solo run, so
// their arms are unreachable and exist to say what the card is. Both used to apply a
// player buff that DOES pay out solo, which is worse than doing nothing: Flanking gave two
// energy next turn where the card is a damage multiplier on an ENEMY for the player's
// ALLIES, and Sneaky stacked Afterimage where the card gains block when ANOTHER player
// plays an Attack.
public class MultiplayerOnlySilentCardTests
{
    [Fact]
    public void FlankingGivesThePlayerNothing()
    {
        var fight = Fight.Hand(Card(SI.Flanking)).Energy(9).Enemy(hp: 200);

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
        fight.PlayerPowersAre();
    }

    [Fact]
    public void SneakyGivesThePlayerNothing()
    {
        var fight = Fight.Hand(Card(SI.Sneaky)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Afterimage));
        fight.PlayerPowersAre();
    }

    /// <summary>Neither enemy is touched either — Flanking's power is on the target.</summary>
    [Fact]
    public void FlankingLeavesTheEnemyAlone()
    {
        var fight = Fight.Hand(Card(SI.Flanking)).Energy(9).Enemy(hp: 200);

        fight.Play(0, target: 0);

        Assert.Equal(200, fight.Enemy0.Hp);
        Assert.Empty(fight.Enemy0.Buffs.Where(b => b.Magnitude != 0));
    }
}

/// <summary>
/// Properties the Silent reading pass turned up that nothing was asserting. The cards were
/// already right; these pin the half of each reading that had no test behind it.
/// </summary>
public class SilentReadPassPinTests
{
    /// <summary>Haze carries the Sly keyword, and costs three rather than one.</summary>
    [Fact]
    public void HazeIsSlyAndCostsThree()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);

        Assert.True(new CardInstance(SI.Haze, false).IsSlyThisTurn());
        Assert.Equal(3, CombatEngine.EffectiveCost(new CardInstance(SI.Haze, false), fight.State));
    }

    /// <summary>Speedster's upgrade adds INNATE — the damage stays at 2.</summary>
    [Fact]
    public void SpeedsterUpgradesToInnateNotToMoreDamage()
    {
        var plain = Fight.Hand(Card(SI.Speedster)).Energy(9).Enemy(hp: 200);
        plain.Play();
        var up = Fight.Hand(Card(SI.Speedster, upgraded: true)).Energy(9).Enemy(hp: 200);
        up.Play();

        Assert.Equal(2, plain.PlayerBuffAmount(BuffId.Speedster));
        Assert.Equal(2, up.PlayerBuffAmount(BuffId.Speedster));
        Assert.False(GeneratedData.Cards.Get(SI.Speedster).Innate);
        Assert.True(GeneratedData.Cards.Get(SI.Speedster).InnateWhenUpgraded);
    }

    /// <summary>
    /// Grand Finale is unplayable while the draw pile has anything in it — a playability
    /// rule, so it has to reach the action mask rather than be checked inside the effect.
    /// </summary>
    [Fact]
    public void GrandFinaleIsNotOfferedWithCardsLeftToDraw()
    {
        var fight = Fight.Hand(Card(SI.GrandFinale)).Energy(9).Enemy(hp: 500);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));

        Assert.DoesNotContain(0, CombatEngine.ValidActions(fight.State));

        fight.State.DrawPile.Clear();
        Assert.Contains(0, CombatEngine.ValidActions(fight.State));
    }

    /// <summary>
    /// Precise Cut counts the hand it leaves BEHIND: the played card is out of hand by the
    /// time the damage is calculated, so a lone Precise Cut is the full 13.
    /// </summary>
    [Fact]
    public void PreciseCutDoesNotCountItself()
    {
        var alone = Fight.Hand(Card(SI.PreciseCut)).Energy(9).Enemy(hp: 500);
        alone.Play(0, target: 0);
        Assert.Equal(487, alone.Enemy0.Hp);

        var crowded = Fight
            .Hand(Card(SI.PreciseCut), Card(SI.StrikeSilent), Card(SI.StrikeSilent))
            .Energy(9)
            .Enemy(hp: 500);
        crowded.Play(0, target: 0);
        Assert.Equal(491, crowded.Enemy0.Hp);
    }

    /// <summary>
    /// Finisher and Flechettes both have `CalculationBase 0`, and `AttackCommand`'s hit
    /// loop simply does not run at zero — there is no minimum of one hit.
    /// </summary>
    [Fact]
    public void FinisherAndFlechettesDealNothingAtZero()
    {
        var finisher = Fight.Hand(Card(SI.Finisher)).Energy(9).Enemy(hp: 500);
        finisher.Play(0, target: 0);
        Assert.Equal(500, finisher.Enemy0.Hp);

        var flechettes = Fight.Hand(Card(SI.Flechettes)).Energy(9).Enemy(hp: 500);
        flechettes.Play(0, target: 0);
        Assert.Equal(500, flechettes.Enemy0.Hp);
    }

    /// <summary>Finisher's own play is not in its count — the entry is written when the play FINISHES.</summary>
    [Fact]
    public void FinisherDoesNotCountItself()
    {
        var fight = Fight.Hand(Card(SI.StrikeSilent), Card(SI.Finisher)).Energy(9).Enemy(hp: 500);

        fight.Play(0, target: 0);
        int before = fight.Enemy0.Hp;
        fight.Play(0, target: 0);

        Assert.Equal(before - 6, fight.Enemy0.Hp);
    }
}
