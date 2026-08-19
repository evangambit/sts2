using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/SecondWind.cs exhausts every non-Attack
// in hand and gains BlockVar(5m) for each one; OnUpgrade raises the per-card block by 2.
public class SecondWindTests
{
    [Fact]
    public void ExhaustsEveryNonAttackAndGainsFiveBlockEach()
    {
        var fight = Fight
            .Hand(Card(IC.SecondWind), Card(IC.DefendIronclad), Card(IC.ShrugItOff))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(2, fight.State.ExhaustPile.Count);
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void LeavesAttacksInHand()
    {
        var fight = Fight
            .Hand(Card(IC.SecondWind), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
        Assert.Equal([IC.DefendIronclad], Fight.Ids(fight.State.ExhaustPile));
        Assert.Equal(5, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsSevenPerCard()
    {
        var fight = Fight
            .Hand(Card(IC.SecondWind, upgraded: true), Card(IC.DefendIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(7, fight.State.PlayerBlock);
    }

    [Fact]
    public void GainsNothingWithOnlyAttacksInHand()
    {
        var fight = Fight
            .Hand(Card(IC.SecondWind), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Empty(fight.State.ExhaustPile);
    }
}
