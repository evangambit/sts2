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

public class PhantomBladesTests
{
    /// <summary>
    /// `PhantomBladesPower` does two things and the emulator modelled neither: every Shiv
    /// the player owns takes the Retain keyword, and `ModifyDamageAdditive` pays its
    /// amount to a Shiv attack only while NO Shiv play has finished this turn — the FIRST
    /// Shiv of the turn, once. It was stacking InfiniteBlades, which adds a Shiv per turn.
    /// </summary>
    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public void OnlyTheFirstShivOfTheTurnHitsHarder(bool upgraded, int bonus)
    {
        var fight = Fight
            .Hand(Card(SI.PhantomBlades, upgraded), Card(SI.Shiv), Card(SI.Shiv))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();
        int shiv = GeneratedData.Cards.Get(SI.Shiv).BaseDamage;
        int before = fight.Enemy0.Hp;

        fight.Play(); // the first Shiv: base plus the bonus
        int afterFirst = fight.Enemy0.Hp;
        Assert.Equal(before - (shiv + bonus), afterFirst);

        fight.Play(); // the second: base only
        Assert.Equal(afterFirst - shiv, fight.Enemy0.Hp);
    }

    /// <summary>And the bonus comes back next turn, since the count is per TURN.</summary>
    [Fact]
    public void TheBonusReturnsTheFollowingTurn()
    {
        var fight = Fight.Hand(Card(SI.PhantomBlades), Card(SI.Shiv)).Energy(9).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        fight.Play();
        fight.Play();
        Assert.Equal(1, fight.State.ShivsPlayedThisTurn);

        fight.EndTurn();

        Assert.Equal(0, fight.State.ShivsPlayedThisTurn);
    }

    /// <summary>A Shiv in hand survives the turn while the power is up.</summary>
    [Fact]
    public void ShivsRetain()
    {
        var fight = Fight.Hand(Card(SI.PhantomBlades), Card(SI.Shiv)).Energy(9);
        fight.State.PlayerHp = 999;

        fight.Play();
        fight.EndTurn();

        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Shiv);
    }

    [Fact]
    public void WithoutThePowerAShivIsDiscardedAsUsual()
    {
        var fight = Fight.Hand(Card(SI.Shiv)).Energy(9);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Shiv);
    }
}

public class SerpentFormTests
{
    /// <summary>
    /// `SerpentFormPower` records its amount before each card the player plays and spends
    /// it after, on a RANDOM hittable enemy. The emulator stacked NoxiousFumes, which
    /// poisons every enemy each turn — a different card.
    /// </summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void EveryCardPlayedHitsARandomEnemy(bool upgraded, int amount)
    {
        var fight = Fight
            .Hand(Card(SI.SerpentForm, upgraded), Card(SI.DefendSilent))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play(); // the power itself
        int before = fight.Enemy0.Hp;

        fight.Play(); // a Defend, which is not an attack and still triggers it

        Assert.Equal(before - amount, fight.Enemy0.Hp);
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));
    }

    /// <summary>
    /// The card that APPLIES the power does not trigger it: the amount is recorded before
    /// the play, and before this play there was no power to record.
    /// </summary>
    [Fact]
    public void TheSerpentFormThatAppliedItDoesNotTrigger()
    {
        var fight = Fight.Hand(Card(SI.SerpentForm)).Energy(9).Enemy(hp: 200);
        int before = fight.Enemy0.Hp;

        fight.Play();

        Assert.Equal(before, fight.Enemy0.Hp);
    }
}

public class TheHuntTests
{
    /// <summary>
    /// `TheHuntPower` is a visual marker and nothing else — the behaviour is in the card.
    /// A kill adds a whole extra CardReward of three to the room, which is the entire
    /// reason to play it and was simply absent.
    /// </summary>
    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 15)]
    public void ItDealsItsDamageAndEarnsNothingWithoutAKill(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.TheHunt, upgraded)).Energy(1).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.ExtraCardRewards);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.TheHunt));
    }

    [Fact]
    public void AKillEarnsAnExtraCardReward()
    {
        var fight = Fight.Hand(Card(SI.TheHunt)).Energy(1).Enemy(hp: 4);

        fight.Play();

        Assert.Equal(0, fight.Enemy0.Hp);
        Assert.Equal(1, fight.State.ExtraCardRewards);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TheHunt));
    }

    /// <summary>
    /// The Hunt shares Feed's `ShouldOwnerDeathTriggerFatal` gate and had none at all: a
    /// summoned minion is free to kill, so without the gate a Reptomancer fight hands out
    /// an extra card reward for every dagger.
    /// </summary>
    [Fact]
    public void KillingAMinionEarnsNoExtraReward()
    {
        var fight = Fight
            .Hand(new CardInstance(SI.TheHunt, false))
            .Energy(3)
            .Enemy(hp: 1, buffs: new BuffState(BuffId.Minion, 1));

        fight.Play(0);

        Assert.True(fight.State.Enemies[0].Hp <= 0);
        Assert.Equal(0, fight.State.ExtraCardRewards);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.TheHunt));
    }

}
