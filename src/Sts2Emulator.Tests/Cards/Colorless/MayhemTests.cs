using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Power. MegaCrit.Sts2.Core.Models.Cards/Mayhem.cs applies MayhemPower(1) — auto-play the top card of the draw pile each turn; OnUpgrade only makes it cheaper.
public class MayhemTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(CL.Mayhem)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.MayhemPower));
    }

    [Fact]
    public void UpgradedCostsOneRatherThanApplyingMore()
    {
        var fight = Fight.Hand(Card(CL.Mayhem, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.MayhemPower));
    }

    [Fact]
    public void LeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(CL.Mayhem)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
