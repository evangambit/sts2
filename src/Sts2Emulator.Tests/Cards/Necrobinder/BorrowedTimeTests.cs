using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/BorrowedTime.cs: EnergyVar 4 upgrading by 2 now, paid
// for with an "ExtraCost" of 1. `BorrowedTimePower.TryModifyEnergyCostInCombat` adds its
// amount to every card its owner plays, and `AfterSideTurnEnd` removes it — so the tax
// lands on the turn it was taken out and no later.
//
// The emulator granted NoBlock, which is not what the card borrows against.
public class BorrowedTimeTests
{
    private const int BorrowedTime = 56;
    private const int Strike = 473; // costs 1
    private const int Defend = 132;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(0).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(BorrowedTime, upgraded));
        fight.State.Energy = 1;
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesFourEnergyAndSixUpgraded()
    {
        Assert.Equal(4, Played().State.Energy);
        Assert.Equal(6, Played(upgraded: true).State.Energy);
    }

    [Fact]
    public void EveryLaterCardCostsOneMore()
    {
        var fight = Played();
        var strike = new CardInstance(Strike, false);

        Assert.Equal(2, CombatEngine.EffectiveCost(strike, fight.State));
    }

    [Fact]
    public void WithoutItACardCostsItsPrintedCost()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);

        Assert.Equal(1, CombatEngine.EffectiveCost(new CardInstance(Strike, false), fight.State));
    }

    /// <summary>Not a Late hook: a card that has been made free stays free.</summary>
    [Fact]
    public void AFreeCardIsStillFree()
    {
        var fight = Played();
        var free = new CardInstance(Defend, false) { FreeThisTurn = true };

        Assert.Equal(0, CombatEngine.EffectiveCost(free, fight.State));
    }

    /// <summary>`AfterSideTurnEnd` removes it, so next turn's cards are untaxed.</summary>
    [Fact]
    public void TheTaxIsGoneNextTurn()
    {
        var fight = Played();
        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.BorrowedTime));
        Assert.Equal(1, CombatEngine.EffectiveCost(new CardInstance(Strike, false), fight.State));
    }

    [Fact]
    public void ItGrantsNoNoBlock()
    {
        Assert.Equal(0, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.NoBlock));
    }
}
