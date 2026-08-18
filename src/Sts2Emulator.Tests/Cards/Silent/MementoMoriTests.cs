using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/MementoMori.cs: CalculationBaseVar(9m)
// plus ExtraDamageVar(4m) for every card discarded this turn; OnUpgrade raises the
// per-discard damage by 1 and leaves the base at 9.
//
// The emulator does not count cards discarded this turn, so it models the zero-discard
// case. These tests pin that, not the missing scaling.
public class MementoMoriTests
{
    [Fact]
    public void DealsNineWithNothingDiscardedThisTurn()
    {
        var fight = Fight.Hand(Card(SI.MementoMori)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradeRaisesThePerDiscardDamageNotTheBase()
    {
        var fight = Fight.Hand(Card(SI.MementoMori, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
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
