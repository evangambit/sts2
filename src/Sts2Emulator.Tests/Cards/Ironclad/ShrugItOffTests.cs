using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/ShrugItOff.cs: BlockVar(8m) and
// CardsVar(1); OnUpgrade raises block by 3 and leaves the draw at 1.
public class ShrugItOffTests
{
    [Fact]
    public void GainsEightBlockAndDrawsOne()
    {
        var fight = Fight
            .Hand(Card(IC.ShrugItOff))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedGainsElevenBlock()
    {
        var fight = Fight
            .Hand(Card(IC.ShrugItOff, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(11, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradeDoesNotDrawAnExtraCard()
    {
        var fight = Fight
            .Hand(Card(IC.ShrugItOff, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }
}
