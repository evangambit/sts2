using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `Distraction` and `Metamorphosis` shared a body with Glimpse Beyond and White Noise
/// that added ONE random card of any type to HAND, free only when upgraded. All four are
/// different cards, and the two above ran that body for real.
/// </summary>
/// <remarks>
/// Glimpse Beyond and White Noise were labelled into the same stack and are handled
/// correctly in earlier dispatches, so their labels there were DEAD — and misleading,
/// because a reader would have attributed the shared body to them. Both are removed.
///
/// The free-this-turn flag is the detail worth keeping: the shared body passed `upgraded`
/// as it, so an unupgraded Distraction gave a card that was not free at all, and the
/// upgrade — which really cuts the COST — appeared to change what you got.
/// </remarks>
public class DistractionTests
{
    private static int Id => GeneratedData.Cards.FindId("Distraction")!.Value;

    [Fact]
    public void ItAddsOneSkillToHandFree()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);

        fight.Play(0);

        var added = fight.State.Hand.Single();
        Assert.Equal(CardType.Skill, GeneratedData.Cards.Get(added.DefId).Type);
        Assert.True(added.FreeThisTurn, "the card is free whether or not Distraction was upgraded");
    }

    /// <summary>The upgrade cuts Distraction's own cost; the card it gives is free either way.</summary>
    [Fact]
    public void UpgradingChangesTheCostNotTheGift()
    {
        var plain = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        var upgraded = Fight.Hand(new CardInstance(Id, true)).Energy(9).Enemy(hp: 300);

        plain.Play(0);
        upgraded.Play(0);

        Assert.Single(plain.State.Hand);
        Assert.Single(upgraded.State.Hand);
        Assert.True(plain.State.Hand[0].FreeThisTurn);
        Assert.True(upgraded.State.Hand[0].FreeThisTurn);
    }
}

public class MetamorphosisTests
{
    private static int Id => GeneratedData.Cards.FindId("Metamorphosis")!.Value;

    /// <summary>
    /// THREE random ATTACKS into the DRAW PILE at random positions, free for the whole
    /// COMBAT. Not one card, not into hand, and not free for a turn.
    /// </summary>
    [Fact]
    public void ItBuriesThreeFreeAttacks()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);

        Assert.Equal(3, fight.State.DrawPile.Count);
        Assert.Empty(fight.State.Hand);
        Assert.All(
            fight.State.DrawPile,
            card =>
            {
                Assert.Equal(CardType.Attack, GeneratedData.Cards.Get(card.DefId).Type);
                Assert.Equal(0, card.CostForCombat);
            }
        );
    }

    /// <summary>`CardsVar(3)` upgrading by TWO — five, not four.</summary>
    [Fact]
    public void UpgradedItBuriesFive()
    {
        var fight = Fight.Hand(new CardInstance(Id, true)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);

        Assert.Equal(5, fight.State.DrawPile.Count);
    }

    /// <summary>Distinct: a shuffle-then-take, so the three are three different cards.</summary>
    [Fact]
    public void TheThreeAreDistinct()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);

        Assert.Equal(3, fight.State.DrawPile.Select(card => card.DefId).Distinct().Count());
    }
}

/// <summary>
/// `Apotheosis` upgrades `PlayerCombatState.AllCards` — Hand, Draw, Discard, EXHAUST and
/// Play, five piles. The exhaust one was missing, and it is not a technicality: cards come
/// back from exhaust, and one that comes back upgraded is a different card.
/// </summary>
public class ApotheosisTests
{
    [Fact]
    public void ItReachesTheExhaustPileToo()
    {
        int id = GeneratedData.Cards.FindId("Apotheosis")!.Value;
        var fight = Fight.Hand(new CardInstance(id, false)).Energy(9).Enemy(hp: 300);
        fight.State.ExhaustPile.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));

        fight.Play(0);

        // `allCard != this` -- Apotheosis skips itself, and it Exhausts, so it is sitting
        // in that pile unupgraded on purpose.
        Assert.All(
            fight.State.ExhaustPile.Where(card => card.DefId != id),
            card => Assert.True(card.Upgraded)
        );
        Assert.All(fight.State.DrawPile, card => Assert.True(card.Upgraded));
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == id && !card.Upgraded);
    }
}
