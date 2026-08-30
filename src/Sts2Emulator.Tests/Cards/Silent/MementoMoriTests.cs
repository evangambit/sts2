using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/MementoMori.cs: CalculationBaseVar(9m)
// plus ExtraDamageVar(4m) for every card discarded this turn. OnUpgrade raises BOTH --
// `CalculationBase.UpgradeValueBy(2m)` and `ExtraDamage.UpgradeValueBy(1m)` -- so the
// base is 11 upgraded and the per-discard is 5.
//
// The header here used to say the upgrade left the base at 9, and the test below was
// named for that claim and asserted it. It came from a reading, and the reading was
// wrong; the source says otherwise in the line right under the one that was read.
//
// The per-discard SCALING is still unmodelled: it counts CardDiscardedEntry rows for the
// turn and the emulator has no discard counter. These tests pin the zero-discard case.
public class MementoMoriTests
{
    [Fact]
    public void DealsNineWithNothingDiscardedThisTurn()
    {
        var fight = Fight.Hand(Card(SI.MementoMori)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    /// <summary>The upgrade raises the base from 9 to 11, on top of the per-discard 4 to 5.</summary>
    [Fact]
    public void UpgradeRaisesTheBaseAsWellAsThePerDiscardDamage()
    {
        var fight = Fight.Hand(Card(SI.MementoMori, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(29, fight.Enemy0.Hp);
    }

    [Fact]
    public void HitsOnlyTheTargetedEnemy()
    {
        var fight = Fight.Hand(Card(SI.MementoMori)).Energy(1).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal(31, fight.Enemy1.Hp);
    }
}
