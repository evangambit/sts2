using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust, MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/Mimic.cs: CalculatedBlockVar whose multiplier is the
// targeted ally's Block — you copy their guard. OnUpgrade removes the Exhaust.
//
// Singleplayer's only ally is the player, so it doubles your own block.
public class MimicTests
{
    [Fact]
    public void DoublesYourOwnBlock()
    {
        var fight = Fight.Hand(Card(CL.Mimic)).Energy(1).Enemy(hp: 40);
        fight.State.PlayerBlock = 9;

        fight.Play();

        Assert.Equal(18, fight.State.PlayerBlock);
    }

    [Fact]
    public void GainsNothingWithNoBlockToCopy()
    {
        var fight = Fight.Hand(Card(CL.Mimic)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.Mimic)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.Mimic], Fight.Ids(fight.State.ExhaustPile));
    }
}
