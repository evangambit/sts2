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
}
