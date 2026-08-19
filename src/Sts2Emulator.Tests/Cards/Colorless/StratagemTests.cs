using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Stratagem.cs applies StratagemPower(1) — pull a card from the draw pile after a shuffle; OnUpgrade only makes it cheaper.
public class StratagemTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(CL.Stratagem)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.StratagemPower));
    }

    [Fact]
    public void UpgradedCostsZeroRatherThanApplyingMore()
    {
        var fight = Fight.Hand(Card(CL.Stratagem, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.StratagemPower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.Stratagem)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
