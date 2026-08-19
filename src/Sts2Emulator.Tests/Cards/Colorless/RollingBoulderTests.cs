using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Power. MegaCrit.Sts2.Core.Models.Cards/RollingBoulder.cs applies PowerVar<RollingBoulderPower>(5m), damage to all enemies at turn start that grows by DynamicVar("IncrementAmount", 5m); OnUpgrade raises the starting damage by 5.
public class RollingBoulderTests
{
    [Fact]
    public void AppliesFive()
    {
        var fight = Fight.Hand(Card(CL.RollingBoulder)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.RollingBoulderPower));
    }

    [Fact]
    public void UpgradedAppliesTen()
    {
        var fight = Fight.Hand(Card(CL.RollingBoulder, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(10, fight.PlayerBuffAmount(BuffId.RollingBoulderPower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.RollingBoulder)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
