using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What a card reward can offer, and how likely each rarity is. Both were wrong in ways
/// the run still looked plausible under: a pool with the wrong members hands back the
/// neighbouring card, and an offset that starts in the wrong place turns Commons into
/// Uncommons for the first several rewards of a run.
/// </summary>
public class RunRewardPoolTests
{
    [Fact]
    public void IroncladRewardPool_IsTheGamesOwnPool()
    {
        Assert.Equal(
            GeneratedData.CardPools.Ironclad.ToArray(),
            RunRewardGenerator.IroncladRewardPool.ToArray()
        );
    }

    [Fact]
    public void IroncladRewardPool_HoldsNoColorlessCards()
    {
        // Restlessness, Splash and Ultimate Defend are Colorless; a hand-written copy of
        // the pool carried all three, which shifted every uncommon pick past them.
        foreach (int colorless in GeneratedData.CardPools.Colorless.ToArray())
        {
            Assert.DoesNotContain(colorless, RunRewardGenerator.IroncladRewardPool.ToArray());
        }
    }

    [Fact]
    public void CardRarityOffset_StartsWhereTheGameStartsIt()
    {
        // CardRarityOdds(Rng) : base(-0.05f, rng) -- the same value a Rare roll resets to.
        var engine = new RunEngine();
        engine.Reset("QS2GYXRKWN");

        Assert.Equal(RunRewardGenerator.CardRarityBaseOffset, engine.State.CardRarityOffset);
        Assert.Equal(-0.05, RunRewardGenerator.CardRarityBaseOffset);
    }
}
