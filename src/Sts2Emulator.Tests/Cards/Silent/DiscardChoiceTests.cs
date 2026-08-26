using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The five Silent cards that discard a CHOSEN card. Every one of them used to discard the
// FIRST card in hand — `DiscardFirstCardsFromHand` — which is the same picking-for-the-
// player that E104 found in the Knowledge Demon's curse screen. Choosing what to throw
// away is the entire point of Survivor, and an agent told the leftmost card goes learns a
// rule the game does not have.
//
// All five raise `CardSelectionKind.DiscardFromHandRepeated`, which reopens until its
// picks are spent or the hand empties — Purity's shape.

public class SurvivorTests
{
    // MegaCrit.Sts2.Core.Models.Cards/Survivor.cs: BlockVar(8m), OnUpgrade +3, then
    // CardSelectCmd.FromHandForDiscard with a prompt for 1.
    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 11)]
    public void BlocksThenAsksWhichCardToDiscard(bool upgraded, int block)
    {
        var fight = Fight
            .Hand(Card(SI.Survivor, upgraded), Card(SI.StrikeSilent), Card(SI.DefendSilent))
            .Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.DiscardFromHandRepeated, fight.Pending!.Kind);
        Assert.Equal(SI.Survivor, fight.Pending.SourceCardDefId);
        // Both cards left in hand are offered, not just the first.
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    /// <summary>
    /// The answer is the player's: picking the SECOND candidate discards the second card,
    /// which the old behaviour could not express.
    /// </summary>
    [Fact]
    public void TheChosenCardIsTheOneDiscarded()
    {
        var fight = Fight
            .Hand(Card(SI.Survivor), Card(SI.StrikeSilent), Card(SI.DefendSilent))
            .Energy(1);
        fight.Play();

        fight.Choose(1); // the Defend, not the Strike

        Assert.Null(fight.Pending);
        Assert.Equal([SI.StrikeSilent], fight.State.Hand.Select(c => c.DefId));
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.DefendSilent);
    }

    /// <summary>An empty hand raises no screen at all, and the block still lands.</summary>
    [Fact]
    public void WithNothingLeftToDiscardItSimplyBlocks()
    {
        var fight = Fight.Hand(Card(SI.Survivor)).Energy(1);

        fight.Play();

        Assert.Null(fight.Pending);
        Assert.Equal(8, fight.State.PlayerBlock);
    }
}

public class AcrobaticsTests
{
    // CardsVar(3), OnUpgrade +1: draw that many, THEN discard one chosen card. The draw
    // comes first, so a card drawn by the Acrobatics is a candidate for its own discard.
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void DrawsThenAsks(bool upgraded, int draw)
    {
        var fight = Fight.Hand(Card(SI.Acrobatics, upgraded)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();

        Assert.Equal(draw, fight.State.Hand.Count);
        Assert.Equal(draw, fight.Pending!.Candidates.Count);
    }
}

public class DaggerThrowTests
{
    // DamageVar(9m); damage, then draw 1, then discard one chosen card.
    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public void HitsDrawsThenAsks(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.DaggerThrow, upgraded)).Energy(1).Enemy(hp: 40);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.DefendSilent, false));

        fight.Play();

        Assert.Equal(40 - damage, fight.Enemy0.Hp);
        // The drawn card is in hand and is the thing being asked about.
        Assert.Equal([SI.DefendSilent], fight.State.Hand.Select(c => c.DefId));
        Assert.Equal(1, fight.Pending!.Candidates.Count);
    }
}

public class PreparedTests
{
    // CardsVar(1), OnUpgrade +1. Draws that many and discards that many CHOSEN cards, so
    // upgraded it asks TWICE -- the repeated shape, not one pick.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void DrawsThenAsksOncePerCard(bool upgraded, int count)
    {
        var fight = Fight.Hand(Card(SI.Prepared, upgraded)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.StrikeSilent, false));
        }

        fight.Play();

        int asked = 0;
        while (fight.Pending is not null)
        {
            asked++;
            fight.Choose(0);
        }

        Assert.Equal(count, asked);
        Assert.Equal(count, fight.State.DiscardPile.Count(c => c.DefId == SI.StrikeSilent));
    }
}

public class HiddenDaggersTests
{
    // CardsVar(2) and Shivs = 2. The card deals NO damage: it discards two CHOSEN cards
    // and then creates two Shivs. Upgrading leaves the count at two and UPGRADES the
    // Shivs. The emulator dealt the CardsVar as damage, added a third Shiv on upgrade and
    // discarded nothing at all.
    [Fact]
    public void ItDiscardsTwoChosenCardsAndDealsNoDamage()
    {
        var fight = Fight
            .Hand(
                Card(SI.HiddenDaggers),
                Card(SI.StrikeSilent),
                Card(SI.DefendSilent),
                Card(SI.Neutralize)
            )
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal(3, fight.Pending!.Candidates.Count);

        fight.Choose(0);
        Assert.NotNull(fight.Pending); // asks a second time
        fight.Choose(0);

        Assert.Null(fight.Pending);
        // The Strike and the Defend, chosen in that order. The Hidden Daggers itself is in
        // the discard too, which is why this counts the two rather than the pile.
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.StrikeSilent);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.DefendSilent);
        Assert.Equal([SI.Neutralize, SI.Shiv, SI.Shiv], fight.State.Hand.Select(c => c.DefId));
    }

    /// <summary>
    /// The Shivs arrive AFTER the discard, so neither of them can be discarded by the card
    /// that made them — which is why the selection carries a follow-up rather than the
    /// call site adding them up front.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoShivsArriveAfterwardsAndTheUpgradeUpgradesThem(bool upgraded)
    {
        var fight = Fight
            .Hand(Card(SI.HiddenDaggers, upgraded), Card(SI.StrikeSilent), Card(SI.DefendSilent))
            .Energy(1);

        fight.Play();
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Shiv);

        fight.Choose(0);
        fight.Choose(0);

        var shivs = fight.State.Hand.Where(c => c.DefId == SI.Shiv).ToList();
        Assert.Equal(2, shivs.Count);
        Assert.All(shivs, s => Assert.Equal(upgraded, s.Upgraded));
    }
}
