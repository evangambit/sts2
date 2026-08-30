using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Genesis.cs: a "StarsPerTurn" var of 2, upgrading by 1.
// `GenesisPower.AfterEnergyReset` gives that many stars at the start of every turn, and
// unlike StarNextTurnPower it does NOT remove itself.
//
// It had been one more label on the flat Strength body this pool was built with.
public class GenesisTests
{
    private const int Genesis = 215;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Genesis, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesNoStarsOnTheTurnItIsPlayed()
    {
        var fight = Played();

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Genesis));
        Assert.Equal(0, fight.State.Stars);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void ItGivesTwoStarsEveryTurn()
    {
        var fight = Played();

        fight.EndTurn();
        Assert.Equal(2, fight.State.Stars);

        fight.EndTurn();
        Assert.Equal(4, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeGivesThree()
    {
        var fight = Played(upgraded: true);

        fight.EndTurn();

        Assert.Equal(3, fight.State.Stars);
    }
}
