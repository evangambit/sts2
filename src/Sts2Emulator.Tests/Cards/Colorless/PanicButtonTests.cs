using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/PanicButton.cs:
// BlockVar(30m), then NoBlockPower for DynamicVar("Turns", 2m) turns; OnUpgrade raises
// the block by 10 and leaves the turns at 2.
public class PanicButtonTests
{
    [Fact]
    public void GainsThirtyBlockAndBlocksBlockForTwoTurns()
    {
        var fight = Fight.Hand(Card(CL.PanicButton)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(30, fight.State.PlayerBlock);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NoBlock));
    }

    [Fact]
    public void UpgradedGainsForty()
    {
        var fight = Fight.Hand(Card(CL.PanicButton, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.State.PlayerBlock);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NoBlock));
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.PanicButton)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.PanicButton], Fight.Ids(fight.State.ExhaustPile));
    }
}
