using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What the player is entitled to know about draw-pile order.
///
/// The pile is one ordered list, but only part of it is the player's to see: the
/// composition, plus wherever they deliberately put a card. An observation built off
/// these counters exposes exactly that much, and a determinization resamples the rest,
/// so a counter that over-claims is a determinism leak rather than a wrong number.
/// See docs/agent-interface.md.
/// </summary>
public class KnownDrawOrderTests
{
    private static CombatState PileOf(int count)
    {
        var state = new CombatState();
        for (int i = 0; i < count; i++)
        {
            state.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        }

        return state;
    }

    [Fact]
    public void AFreshPileIsEntirelyUnknown()
    {
        var state = PileOf(10);

        Assert.Equal(0, state.KnownTopCount);
        Assert.Equal(0, state.KnownBottomCount);
    }

    [Fact]
    public void TopDeckingMakesThatCardKnown()
    {
        var state = PileOf(5);

        state.TopDeck(new CardInstance(IC.Bash, false));

        Assert.Equal(1, state.KnownTopCount);
        Assert.Equal(IC.Bash, state.DrawPile[0].DefId);
    }

    [Fact]
    public void TopDeckingTwiceKeepsBothKnownInOrder()
    {
        var state = PileOf(5);

        state.TopDeck(new CardInstance(IC.Bash, false));
        state.TopDeck(new CardInstance(IC.Anger, false));

        Assert.Equal(2, state.KnownTopCount);
        Assert.Equal(IC.Anger, state.DrawPile[0].DefId);
        Assert.Equal(IC.Bash, state.DrawPile[1].DefId);
    }

    [Fact]
    public void DrawingSpendsTheKnowledgeItUsed()
    {
        var state = PileOf(5);
        state.TopDeck(new CardInstance(IC.Bash, false));

        state.RemoveFromDrawPileAt(0);

        Assert.Equal(0, state.KnownTopCount);
    }

    [Fact]
    public void DrawingPastTheKnownRegionCannotPushItNegative()
    {
        var state = PileOf(5);

        state.RemoveFromDrawPileAt(0);
        state.RemoveFromDrawPileAt(0);

        Assert.Equal(0, state.KnownTopCount);
    }

    [Fact]
    public void BottomDeckingIsKnownFromTheOtherEnd()
    {
        var state = PileOf(5);

        state.BottomDeck(new CardInstance(IC.Bash, false));

        Assert.Equal(1, state.KnownBottomCount);
        Assert.Equal(IC.Bash, state.DrawPile[^1].DefId);
    }

    [Fact]
    public void AShuffleInsideTheKnownPrefixTruncatesIt()
    {
        var state = PileOf(5);
        state.TopDeck(new CardInstance(IC.Bash, false));
        state.TopDeck(new CardInstance(IC.Anger, false));

        // A card lands between the two known ones: the first is still known, the
        // second no longer is, because something unseen now sits in front of it.
        state.InsertIntoDrawPile(1, new CardInstance(IC.Armaments, false));

        Assert.Equal(1, state.KnownTopCount);
    }

    [Fact]
    public void AShuffleBelowTheKnownPrefixLeavesItAlone()
    {
        var state = PileOf(5);
        state.TopDeck(new CardInstance(IC.Bash, false));

        state.InsertIntoDrawPile(3, new CardInstance(IC.Armaments, false));

        Assert.Equal(1, state.KnownTopCount);
    }

    [Fact]
    public void RemovingFromTheMiddleShrinksNeitherRegion()
    {
        var state = PileOf(6);
        state.TopDeck(new CardInstance(IC.Bash, false));
        state.BottomDeck(new CardInstance(IC.Anger, false));

        state.RemoveFromDrawPileAt(3);

        Assert.Equal(1, state.KnownTopCount);
        Assert.Equal(1, state.KnownBottomCount);
    }

    [Fact]
    public void RemovingTheKnownBottomCardShrinksThatRegion()
    {
        var state = PileOf(4);
        state.BottomDeck(new CardInstance(IC.Anger, false));

        state.RemoveFromDrawPileAt(state.DrawPile.Count - 1);

        Assert.Equal(0, state.KnownBottomCount);
    }

    [Fact]
    public void TheTwoRegionsNeverOverlap()
    {
        var state = PileOf(0);

        state.TopDeck(new CardInstance(IC.Bash, false));
        state.BottomDeck(new CardInstance(IC.Anger, false));
        state.TopDeck(new CardInstance(IC.Armaments, false));

        Assert.True(
            state.KnownTopCount + state.KnownBottomCount <= state.DrawPile.Count,
            $"claimed {state.KnownTopCount}+{state.KnownBottomCount} of {state.DrawPile.Count}"
        );
    }

    [Fact]
    public void AReshuffleTakesTheWholeOrderAway()
    {
        var state = PileOf(3);
        state.TopDeck(new CardInstance(IC.Bash, false));
        state.BottomDeck(new CardInstance(IC.Anger, false));
        state.DiscardPile.Add(new CardInstance(IC.Armaments, false));

        CardEffects.ShuffleDiscardIntoDraw(state, new Random(0));

        Assert.Equal(0, state.KnownTopCount);
        Assert.Equal(0, state.KnownBottomCount);
    }

    [Fact]
    public void ANewCombatStartsWithNothingKnown()
    {
        var state = CombatFactory.NewCombat(seed: 0);

        Assert.Equal(0, state.KnownTopCount);
        Assert.Equal(0, state.KnownBottomCount);
    }
}
