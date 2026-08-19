using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Fasten.cs applies FastenPower at DynamicVar("ExtraBlock", 4m) — Defend cards give that much more block; OnUpgrade raises it by 2.
public class FastenTests
{
    [Fact]
    public void AppliesFour()
    {
        var fight = Fight.Hand(Card(CL.Fasten)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FastenPower));
    }

    [Fact]
    public void UpgradedAppliesSix()
    {
        var fight = Fight.Hand(Card(CL.Fasten, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.FastenPower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.Fasten)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
