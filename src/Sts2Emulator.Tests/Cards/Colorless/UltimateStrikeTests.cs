using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/UltimateStrike.cs: DamageVar(14m); OnUpgrade raises it by 6. Pure damage, so it runs on the generic path.
public class UltimateStrikeTests
{
    [Fact]
    public void Deals14()
    {
        var fight = Fight.Hand(Card(CL.UltimateStrike)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(46, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDeals20()
    {
        var fight = Fight.Hand(Card(CL.UltimateStrike, upgraded: true)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsRaisedByStrength()
    {
        var fight = Fight
            .Hand(Card(CL.UltimateStrike))
            .Energy(2)
            .PlayerBuff(BuffId.Strength, 3)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(43, fight.Enemy0.Hp);
    }
}
