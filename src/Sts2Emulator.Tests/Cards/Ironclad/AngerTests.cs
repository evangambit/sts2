using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Anger.cs: DamageVar(6m),
// OnUpgrade UpgradeValueBy(2m), then CreateClone() added to PileType.Discard.
public class AngerTests
{
    [Fact]
    public void DealsSixAndAddsACopyToTheDiscardPile()
    {
        var fight = Fight.Hand(Card(IC.Anger)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
        // The played card and its clone: two Angers in the discard pile.
        Assert.Equal(2, fight.State.DiscardPile.Count(card => card.DefId == IC.Anger));
    }

    [Fact]
    public void UpgradedDealsEight()
    {
        var fight = Fight.Hand(Card(IC.Anger, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheCopyKeepsTheUpgrade()
    {
        var fight = Fight.Hand(Card(IC.Anger, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.All(
            fight.State.DiscardPile.Where(card => card.DefId == IC.Anger),
            card => Assert.True(card.Upgraded)
        );
    }

    /// <summary>
    /// `CreateClone()` is `CardScope.CloneCard(this)` — the whole card. Building the copy
    /// from the id and the upgrade flag drops everything else, so an enchanted Anger used
    /// to copy itself back stripped of the thing that made it worth copying.
    /// </summary>
    [Fact]
    public void TheCopyKeepsTheEnchantment()
    {
        var anger = new CardInstance(IC.Anger, false) with
        {
            Enchantment = Enchantment.Sharp,
            EnchantAmount = 4,
        };
        var fight = Fight.Hand(anger).Energy(3).Enemy(hp: 60);

        fight.Play(0);

        // Two Angers: the one played and the clone it made. Both carry the enchantment.
        var angers = fight.State.DiscardPile.Where(c => c.DefId == IC.Anger).ToList();
        Assert.Equal(2, angers.Count);
        Assert.All(angers, c => Assert.Equal(Enchantment.Sharp, c.Enchantment));
        Assert.All(angers, c => Assert.Equal(4, c.EnchantAmount));
    }

    /// <summary>
    /// A copy is a card in a pile, not a card mid-play: the per-play flags do not survive
    /// it, or a free Anger would seed a free one into the deck.
    /// </summary>
    [Fact]
    public void TheCopyIsNotStillFreeForTheTurn()
    {
        var anger = new CardInstance(IC.Anger, false) with { FreeThisTurn = true };
        var fight = Fight.Hand(anger).Energy(3).Enemy(hp: 60);

        fight.Play(0);

        Assert.All(
            fight.State.DiscardPile.Where(c => c.DefId == IC.Anger),
            c => Assert.False(c.FreeThisTurn)
        );
    }
}
