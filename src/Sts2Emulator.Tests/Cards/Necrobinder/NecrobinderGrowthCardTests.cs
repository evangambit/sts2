using System.Linq;
using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/TimesUp.cs: CalculationBase 0 plus ExtraDamage 1 per
// point of DOOM on the TARGET. Upgrading buys Retain, not damage.
//
// The emulator scaled it by the cards played this combat. The live capture could not tell
// the two apart — a fresh enemy has no Doom and no cards have been played, so both
// readings dealt nothing, and the fixture passed clean.
public class TimesUpTests
{
    private const int TimesUp = 509;

    [Fact]
    public void ItHitsForTheTargetsDoom()
    {
        var fight = Fight.Hand(new CardInstance(TimesUp, false))
            .Energy(9)
            .Enemy(hp: 200, buffs: [new BuffState(BuffId.Doom, 12)]);

        fight.Play();

        Assert.Equal(188, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithNoDoomItDealsNothing()
    {
        var fight = Fight.Hand(new CardInstance(TimesUp, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200, fight.Enemy0.Hp);
    }

    /// <summary>Cards played this combat are not the number — that was the old reading.</summary>
    [Fact]
    public void CardsPlayedDoNotFeedIt()
    {
        var fight = Fight.Hand(
                new CardInstance(473, false),
                new CardInstance(473, false),
                new CardInstance(TimesUp, false)
            )
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();
        fight.Play();
        int before = fight.Enemy0.Hp;
        fight.Play();

        Assert.Equal(before, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradingBuysRetain()
    {
        Assert.False(new CardInstance(TimesUp, false).IsRetained());
        Assert.True(new CardInstance(TimesUp, true).IsRetained());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/TheScythe.cs: `_baseDamage = 13`, and every play adds
// `IntVar("Increase", 4)` — 5 upgraded — to THIS copy's damage for good. Rampage's shape.
//
// The emulator scaled it by the cards exhausted this turn. Its printed damage is 0 in the
// card data because the DamageVar is built from a [SavedProperty] the extractor cannot
// read, which is how a wrong body went unnoticed.
public class TheScytheTests
{
    private const int TheScythe = 501;

    [Fact]
    public void ItHitsForThirteen()
    {
        var fight = Fight.Hand(new CardInstance(TheScythe, false)).Energy(9).Enemy(hp: 500);

        fight.Play();

        Assert.Equal(487, fight.Enemy0.Hp);
    }

    /// <summary>Exhausted cards are not the number — that was the old reading.</summary>
    [Fact]
    public void ExhaustsThisTurnDoNotFeedIt()
    {
        var fight = Fight.Hand(new CardInstance(TheScythe, false)).Energy(9).Enemy(hp: 500);
        fight.State.CardsExhaustedThisTurn = 6;

        fight.Play();

        Assert.Equal(487, fight.Enemy0.Hp);
    }

    /// <summary>The growth rides on the copy, so it shows when that copy is played again.</summary>
    [Fact]
    public void ThePlayedCopyGrowsByFour()
    {
        var fight = Fight.Hand(new CardInstance(TheScythe, false)).Energy(9).Enemy(hp: 500);
        fight.Play();

        var grown = fight.State.ExhaustPile[0];
        fight.State.ExhaustPile.Clear();
        fight.State.Hand.Add(grown);
        fight.Play();

        Assert.Equal(500 - 13 - 17, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedItGrowsByFive()
    {
        var fight = Fight.Hand(new CardInstance(TheScythe, true)).Energy(9).Enemy(hp: 500);
        fight.Play();

        var grown = fight.State.ExhaustPile[0];
        fight.State.ExhaustPile.Clear();
        fight.State.Hand.Add(grown);
        fight.Play();

        Assert.Equal(500 - 13 - 18, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = Fight.Hand(new CardInstance(TheScythe, false)).Energy(9).Enemy(hp: 500);

        fight.Play();

        Assert.Single(fight.State.ExhaustPile);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Undeath.cs: block 7 upgrading by 2, and then
// `CreateClone()` — a copy of UNDEATH, which the live capture places in the discard pile.
//
// The emulator made a SOUL instead. The pile COUNTS matched, so the capture passed and the
// card was still wrong: a deck that should fill with free 7-block skills filled with
// draw-one cantrips.
public class UndeathTests
{
    private const int Undeath = 523;
    private const int Soul = 446;
    private const int DefendNecrobinder = 132;

    [Fact]
    public void ItGainsSevenBlockAndNineUpgraded()
    {
        var fight = Fight.Hand(new CardInstance(Undeath, false)).Energy(9).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(7, fight.State.PlayerBlock);

        var up = Fight.Hand(new CardInstance(Undeath, true)).Energy(9).Enemy(hp: 200);
        up.Play();
        Assert.Equal(9, up.State.PlayerBlock);
    }

    [Fact]
    public void ItLeavesTwoUndeathsInTheDiscardPile()
    {
        var fight = Fight.Hand(new CardInstance(Undeath, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.All(fight.State.DiscardPile, c => Assert.Equal(Undeath, c.DefId));
        Assert.DoesNotContain(fight.State.DiscardPile, c => c.DefId == Soul);
    }

    /// <summary>The clone is of THIS copy, upgrade included.</summary>
    [Fact]
    public void TheCloneIsUpgradedIfItWas()
    {
        var fight = Fight.Hand(new CardInstance(Undeath, true)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.All(fight.State.DiscardPile, c => Assert.True(c.Upgraded));
    }

    /// <summary>Defend shares nothing with it — it was stacked on the same case.</summary>
    [Fact]
    public void DefendDoesNotCloneItself()
    {
        var fight = Fight.Hand(new CardInstance(DefendNecrobinder, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Single(fight.State.DiscardPile);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/GraveWarden.cs: BlockVar 8 upgrading by 3, then
// `CardsVar(1)` Souls into the DRAW pile at a random position — both halves already right.
//
// It is here because the Soul was briefly deleted: `card_pair.py` dropped every line
// containing `PreviewCardPileAdd(`, and this card's only other effect is written inside
// that call, so the source read as block and nothing else. The live capture caught it.
public class GraveWardenTests
{
    private const int GraveWarden = 228;

    [Fact]
    public void ItGainsEightBlockAndElevenUpgraded()
    {
        var fight = Fight.Hand(new CardInstance(GraveWarden, false)).Energy(9).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(8, fight.State.PlayerBlock);

        var up = Fight.Hand(new CardInstance(GraveWarden, true)).Energy(9).Enemy(hp: 200);
        up.Play();
        Assert.Equal(11, up.State.PlayerBlock);
    }

    [Fact]
    public void ItPutsOneSoulInTheDrawPile()
    {
        var fight = Fight.Hand(new CardInstance(GraveWarden, false)).Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Equal(Soul, Assert.Single(fight.State.DrawPile).DefId);
    }

    /// <summary>One Soul, upgraded or not — the upgrade is all block.</summary>
    [Fact]
    public void UpgradingDoesNotMakeMoreSouls()
    {
        var fight = Fight.Hand(new CardInstance(GraveWarden, true)).Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Single(fight.State.DrawPile);
        Assert.False(fight.State.DrawPile[0].Upgraded);
    }

    private const int Soul = 446;
}

// MegaCrit.Sts2.Core.Models.Cards/RightHandHand.cs: OstyDamage 4 upgrading by 2 inside the
// missing-Osty guard, and an `AfterCardPlayedLate` that pulls every copy in the DISCARD
// pile back to hand once a play has spent `EnergyVar(2)` or more. On the card, so it comes
// back for whatever was expensive rather than for itself — and it costs nothing, so it can
// never bring itself back. The emulator had only the attack.
public class RightHandHandTests
{
    private const int RightHandHand = 398;
    private const int Reap = 384; // costs 3
    private const int StrikeNecrobinder = 473; // costs 1

    private static Fight Discarded()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.DiscardPile.Add(new CardInstance(RightHandHand, false));
        return fight;
    }

    [Fact]
    public void AnExpensivePlayPullsItBack()
    {
        var fight = Discarded();
        fight.State.Hand.Add(new CardInstance(Reap, false));

        fight.Play();

        Assert.Contains(fight.State.Hand, c => c.DefId == RightHandHand);
        Assert.DoesNotContain(fight.State.DiscardPile, c => c.DefId == RightHandHand);
    }

    [Fact]
    public void ACheapPlayLeavesItThere()
    {
        var fight = Discarded();
        fight.State.Hand.Add(new CardInstance(StrikeNecrobinder, false));

        fight.Play();

        Assert.Contains(fight.State.DiscardPile, c => c.DefId == RightHandHand);
        Assert.Empty(fight.State.Hand);
    }

    /// <summary>Every copy in the pile comes back, not just the first.</summary>
    [Fact]
    public void EveryCopyComesBack()
    {
        var fight = Discarded();
        fight.State.DiscardPile.Add(new CardInstance(RightHandHand, false));
        fight.State.Hand.Add(new CardInstance(Reap, false));

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count(c => c.DefId == RightHandHand));
    }

    /// <summary>It costs nothing, so playing it can never recall it.</summary>
    [Fact]
    public void ItCannotRecallItself()
    {
        var fight = Fight.Hand(new CardInstance(RightHandHand, false)).Energy(9).Enemy(hp: 500);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Single(fight.State.DiscardPile);
    }

    [Fact]
    public void ItIsStillAnOstyAttack()
    {
        var fight = Fight.Hand(new CardInstance(RightHandHand, false)).Energy(9).Enemy(hp: 500);
        Sts2Emulator.Core.Effects.CardEffects.SummonOsty(fight.State, 10);

        fight.Play();

        Assert.Equal(496, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Sow.cs: `TargetingAllOpponents`, 8 upgrading by 3, and
// Retain. It shared the single-target Strike body — the second card in that stack to turn
// out to be an all-enemies attack, after Banshee's Cry.
public class SowTests
{
    private const int Sow = 449;

    [Fact]
    public void ItHitsEveryEnemyForEight()
    {
        var fight = Fight.Hand(new CardInstance(Sow, false)).Energy(9).Enemy(hp: 200).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(192, fight.Enemy0.Hp);
        Assert.Equal(192, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedItHitsForEleven()
    {
        var fight = Fight.Hand(new CardInstance(Sow, true)).Energy(9).Enemy(hp: 200).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(189, fight.Enemy0.Hp);
        Assert.Equal(189, fight.Enemy1.Hp);
    }

    [Fact]
    public void ItIsRetained()
    {
        Assert.True(new CardInstance(Sow, false).IsRetained());
    }
}
