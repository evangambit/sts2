using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Strangle.cs: DamageVar(8m) and
// PowerVar<StranglePower>(2m); OnUpgrade raises the damage by 2 and leaves the power at 2.
//
// StranglePower itself is not modelled — the emulator stands Vulnerable 2 in for it — so
// the debuff assertions pin the stand-in, not the real power.
public class StrangleTests
{
    [Fact]
    public void DealsEightAndAppliesTheDebuff()
    {
        var fight = Fight.Hand(Card(SI.Strangle)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradedDealsTen()
    {
        var fight = Fight.Hand(Card(SI.Strangle, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(30, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradeDoesNotRaiseTheDebuff()
    {
        var fight = Fight.Hand(Card(SI.Strangle, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }
}
