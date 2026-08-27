using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Cards the source marked `approximation`. On the poison batch's evidence that word is
// worth reading as "unverified" rather than "close": all three there turned out to be a
// DIFFERENT card, and so did most of these.

public class BurstTests
{
    /// <summary>
    /// `BurstPower.ModifyCardPlayCount` adds a play to a SKILL and decrements itself. The
    /// emulator stacked OneTwoPunch, which is the same rule for ATTACKS — so Burst
    /// doubled the wrong half of the deck.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void TheNextSkillsArePlayedTwice(bool upgraded, int skills)
    {
        var fight = Fight
            .Hand(Card(SI.Burst, upgraded), Card(SI.DefendSilent), Card(SI.DefendSilent))
            .Energy(9);

        fight.Play();
        Assert.Equal(skills, fight.PlayerBuffAmount(BuffId.Burst));

        fight.Play(); // a Defend, doubled: 5 + 5

        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(skills - 1, fight.PlayerBuffAmount(BuffId.Burst));
    }

    /// <summary>An ATTACK is not what Burst doubles, which is the whole of the old defect.</summary>
    [Fact]
    public void AnAttackIsNotDoubled()
    {
        var fight = Fight.Hand(Card(SI.Burst), Card(SI.StrikeSilent)).Energy(9).Enemy(hp: 60);

        fight.Play();
        fight.Play();

        Assert.Equal(60 - 6, fight.Enemy0.Hp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Burst));
    }
}

public class MasterPlannerTests
{
    /// <summary>
    /// `MasterPlannerPower.AfterCardPlayed` applies the Sly KEYWORD to every Skill its
    /// owner plays — permanently for the combat, so the copy plays itself the next time
    /// anything discards it. The emulator drew a card next turn, which is a different
    /// card; the upgrade is a cost cut, not a bigger effect.
    /// </summary>
    [Fact]
    public void EverySkillPlayedBecomesSlyForTheRestOfTheCombat()
    {
        var fight = Fight.Hand(Card(SI.MasterPlanner), Card(SI.DefendSilent)).Energy(9);

        fight.Play(); // the power
        fight.Play(); // a Defend

        var defend = fight.State.DiscardPile.Single(c => c.DefId == SI.DefendSilent);
        Assert.True(defend.SlyForCombat);
    }

    /// <summary>An Attack is untouched: the hook filters on `card.Type != Skill`.</summary>
    [Fact]
    public void AnAttackIsNotMarked()
    {
        var fight = Fight
            .Hand(Card(SI.MasterPlanner), Card(SI.StrikeSilent))
            .Energy(9)
            .Enemy(hp: 60);

        fight.Play();
        fight.Play();

        Assert.False(fight.State.DiscardPile.Single(c => c.DefId == SI.StrikeSilent).SlyForCombat);
    }

    /// <summary>
    /// And the mark is what it is for: the marked Skill, drawn again and then discarded,
    /// plays itself.
    /// </summary>
    [Fact]
    public void TheMarkedSkillPlaysWhenLaterDiscarded()
    {
        var fight = Fight.Hand(Card(SI.MasterPlanner), Card(SI.DefendSilent)).Energy(9);
        fight.Play();
        fight.Play();

        // Bring it back and throw it away.
        var marked = fight.State.DiscardPile.Single(c => c.DefId == SI.DefendSilent);
        fight.State.DiscardPile.Remove(marked);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(marked);
        fight.State.Hand.Add(Card(SI.Neutralize));

        CardEffects.DiscardFirstCardsFromHand(fight.State, 1);
        Assert.Contains(fight.State.AutoPlayQueue, c => c.DefId == SI.DefendSilent);

        // The queue drains at the top of the next step, so playing anything resolves it --
        // asserted before ending the turn, which would clear the block.
        int blockBefore = fight.State.PlayerBlock;
        fight.Play(0);

        Assert.Equal(blockBefore + 5, fight.State.PlayerBlock);
    }
}

public class SpeedsterTests
{
    /// <summary>
    /// `SpeedsterPower.AfterCardDrawn` fires when the draw is NOT the hand draw and the
    /// player's own side is acting, dealing 2 unpowered damage to every enemy. The
    /// emulator gave energy next turn, which is a different card.
    /// </summary>
    [Fact]
    public void EveryMidTurnDrawHitsAllEnemies()
    {
        var fight = Fight.Hand(Card(SI.Speedster)).Energy(2).Enemy(hp: 60);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 3; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();
        int before = fight.Enemy0.Hp;

        CardEffects.DrawCards(fight.State, 3, new Random(0));

        Assert.Equal(before - 6, fight.Enemy0.Hp);
    }
}

