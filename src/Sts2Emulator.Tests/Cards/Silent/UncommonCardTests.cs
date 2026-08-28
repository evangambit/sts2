using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Silent's twelve unpinned uncommons, read against
// MegaCrit.Sts2.Core.Models.Cards/*.cs. Six were wrong.

public class BubbleBubbleTests
{
    // PowerVar<PoisonPower>(9m) +3, and `if (cardPlay.Target.HasPower<PoisonPower>())`
    // gates the whole effect: on a clean enemy the card does nothing at all.
    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public void PoisonsAnAlreadyPoisonedTarget(bool upgraded, int poison)
    {
        var fight = Fight.Hand(Card(SI.BubbleBubble, upgraded)).Energy(1).Enemy(hp: 60);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Poison, 2);

        fight.Play();

        Assert.Equal(2 + poison, fight.EnemyBuffAmount(BuffId.Poison));
    }

    [Fact]
    public void ItDoesNothingToAnUnpoisonedTarget()
    {
        var fight = Fight.Hand(Card(SI.BubbleBubble)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));
    }
}

public class CalculatedGambleTests
{
    // Discards the hand and draws that many. `CardCmd.DiscardAndDraw` is the same
    // chokepoint Sly rides on, so a Tactician thrown away by a Gamble PLAYS.
    [Fact]
    public void DiscardsTheHandAndDrawsAsMany()
    {
        var fight = Fight
            .Hand(Card(SI.CalculatedGamble), Card(SI.StrikeSilent), Card(SI.DefendSilent))
            .Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count);
        Assert.All(fight.State.Hand, c => Assert.Equal(SI.Backstab, c.DefId));
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.StrikeSilent);
    }

    /// <summary>The discard is a `CardCmd.Discard`, so Sly fires — the shared chokepoint earning its keep.</summary>
    [Fact]
    public void ASlyCardInTheHandIsPlayedRatherThanDiscarded()
    {
        var fight = Fight.Hand(Card(SI.CalculatedGamble), Card(SI.Tactician)).Energy(1);
        fight.State.DrawPile.Clear();
        int energyBefore = fight.State.Energy;

        fight.Play();

        Assert.Equal(energyBefore + 1, fight.State.Energy);
    }

    /// <summary>`OnUpgrade` adds Retain — so the upgraded copy is not thrown away by its own effect.</summary>
    [Fact]
    public void TheUpgradeGrantsRetain()
    {
        Assert.True(GeneratedData.Cards.Get(SI.CalculatedGamble).RetainWhenUpgraded);
    }
}

/// <summary>
/// Escape Plan blocks only if the card it DREW is a Skill.
/// </summary>
/// <remarks>
/// `(await CardPileCmd.Draw(...)).FirstOrDefault()` and a type check on what came back.
/// The emulator peeked at `DrawPile[0]` before drawing — the same card only while the
/// draw pile is non-empty. With an empty pile the draw reshuffles the discard and the peek
/// describes a card that is no longer there.
/// </remarks>
public class EscapePlanTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public void ASkillDrawnGivesBlock(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.EscapePlan, upgraded)).Energy(0);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.DefendSilent, false));

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }

    [Fact]
    public void AnAttackDrawnGivesNothing()
    {
        var fight = Fight.Hand(Card(SI.EscapePlan)).Energy(0);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>
    /// With an empty draw pile the discard reshuffles in, and the block follows what was
    /// actually drawn. Peeking at the draw pile answers this one wrong in both directions.
    /// </summary>
    [Fact]
    public void ItReadsTheCardDrawnFromAReshuffle()
    {
        var fight = Fight.Hand(Card(SI.EscapePlan)).Energy(0);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(SI.DefendSilent, false));

        fight.Play();

        Assert.Equal([SI.DefendSilent], fight.State.Hand.Select(c => c.DefId));
        Assert.Equal(3, fight.State.PlayerBlock);
    }

    /// <summary>And with nothing anywhere to draw, there is no card and so no block.</summary>
    [Fact]
    public void NothingToDrawGivesNoBlock()
    {
        var fight = Fight.Hand(Card(SI.EscapePlan)).Energy(0);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Clear();

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}

