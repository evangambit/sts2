using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack. MegaCrit.Sts2.Core.Models.Cards/FlashOfSteel.cs: DamageVar(5m) and
// CardsVar(1); OnUpgrade raises the damage by 3 and leaves the draw at 1.
public class FlashOfSteelTests
{
    [Fact]
    public void DealsFiveAndDrawsOne()
    {
        var fight = Fight
            .Hand(Card(CL.FlashOfSteel))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(35, fight.Enemy0.Hp);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDealsEight()
    {
        var fight = Fight
            .Hand(Card(CL.FlashOfSteel, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradeDoesNotDrawAnExtraCard()
    {
        var fight = Fight
            .Hand(Card(CL.FlashOfSteel, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }
}
