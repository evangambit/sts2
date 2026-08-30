using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Relics/BagOfPreparation.cs: `ModifyHandDraw` returns
// `count + CardsVar(2)` while TurnNumber is 1 — the OPENING HAND is seven cards, not five
// followed by a separate draw of two.
//
// The emulator drew its two through a `DrawCards` at COMBAT START, which runs after the
// opening hand is already dealt. Same seven cards, and two things wrong with them: the
// pair were not part of the hand draw, so the hooks that fire only on EXTRA draws saw them
// (E329), and the opening-hand size feeds `ApplyTurnOneDrawPileReorder`, which decides how
// many Innate cards the hand is guaranteed to hold.
//
// Ring of the Snake is the SAME mechanic and was already modelled the other way. One of
// the two had to be wrong.
public class BagOfPreparationTests
{
    [Fact]
    public void ItOpensOnSevenCards()
    {
        var plain = Fight.WithRelics();
        var withBag = Fight.WithRelics(RelicEffects.BagOfPreparation);

        Assert.Equal(plain.State.Hand.Count + 2, withBag.State.Hand.Count);
    }

    /// <summary>
    /// The two are part of the HAND DRAW, so nothing that counts extra draws sees them.
    /// Under the old model this read 2.
    /// </summary>
    [Fact]
    public void TheTwoAreNotCountedAsExtraDraws()
    {
        var withBag = Fight.WithRelics(RelicEffects.BagOfPreparation);

        Assert.Equal(0, withBag.State.ExtraCardsDrawnThisTurn);
    }

    /// <summary>It pays on turn one and never again.</summary>
    [Fact]
    public void ItDoesNotPayOnLaterTurns()
    {
        var plain = Fight.WithRelics();
        var withBag = Fight.WithRelics(RelicEffects.BagOfPreparation);

        plain.EndTurn();
        withBag.EndTurn();

        Assert.Equal(plain.State.Hand.Count, withBag.State.Hand.Count);
    }
}

// MegaCrit.Sts2.Core.Models.Relics/MummifiedHand.cs: after a POWER, one card in hand is
// made free for the turn. Its first two filters are
// `EnergyCost.GetWithModifiers(None) > 0 || BaseStarCost > 0` and
// `CostsEnergyOrStars(includeGlobalModifiers: true)` — BOTH resources.
//
// The emulator read energy only. That is not a Regent-shaped detail so much as a
// Regent-shaped blind spot: most of the character's cards cost 0 energy and several stars,
// so a whole hand read as free and the relic fell through to its last-resort branch.
public class MummifiedHandTests
{
    private const int Accuracy = 3; // a Power, to trigger the relic
    private const int Devastate = 143; // 1 energy, 4 stars
    private const int Radiate = 377; // 0 energy, no star cost
    private const int SevenStars = 422; // 2 energy, 7 stars

    private static Fight WithHand(params int[] hand)
    {
        var fight = Fight.WithRelics(RelicEffects.MummifiedHand).Energy(9).Enemy(hp: 500);
        fight.State.Hand.Clear();
        foreach (int id in hand)
        {
            fight.State.Hand.Add(new CardInstance(id, false));
        }

        return fight;
    }

    /// <summary>
    /// A card that costs only STARS is printed-costed, so it is in the preferred pool —
    /// and with a zero-energy zero-star card beside it, the star card is the only
    /// candidate and must be the one that goes free.
    /// </summary>
    [Fact]
    public void AStarOnlyCardCountsAsCosting()
    {
        var fight = WithHand(Radiate, SevenStars);
        fight.State.Hand.Add(new CardInstance(Accuracy, false));

        fight.Play(2);

        var sevenStars = fight.State.Hand.Single(c => c.DefId == SevenStars);
        var radiate = fight.State.Hand.Single(c => c.DefId == Radiate);
        Assert.True(sevenStars.FreeThisTurn);
        Assert.False(radiate.FreeThisTurn);
    }

    /// <summary>An energy-costed card is still preferred, which is the half that worked.</summary>
    [Fact]
    public void AnEnergyCostedCardIsStillPreferred()
    {
        var fight = WithHand(Radiate, Devastate);
        fight.State.Hand.Add(new CardInstance(Accuracy, false));

        fight.Play(2);

        Assert.True(fight.State.Hand.Single(c => c.DefId == Devastate).FreeThisTurn);
        Assert.False(fight.State.Hand.Single(c => c.DefId == Radiate).FreeThisTurn);
    }

    /// <summary>
    /// A hand with nothing costed at all still picks a card — the last-resort branch, which
    /// is where a Regent hand used to land every time.
    /// </summary>
    [Fact]
    public void AFreeHandStillPicksSomething()
    {
        var fight = WithHand(Radiate);
        fight.State.Hand.Add(new CardInstance(Accuracy, false));

        fight.Play(1);

        Assert.True(fight.State.Hand.Single(c => c.DefId == Radiate).FreeThisTurn);
    }
}
