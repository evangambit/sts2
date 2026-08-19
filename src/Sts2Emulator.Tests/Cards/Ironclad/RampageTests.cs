using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Rampage.cs: DamageVar(9m) plus an
// "Increase" of 5 (9 upgraded) that is added to the card's own damage on every play and
// persists for the rest of the combat.
//
// The growth lives on the copy that was played, so it rides into the discard pile and
// comes back with it, and a second Rampage in the deck grows on its own schedule.
public class RampageTests
{
    [Fact]
    public void DealsNineOnItsFirstPlay()
    {
        var fight = Fight.Hand(Card(IC.Rampage)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradeChangesTheGrowthRatherThanTheBaseDamage()
    {
        var fight = Fight.Hand(Card(IC.Rampage, upgraded: true)).Energy(1).Enemy(hp: 40);

        // OnUpgrade raises "Increase" by 4 and leaves DamageVar at 9.
        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void GainsFiveDamageForItsNextPlay()
    {
        var fight = Fight.Hand(Card(IC.Rampage), Card(IC.Rampage)).Energy(9).Enemy(hp: 90);

        fight.Play(index: 0);

        // The copy in hand is untouched; only the one that was played grew.
        Assert.Equal(81, fight.Enemy0.Hp);
        Assert.Equal(5, fight.State.DiscardPile[0].BonusDamage);
        Assert.Equal(0, fight.State.Hand[0].BonusDamage);
    }

    [Fact]
    public void HitsHarderEachTimeTheSameCopyIsPlayed()
    {
        var fight = Fight.Hand(Card(IC.Rampage)).Energy(9).Enemy(hp: 90);

        // Play it, draw it back, play it again: 9 then 14.
        fight.Play();
        var grown = fight.State.DiscardPile[0];
        fight.State.DiscardPile.Clear();
        fight.State.Hand.Add(grown);
        fight.Play();

        Assert.Equal(67, fight.Enemy0.Hp);
        Assert.Equal(10, fight.State.DiscardPile[0].BonusDamage);
    }

    [Fact]
    public void UpgradedGrowsByNine()
    {
        var fight = Fight.Hand(Card(IC.Rampage, upgraded: true)).Energy(9).Enemy(hp: 90);

        fight.Play();

        Assert.Equal(9, fight.State.DiscardPile[0].BonusDamage);
    }

    [Fact]
    public void GoesToTheDiscardPileToBeDrawnAgain()
    {
        var fight = Fight.Hand(Card(IC.Rampage)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Rampage], Fight.Ids(fight.State.DiscardPile));
    }
}
