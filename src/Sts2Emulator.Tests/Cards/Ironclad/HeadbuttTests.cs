using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Headbutt.cs: DamageVar(9m), OnUpgrade
// UpgradeValueBy(3m), then CardSelectCmd.FromCombatPile(Discard) -> CardPileCmd.Add(
// PileType.Draw, CardPilePosition.Top).
//
// The game lets the player pick which discarded card comes back; the emulator has no
// selection screen and takes the most recently discarded one, so these tests pin the
// approximation rather than the choice.
public class HeadbuttTests
{
    [Fact]
    public void DealsNineAndPutsADiscardedCardOnTopOfTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(IC.Headbutt))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Discard(Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
        Assert.Equal(IC.Bash, fight.State.DrawPile[0].DefId);
    }

    [Fact]
    public void UpgradedDealsTwelve()
    {
        var fight = Fight
            .Hand(Card(IC.Headbutt, upgraded: true))
            .Energy(1)
            .Discard(Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheRetrievedCardLeavesTheDiscardPile()
    {
        var fight = Fight
            .Hand(Card(IC.Headbutt))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Discard(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.DoesNotContain(fight.State.DiscardPile, card => card.DefId == IC.Bash);
    }

    [Fact]
    public void StillDealsDamageWithAnEmptyDiscardPile()
    {
        var fight = Fight.Hand(Card(IC.Headbutt)).Energy(1).Discard().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }
}
