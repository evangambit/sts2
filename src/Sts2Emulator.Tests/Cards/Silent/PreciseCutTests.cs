using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack. MegaCrit.Sts2.Core.Models.Cards/PreciseCut.cs: CalculationBaseVar(13m)
// with ExtraDamageVar(2m) times MINUS the number of other cards in hand (the multiplier
// subtracts the card itself while it is still in hand); OnUpgrade raises the base to 16.
public class PreciseCutTests
{
    [Fact]
    public void DealsThirteenAsTheOnlyCardInHand()
    {
        var fight = Fight.Hand(Card(SI.PreciseCut)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(27, fight.Enemy0.Hp);
    }

    [Fact]
    public void LosesTwoDamagePerOtherCardInHand()
    {
        var fight = Fight
            .Hand(Card(SI.PreciseCut), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        // 13 - 2 x 2
        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedStartsFromSixteen()
    {
        var fight = Fight
            .Hand(Card(SI.PreciseCut, upgraded: true), Card(IC.Bash))
            .Energy(1)
            .Enemy(hp: 40);

        // 16 - 2 x 1
        fight.Play();

        Assert.Equal(26, fight.Enemy0.Hp);
    }

    [Fact]
    public void NeverHealsTheEnemyWithAFullHand()
    {
        var fight = Fight
            .Hand(
                Card(SI.PreciseCut),
                Card(IC.Bash),
                Card(IC.Bash),
                Card(IC.Bash),
                Card(IC.Bash),
                Card(IC.Bash),
                Card(IC.Bash),
                Card(IC.Bash)
            )
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }
}
