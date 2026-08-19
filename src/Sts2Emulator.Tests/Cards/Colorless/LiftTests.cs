using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardMultiplayerConstraint.MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/Lift.cs gives BlockVar(11m) to the targeted ally;
// OnUpgrade raises it by 5. Singleplayer's only ally is the player, and the card has no
// case of its own — the block comes off the generic damage-and-block path.
public class LiftTests
{
    [Fact]
    public void GivesElevenBlock()
    {
        var fight = Fight.Hand(Card(CL.Lift)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(11, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGivesSixteen()
    {
        var fight = Fight.Hand(Card(CL.Lift, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.State.PlayerBlock);
    }

    [Fact]
    public void IsRaisedByDexterity()
    {
        var fight = Fight
            .Hand(Card(CL.Lift))
            .Energy(1)
            .PlayerBuff(BuffId.Dexterity, 2)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(13, fight.State.PlayerBlock);
    }
}
