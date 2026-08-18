using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Rampage.cs: DamageVar(9m) plus an
// "Increase" of 5 (9 upgraded) that is added to the card's own damage on every play and
// persists for the rest of the combat.
//
// The emulator does not model that growth — CardEffects calls it out as "approx: static
// base" — so every play deals the printed damage. These tests pin the first play, which
// is correct either way, rather than asserting the missing growth is right.
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
    public void GoesToTheDiscardPileToBeDrawnAgain()
    {
        var fight = Fight.Hand(Card(IC.Rampage)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Rampage], Fight.Ids(fight.State.DiscardPile));
    }
}
