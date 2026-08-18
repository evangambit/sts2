using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Dash.cs: BlockVar(10m) gained BEFORE
// DamageVar(10m) is dealt; OnUpgrade raises both by 3.
public class DashTests
{
    [Fact]
    public void GainsTenBlockAndDealsTen()
    {
        var fight = Fight.Hand(Card(SI.Dash)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(30, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedGainsAndDealsThirteen()
    {
        var fight = Fight.Hand(Card(SI.Dash, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(13, fight.State.PlayerBlock);
        Assert.Equal(27, fight.Enemy0.Hp);
    }

    [Fact]
    public void BlockLandsBeforeTheHitSoJuggernautFiresFirst()
    {
        var fight = Fight
            .Hand(Card(SI.Dash))
            .Energy(2)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .Enemy(hp: 40);

        // 6 from Juggernaut's block trigger plus Dash's own 10.
        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
    }
}
