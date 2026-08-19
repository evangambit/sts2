using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Power. MegaCrit.Sts2.Core.Models.Cards/Panache.cs applies PanachePower at DynamicVar("PanacheDamage", 10m) — damage to all enemies after every fifth card played; OnUpgrade raises it by 4.
public class PanacheTests
{
    [Fact]
    public void AppliesTen()
    {
        var fight = Fight.Hand(Card(CL.Panache)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(10, fight.PlayerBuffAmount(BuffId.PanachePower));
    }

    [Fact]
    public void UpgradedAppliesFourteen()
    {
        var fight = Fight.Hand(Card(CL.Panache, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(14, fight.PlayerBuffAmount(BuffId.PanachePower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.Panache)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