public class MurderTests
{
    /// <summary>
    /// `CalculationBaseVar(1)` plus `ExtraDamageVar(1)` times the number of CardDrawnEntry
    /// rows for this player — every card drawn this COMBAT. The emulator dealt a flat
    /// 25/35, a guess at what that averages to.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ItDealsOnePerCardDrawnThisCombat(bool upgraded, int baseDamage)
    {
        var fight = Fight.Hand(Card(SI.Murder, upgraded)).Energy(3).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 7; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.State.CardsDrawnThisCombat = 0;
        CardEffects.DrawCards(fight.State, 7, new Random(0));
        int drawn = fight.State.CardsDrawnThisCombat;
        Assert.Equal(7, drawn);

        int before = fight.Enemy0.Hp;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Murder));

        Assert.Equal(before - (baseDamage + drawn), fight.Enemy0.Hp);
    }
}

public class TrackingTests
{
    /// <summary>
    /// `TrackingPower.ModifyDamageMultiplicative` returns its own Amount when the target
    /// has Weak and the damage is a powered card attack — so Tracking 2 is DOUBLE damage.
    /// The emulator applied Vicious, an unrelated power.
    /// </summary>
    [Fact]
    public void CardAttacksOnAWeakTargetAreDoubled()
    {
        var fight = Fight.Hand(Card(SI.Tracking), Card(SI.StrikeSilent)).Energy(9).Enemy(hp: 60);
        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Tracking));

        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Weak, 2);
        fight.Play();

        Assert.Equal(60 - 12, fight.Enemy0.Hp);
    }

    /// <summary>Without Weak on the target it does nothing at all.</summary>
    [Fact]
    public void AnUnweakenedTargetTakesTheOrdinaryDamage()
    {
        var fight = Fight.Hand(Card(SI.Tracking), Card(SI.StrikeSilent)).Energy(9).Enemy(hp: 60);
        fight.Play();
        fight.Play();

        Assert.Equal(60 - 6, fight.Enemy0.Hp);
    }

    /// <summary>
    /// A second Tracking adds ONE rather than another two, which is why the card reads its
    /// own power before applying.
    /// </summary>
    [Fact]
    public void ASecondCopyAddsOne()
    {
        var fight = Fight.Hand(Card(SI.Tracking), Card(SI.Tracking)).Energy(9);

        fight.Play();
        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Tracking));
    }
}

public class WraithFormTests
{
    /// <summary>
    /// `WraithFormPower.AfterSideTurnStart` takes its Amount in Dexterity at the start of
    /// EVERY turn — the price the card is built around. The emulator charged it once, as
    /// the card was played.
    /// </summary>
    [Fact]
    public void TheDexterityCostIsPaidEveryTurn()
    {
        var fight = Fight.Hand(Card(SI.WraithForm)).Energy(3);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Intangible));
        // Nothing is charged on the turn it is played: the power's hook is a turn START.
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.EndTurn();
        Assert.Equal(-1, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.State.PlayerHp = 999;
        fight.EndTurn();
        Assert.Equal(-2, fight.PlayerBuffAmount(BuffId.Dexterity));
    }
}

/// <summary>
/// Two of the eleven approximations are unreachable in a solo run, so their approximation
/// costs nothing: <c>CardMultiplayerConstraint.MultiplayerOnly</c> means
/// <c>CardFactory.FilterForPlayerCount</c> drops them from the pool before the rarity
/// roll. Pinned rather than rewritten, because the next reader working the approximation
/// list should not spend a batch on a card no single-player run can hold.
/// </summary>
internal static class MultiplayerOnlyCard
{
    public static void IsNeverOfferedSolo(int defId)
    {
        Assert.True(GeneratedData.Cards.Get(defId).MultiplayerOnly);
        Assert.False(RunRewardGenerator.IsAllowedSolo(defId));
    }
}

public class FlankingTests
{
    [Fact]
    public void ASoloRunIsNeverOfferedIt() => MultiplayerOnlyCard.IsNeverOfferedSolo(SI.Flanking);
}

public class SneakyTests
{
    [Fact]
    public void ASoloRunIsNeverOfferedIt() => MultiplayerOnlyCard.IsNeverOfferedSolo(SI.Sneaky);
}
