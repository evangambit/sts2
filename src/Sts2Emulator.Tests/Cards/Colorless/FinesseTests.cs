using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Finesse.cs: BlockVar(4m) and CardsVar(1);
// OnUpgrade raises the block by 3 and leaves the draw at 1.
public class FinesseTests
{
    [Fact]
    public void GainsFourBlockAndDrawsOne()
    {
        var fight = Fight
            .Hand(Card(CL.Finesse))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.State.PlayerBlock);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedGainsSevenBlock()
    {
        var fight = Fight
            .Hand(Card(CL.Finesse, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(7, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradeDoesNotDrawMore()
    {
        var fight = Fight
            .Hand(Card(CL.Finesse, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }
}