public class ExpertiseTests
{
    // CardsVar(6) +1, and it draws `max(0, 6 - hand.Count)` -- a top-up, not a draw.
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 7)]
    public void FillsTheHandToItsNumber(bool upgraded, int target)
    {
        var fight = Fight.Hand(Card(SI.Expertise, upgraded), Card(SI.StrikeSilent)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 12; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(target, fight.State.Hand.Count);
    }

    /// <summary>A hand already at the number draws nothing, rather than going negative.</summary>
    [Fact]
    public void AFullHandDrawsNothing()
    {
        var fight = Fight
            .Hand(
                Card(SI.Expertise),
                Card(SI.StrikeSilent),
                Card(SI.StrikeSilent),
                Card(SI.StrikeSilent),
                Card(SI.StrikeSilent),
                Card(SI.StrikeSilent),
                Card(SI.StrikeSilent)
            )
            .Energy(1);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play();

        Assert.Equal(6, fight.State.Hand.Count);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }
}

public class FootworkTests
{
    // PowerVar<DexterityPower>(2m) +1.
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void GrantsDexterity(bool upgraded, int dexterity)
    {
        var fight = Fight.Hand(Card(SI.Footwork, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(dexterity, fight.PlayerBuffAmount(BuffId.Dexterity));
    }
}

/// <summary>
/// Infinite Blades makes a Shiv BEFORE the hand is drawn, not after.
/// </summary>
/// <remarks>
/// `InfiniteBladesPower.BeforeHandDraw`. The emulator added its Shivs at the end of the
/// turn-start sequence, which is invisible until the hand limit bites and then inverts
/// which cards get cut: the game cuts the DRAW, the emulator cut the Shivs.
/// </remarks>
public class InfiniteBladesTests
{
    [Fact]
    public void AShivArrivesEveryTurn()
    {
        var fight = Fight.Hand(Card(SI.InfiniteBlades)).Energy(1);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.InfiniteBlades));

        fight.EndTurn();
        Assert.Equal(1, fight.State.Hand.Count(c => c.DefId == SI.Shiv));

        fight.State.Hand.Clear();
        fight.EndTurn();
        Assert.Equal(1, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
    }

    /// <summary>
    /// At the hand limit the Shiv takes its slot first and the DRAW is what gets cut.
    /// This is the only place the ordering is visible, and it is the reason to get it
    /// right.
    /// </summary>
    [Fact]
    public void TheShivTakesItsSlotBeforeTheDraw()
    {
        var fight = Fight.Hand(Card(SI.InfiniteBlades)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.Play();

        // Nine cards retained into the next turn leaves exactly one slot.
        fight.State.Hand.Clear();
        for (int i = 0; i < 9; i++)
        {
            fight.State.Hand.Add(new CardInstance(SI.Snakebite, false)); // Retain
        }

        fight.State.DrawPile.Clear();
        for (int i = 0; i < 10; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.EndTurn();

        Assert.Equal(10, fight.State.Hand.Count);
        Assert.Equal(1, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }

    [Fact]
    public void TheUpgradeMakesItInnate()
    {
        Assert.True(GeneratedData.Cards.Get(SI.InfiniteBlades).InnateWhenUpgraded);
    }
}

public class LegSweepTests
{
    /// <summary>
    /// `BlockVar(11m)` +3 and `PowerVar&lt;WeakPower&gt;(2m)` +1. The emulator gave Weak
    /// 3/4 — a whole extra stack at both levels, on a card whose block half was right.
    /// </summary>
    [Theory]
    [InlineData(false, 11, 2)]
    [InlineData(true, 14, 3)]
    public void BlocksAndWeakens(bool upgraded, int block, int weak)
    {
        var fight = Fight.Hand(Card(SI.LegSweep, upgraded)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
    }
}

public class ReflexTests
{
    // CardsVar(2) +1, 3-cost, Sly.
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Draws(bool upgraded, int cards)
    {
        var fight = Fight.Hand(Card(SI.Reflex, upgraded)).Energy(3);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal(cards, fight.State.Hand.Count);
    }
}

/// <summary>
/// Scare is Weak 1 on every enemy. It was sharing Haze's arm and applying POISON.
/// </summary>
/// <remarks>
/// Two cards that looked alike got one case body, and the body belonged to the other one —
/// so a 0-cost Weak card was handing out 4 Poison. The Weak is a literal `1m` in the loop
/// with no var behind it, and `OnUpgrade` only does `RemoveKeyword(CardKeyword.Exhaust)`,
/// so the upgrade changes nothing about what it applies.
/// </remarks>
public class ScareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WeakensEveryEnemyByOne(bool upgraded)
    {
        var fight = Fight.Hand(Card(SI.Scare, upgraded)).Energy(0).Enemy(hp: 60);
        fight.Enemy(hp: 60);

        fight.Play();

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 1));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Poison));
    }

