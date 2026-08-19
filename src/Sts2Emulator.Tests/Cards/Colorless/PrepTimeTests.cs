using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/PrepTime.cs applies PowerVar<PrepTimePower>(4m) — Vigor at the start of each turn; OnUpgrade raises it by 2.
public class PrepTimeTests
{
    [Fact]
    public void AppliesFour()
    {
        var fight = Fight.Hand(Card(CL.PrepTime)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.PrepTimePower));
    }

    [Fact]
    public void UpgradedAppliesSix()
    {
        var fight = Fight.Hand(Card(CL.PrepTime, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.PrepTimePower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.PrepTime)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
