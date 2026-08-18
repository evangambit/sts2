using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardMultiplayerConstraint.MultiplayerOnly.
// MegaCrit.Sts2.Core.Models.Cards/GangUp.cs: CalculationBaseVar(5m) plus
// ExtraDamageVar(5m) for every powered attack an ALLY landed on the target this turn;
// OnUpgrade raises that per-hit damage by 2, not the base.
//
// Singleplayer has no allies, so the multiplier is always zero here.
public class GangUpTests
{
    [Fact]
    public void DealsFiveWithNoAlliesToGangUpWith()
    {
        var fight = Fight.Hand(Card(CL.GangUp)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(35, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedStillDealsFiveWithoutAllies()
    {
        var fight = Fight.Hand(Card(CL.GangUp, upgraded: true)).Energy(1).Enemy(hp: 40);

        // The upgrade raises the per-ally-hit damage; with no ally hits it changes nothing.
        fight.Play();

        Assert.Equal(35, fight.Enemy0.Hp);
    }

    [Fact]
    public void HitsOnlyTheTargetedEnemy()
    {
        var fight = Fight.Hand(Card(CL.GangUp)).Energy(1).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal(35, fight.Enemy1.Hp);
    }
}
