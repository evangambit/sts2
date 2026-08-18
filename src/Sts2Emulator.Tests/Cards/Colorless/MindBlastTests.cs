using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/MindBlast.cs: CalculationBaseVar(0m)
// plus ExtraDamageVar(1m) per card in the DRAW pile; OnUpgrade is EnergyCost.UpgradeBy(-1),
// so the upgrade is cheaper rather than stronger.
public class MindBlastTests
{
    [Fact]
    public void DealsOneDamagePerCardInTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.MindBlast))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(37, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithAnEmptyDrawPile()
    {
        var fight = Fight.Hand(Card(CL.MindBlast)).Energy(1).Draw().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedCostsZeroRatherThanHittingHarder()
    {
        var fight = Fight
            .Hand(Card(CL.MindBlast, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(38, fight.Enemy0.Hp);
        Assert.Equal(1, fight.State.Energy);
    }
}
