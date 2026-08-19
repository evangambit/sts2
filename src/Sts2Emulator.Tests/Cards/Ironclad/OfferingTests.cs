using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Offering.cs: HpLossVar(6m) as
// Unblockable | Unpowered | Move, then EnergyVar(2), then CardsVar(3) drawn; OnUpgrade
// raises the draw by 2 and leaves the HP and energy alone.
public class OfferingTests
{
    [Fact]
    public void LosesSixHpForTwoEnergyAndThreeCards()
    {
        var fight = Fight
            .Hand(Card(IC.Offering))
            .Energy(0)
            .PlayerHp(64)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(58, fight.State.PlayerHp);
        Assert.Equal(2, fight.State.Energy);
        Assert.Equal(3, fight.State.Hand.Count);
    }

    [Fact]
    public void UpgradedDrawsFive()
    {
        var fight = Fight
            .Hand(Card(IC.Offering, upgraded: true))
            .Energy(0)
            .PlayerHp(64)
            .Draw(
                Card(IC.Bash),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Anger),
                Card(IC.Bash),
                Card(IC.Bash)
            )
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.State.Hand.Count);
        Assert.Equal(2, fight.State.Energy);
    }

    [Fact]
    public void TheHpLossIsUnblockable()
    {
        var fight = Fight.Hand(Card(IC.Offering)).Energy(0).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 10;

        fight.Play();

        Assert.Equal(58, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
    }
}
