using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/ThrummingHatchet.cs: DamageVar(11m); OnUpgrade raises it by 3. Pure damage, so it runs on the generic damage-and-block path with no case of its own.
public class ThrummingHatchetTests
{
    [Fact]
    public void Deals11()
    {
        var fight = Fight.Hand(Card(CL.ThrummingHatchet)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(49, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDeals14()
    {
        var fight = Fight.Hand(Card(CL.ThrummingHatchet, upgraded: true)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(46, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsRaisedByStrength()
    {
        var fight = Fight
            .Hand(Card(CL.ThrummingHatchet))
            .Energy(2)
            .PlayerBuff(BuffId.Strength, 3)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(46, fight.Enemy0.Hp);
    }
}