    /// <summary>The upgrade removes Exhaust and nothing else.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = Fight.Hand(Card(SI.Scare, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == SI.Scare));
    }
}

public class TacticianTests
{
    // EnergyVar(1) +1, 3-cost, Sly. Playing it costs more than it gives -- the point is to
    // DISCARD it, which SlyTests covers.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void GivesEnergy(bool upgraded, int energy)
    {
        var fight = Fight.Hand(Card(SI.Tactician, upgraded)).Energy(3);

        fight.Play();

        Assert.Equal(3 - 3 + energy, fight.State.Energy);
    }
}

/// <summary>
/// Up My Sleeve makes three Shivs, and gets a point cheaper every time it is played.
/// </summary>
/// <remarks>
/// `CardsVar(3)` — the emulator was a Shiv short at both levels — and
/// `base.EnergyCost.AddThisCombat(-1)` at the end of `OnPlay`, which is the card's whole
/// shape: a 2-cost that becomes a 1-cost and then a free one. The comment beside the line
/// already said the cost "drops in combat in real game" and no code did it.
/// </remarks>
public class UpMySleeveTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void MakesThreeShivs(bool upgraded, int shivs)
    {
        var fight = Fight.Hand(Card(SI.UpMySleeve, upgraded)).Energy(2);

        fight.Play();

        Assert.Equal(shivs, fight.State.Hand.Count(c => c.DefId == SI.Shiv));
    }

    /// <summary>
    /// The bump rides on the CARD through the piles, as Frantic Escape's does in the other
    /// direction — so the second play costs 1 and the third is free.
    /// </summary>
    [Fact]
    public void EveryPlayMakesItCheaper()
    {
        var fight = Fight.Hand(Card(SI.UpMySleeve)).Energy(9);

        int before = fight.State.Energy;
        fight.Play();
        Assert.Equal(before - 2, fight.State.Energy);

        var played = fight.State.DiscardPile.Single(c => c.DefId == SI.UpMySleeve);
        Assert.Equal(-1, played.CostBump);

        fight.State.Hand.Add(played);
        before = fight.State.Energy;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.UpMySleeve));

        Assert.Equal(before - 1, fight.State.Energy);
    }

    /// <summary>And it is per-copy: a second Up My Sleeve in the deck is still full price.</summary>
    [Fact]
    public void ASecondCopyIsStillFullPrice()
    {
        var fight = Fight.Hand(Card(SI.UpMySleeve), Card(SI.UpMySleeve)).Energy(9);
        fight.Play();

        int before = fight.State.Energy;
        fight.Play();

        Assert.Equal(before - 2, fight.State.Energy);
    }
}

