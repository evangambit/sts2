using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Pinpoint.cs: DamageVar(15m);
// OnUpgrade raises it by 4.
public class PinpointTests
{
    [Fact]
    public void DealsFifteen()
    {
        var fight = Fight.Hand(Card(SI.Pinpoint)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(25, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsNineteen()
    {
        var fight = Fight.Hand(Card(SI.Pinpoint, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(21, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsRaisedByTheTargetsVulnerable()
    {
        var fight = Fight
            .Hand(Card(SI.Pinpoint))
            .Energy(3)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)]);

        // 15 x 1.5, truncated.
        fight.Play();

        Assert.Equal(18, fight.Enemy0.Hp);
    }
}
