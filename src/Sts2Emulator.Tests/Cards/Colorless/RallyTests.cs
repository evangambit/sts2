using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill, MultiplayerOnly, TargetType.AllAllies.
// MegaCrit.Sts2.Core.Models.Cards/Rally.cs gives BlockVar(12m) to every living ally;
// OnUpgrade raises it by 5. Singleplayer's team is one player, and the card has no case
// of its own — the block comes off the generic damage-and-block path.
public class RallyTests
{
    [Fact]
    public void GivesTwelveBlock()
    {
        var fight = Fight.Hand(Card(CL.Rally)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(12, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGivesSeventeen()
    {
        var fight = Fight.Hand(Card(CL.Rally, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(17, fight.State.PlayerBlock);
    }
}
