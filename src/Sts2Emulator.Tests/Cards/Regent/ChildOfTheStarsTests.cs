using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/ChildOfTheStars.cs: a "BlockForStars" var of 2, upgrading
// by 1. `ChildOfTheStarsPower.AfterStarsSpent` gives that much Unpowered block PER STAR
// spent — and that hook fires from `CardModel.SpendStars` alone, which is a card paying its
// star COST, not every way stars can leave the counter.
//
// The emulator gave a flat 1/2 Strength.
public class ChildOfTheStarsTests
{
    private const int ChildOfTheStars = 85;
    private const int FallingStar = 179; // costs 2 stars
    private const int Alignment = 11; // costs 3 stars
    private const int StrikeRegent = 474; // costs none

    private static Fight Armed(bool upgraded = false, int stars = 9)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = stars;
        fight.State.Hand.Add(new CardInstance(ChildOfTheStars, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGainsNoBlockByItself()
    {
        var fight = Armed();

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ChildOfTheStars));
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TwoBlockPerStarSpent()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(FallingStar, false));

        fight.Play(0, target: 0);

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeIsThreePerStar()
    {
        var fight = Armed(upgraded: true);
        fight.State.Hand.Add(new CardInstance(Alignment, false));

        fight.Play(0);

        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void ACardWithNoStarCostGainsNothing()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
