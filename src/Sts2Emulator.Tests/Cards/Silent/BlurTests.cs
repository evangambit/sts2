using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// `BlockVar(5m)` and `DynamicVar("Blur", 1m)`; OnUpgrade raises the BLOCK by 3 and leaves
/// the Blur at 1.
/// </summary>
/// <remarks>
/// `BlurPower.ShouldClearBlock` returns false for its owner, so the block survives the
/// turn start — and `AfterSideTurnStart` DECREMENTS the power, so a counter of 1 saves it
/// once and is then gone.
///
/// The emulator stood `Barricade` in for it, which is the Ironclad rare whose block never
/// expires at all. A 1-cost common was playing as a 3-cost rare, permanently, for the rest
/// of the combat — and every Defend after it compounded, since nothing ever cleared.
/// </remarks>
public class BlurTests
{
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void TheBlockSurvivesExactlyOneTurnStart(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.Blur, upgraded)).Energy(1);
        fight.State.PlayerHp = 9999;

        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Blur));

        // Set high enough that the enemy attack cannot eat all of it: otherwise "no block
        // left" cannot be told apart from "the block was cleared", which is the whole
        // question. The first draft of this test could not, and passed at 8 and failed at
        // 5 for that reason alone.
        fight.State.PlayerBlock = 500;
        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Blur));
        Assert.True(fight.State.PlayerBlock > 0, "the block should have survived one turn");
    }

    /// <summary>And the turn after that, block clears as usual.</summary>
    [Fact]
    public void TheTurnAfterThatTheBlockClears()
    {
        var fight = Fight.Hand(Card(SI.Blur)).Energy(1);
        fight.State.PlayerHp = 9999;
        fight.Play();

        fight.State.PlayerBlock = 99;
        fight.EndTurn(); // saved by Blur, which is spent doing it
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Blur));

        fight.State.PlayerBlock = 99;
        fight.State.PlayerHp = 9999;
        fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>
    /// It is not Barricade: playing Blur must not leave the player with a power that never
    /// expires, which is what the stand-in did.
    /// </summary>
    [Fact]
    public void ItIsNotBarricade()
    {
        var fight = Fight.Hand(Card(SI.Blur)).Energy(1);

        fight.Play();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Barricade));
    }

    /// <summary>
    /// Two Blurs stack the counter, so the block rides two turn starts rather than one.
    /// `PowerStackType.Counter` is what makes that true.
    /// </summary>
    [Fact]
    public void TwoBlursSaveTheBlockTwice()
    {
        var fight = Fight.Hand(Card(SI.Blur), Card(SI.Blur)).Energy(3);
        fight.State.PlayerHp = 9999;
        fight.Play();
        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Blur));

        fight.State.PlayerBlock = 99;
        fight.EndTurn();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Blur));

        fight.State.PlayerBlock = 99;
        fight.State.PlayerHp = 9999;
        fight.EndTurn();
        Assert.True(fight.State.PlayerBlock > 0);

        fight.State.PlayerBlock = 99;
        fight.State.PlayerHp = 9999;
        fight.EndTurn();
        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
