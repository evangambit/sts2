using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Headbutt.cs: DamageVar(9m), OnUpgrade
// UpgradeValueBy(3m), then CardSelectCmd.FromCombatPile(Discard) -> CardPileCmd.Add(
// PileType.Draw, CardPilePosition.Top). The player picks which card comes back.
public class HeadbuttTests
{
    [Fact]
    public void DealsNineAndAsksWhichDiscardedCardToRetrieve()
    {
        var fight = Fight
            .Hand(Card(IC.Headbutt))
            .Energy(1)
            .Discard(Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
        Assert.Equal(CardSelectionKind.DiscardToDrawPileTop, fight.Pending?.Kind);
        Assert.Equal(2, fight.Pending?.Candidates.Count);
    }

    [Fact]
    public void TheChosenCardGoesOnTopOfTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(IC.Headbutt))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Discard(Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play();

        // Candidate 0 is the Strike — the card the old auto-pick would never have taken.
        fight.Choose(0);

        Assert.Equal(IC.StrikeIronclad, fight.State.DrawPile[0].DefId);
        // The Bash stays put, and the played Headbutt joins it in the discard pile.
        Assert.Equal([IC.Bash, IC.Headbutt], Fight.Ids(fight.State.DiscardPile));
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void UpgradedDealsTwelve()
    {
        var fight = Fight.Hand(Card(IC.Headbutt, upgraded: true)).Energy(1).Discard().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void AsksNothingWhenTheDiscardPileIsEmpty()
    {
        var fight = Fight.Hand(Card(IC.Headbutt)).Energy(1).Discard().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
        Assert.Null(fight.Pending);
    }
}
