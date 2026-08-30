using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Demesne.cs: a 3-cost Ethereal Power applying
// DemesnePower at CardsVar 1. `OnUpgrade` is `EnergyCost.UpgradeBy(-1)` — a discount, not
// a bigger stack.
//
// DemesnePower is BOTH `ModifyHandDraw` and `ModifyMaxEnergy`, by its amount, every turn
// for the rest of the combat. The emulator granted a one-shot NextTurnEnergy and
// NextTurnDraw: a single turn of a permanent effect.
public class DemesneTests
{
    private const int Demesne = 140;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Demesne, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAppliesOneStackWhetherUpgradedOrNot()
    {
        Assert.Equal(1, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.Demesne));
        Assert.Equal(1, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.Demesne));
    }

    [Fact]
    public void ItRaisesMaxEnergyEveryTurn()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Energy;

        var fight = Played();
        fight.EndTurn();

        Assert.Equal(plain + 1, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(plain + 1, fight.State.Energy);
    }

    [Fact]
    public void ItRaisesTheHandDrawEveryTurn()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Played();
        fight.EndTurn();

        Assert.Equal(plain + 1, fight.State.Hand.Count);
    }

    /// <summary>Not the one-shot NextTurn pair it used to grant.</summary>
    [Fact]
    public void ItGrantsNoOneShotNextTurnBonuses()
    {
        var fight = Played();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnEnergy));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NextTurnDraw));
    }
}