/// <summary>
/// Well-Laid Plans keeps CHOSEN cards, every turn, for the rest of the combat.
/// </summary>
/// <remarks>
/// `WellLaidPlansPower.BeforeFlushLate` raises a card-selection screen over the hand — min
/// 0, max Amount, filtered to cards not already retaining — and gives each pick
/// `GiveSingleTurnRetain()`. The emulator applied `BuffId.RetainHand`, which keeps the
/// WHOLE hand and counts down: 1 meant "keep everything for one turn, then nothing". Two
/// different cards, and the real one keeps less and keeps it forever.
///
/// It is the first selection the emulator raises outside a card play, and the first that
/// may be DECLINED — the screen's minimum is zero, so the action space offers a skip one
/// past the last candidate.
/// </remarks>
public class WellLaidPlansTests
{
    // Whether a card SURVIVED the flush is read off the next hand, so the draw pile has
    // to be deep enough that the discard is never reshuffled back into it -- otherwise a
    // card that was correctly flushed is drawn again and reads as retained.
    private static Fight WithPower(params CardInstance[] rest)
    {
        var fight = Fight.Hand([Card(SI.WellLaidPlans), .. rest]).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        return fight;
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ItGrantsItsOwnPowerAndNotWholeHandRetain(bool upgraded, int picks)
    {
        var fight = Fight.Hand(Card(SI.WellLaidPlans, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(picks, fight.PlayerBuffAmount(BuffId.WellLaidPlans));
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.RetainHand));
    }

    /// <summary>Ending the turn asks which card to keep, rather than keeping them all.</summary>
    [Fact]
    public void EndingTheTurnAsksWhichCardToKeep()
    {
        var fight = WithPower(Card(SI.Backstab), Card(SI.Slice));
        fight.Play();

        fight.EndTurn();

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.RetainForNextTurn, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
        // The turn has not ended yet -- it is owed until the screen is answered.
        Assert.True(fight.State.PlayerTurn);
    }

    /// <summary>
    /// The chosen card survives the flush and the rest do not, and the end turn it was
    /// holding up then runs.
    /// </summary>
    [Fact]
    public void TheChosenCardSurvivesAndTheRestDoNot()
    {
        var fight = WithPower(Card(SI.Backstab), Card(SI.Slice));
        fight.Play();
        fight.EndTurn();

        fight.Choose(fight.Pending!.Candidates.IndexOf(1)); // the Slice, not the Backstab

        Assert.Null(fight.Pending);
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Slice);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }

    /// <summary>
    /// The grant is for ONE flush, so a card kept this turn is offered again next turn
    /// rather than sticking permanently.
    /// </summary>
    [Fact]
    public void TheGrantLastsOneTurnAndThePowerAsksAgain()
    {
        var fight = WithPower(Card(SI.Backstab));
        fight.Play();
        fight.EndTurn();
        fight.Choose(0);

        var kept = fight.State.Hand.Single(c => c.DefId == SI.Backstab);
        Assert.False(kept.RetainThisTurn);

        fight.EndTurn();

        Assert.NotNull(fight.Pending);
        Assert.Contains(SI.Backstab, fight.Pending!.Candidates.Select(i => fight.State.Hand[i].DefId));
    }

    /// <summary>
    /// Keeping NOTHING is a legal answer — the screen's minimum is zero. The skip is the
    /// action one past the last candidate.
    /// </summary>
    [Fact]
    public void TheScreenCanBeDeclined()
    {
        var fight = WithPower(Card(SI.Backstab));
        fight.Play();
        fight.EndTurn();

        int skip = fight.Pending!.Candidates.Count;
        Assert.Contains(skip, CombatEngine.ValidActions(fight.State));

        fight.Choose(skip);

        Assert.Null(fight.Pending);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Backstab);
    }

    /// <summary>An upgraded copy asks twice, and both picks are kept.</summary>
    [Fact]
    public void UpgradedItAsksTwice()
    {
        var fight = Fight
            .Hand(
                Card(SI.WellLaidPlans, upgraded: true),
                Card(SI.Backstab),
                Card(SI.Slice),
                Card(SI.Deflect)
            )
            .Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 30; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();
        fight.EndTurn();

        fight.Choose(0); // the Backstab
        Assert.NotNull(fight.Pending);
        fight.Choose(0); // the Slice

        Assert.Null(fight.Pending);
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Backstab);
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Slice);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Deflect);
    }

    /// <summary>
    /// A card that already retains is not offered — the power's own `RetainFilter` is
    /// `!card.ShouldRetainThisTurn`, so a pick is never spent on a card that was safe.
    /// </summary>
    [Fact]
    public void AlreadyRetainingCardsAreNotOffered()
    {
        var fight = WithPower(Card(SI.Snakebite), Card(SI.Backstab));
        fight.Play();

        fight.EndTurn();

        // Snakebite carries Retain; only the Backstab is worth a pick.
        Assert.Equal([1], fight.Pending!.Candidates);
    }

    /// <summary>With nothing to offer the turn simply ends, and no screen is raised.</summary>
    [Fact]
    public void AnEmptyHandRaisesNoScreen()
    {
        var fight = Fight.Hand(Card(SI.WellLaidPlans)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        fight.Play();

        fight.EndTurn();

        Assert.Null(fight.Pending);
    }
}
