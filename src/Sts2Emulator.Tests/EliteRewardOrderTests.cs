using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// An elite's three cards are rolled BEFORE its relic.
/// </summary>
/// <remarks>
/// <c>RewardsSet</c> builds gold, potion, card, relic and then populates them in that
/// order; it only sorts by <c>RewardsSetIndex</c> afterwards, so the order the screen
/// SHOWS them in — relic above the card — is not the order they were rolled in. The
/// relic's rarity comes off <c>PlayerRng.Rewards</c> like everything else, so rolling it
/// early handed the card offer the relic's value and shifted all three cards by a draw.
///
/// <para>
/// Thirty committed traces missed this because not one of them ever claimed a combat
/// relic reward — the three relic claims in the whole set are Neow screens on step 2. It
/// took a buffed run surviving to a floor-14 elite to reach the screen at all.
/// </para>
/// </remarks>
public class EliteRewardOrderTests
{
    /// <summary>
    /// With the potion roll forced to fail on both, an elite and a normal fight spend the
    /// same draws before their cards — one for the potion, one for the gold — so they
    /// must offer the SAME three cards. A relic rolled in front of the cards is exactly
    /// what breaks that, and it broke it on all three seeds.
    /// </summary>
    [Theory]
    [InlineData("8QKMNR4T2W")]
    [InlineData("J09SPL8Y3V")]
    [InlineData("RRRR6WR3C4")]
    public void AnEliteOffersTheSameCardsAsANormalFightFromTheSamePosition(string seed)
    {
        var elite = At(seed, RunConstants.NodeElite);
        var normal = At(seed, RunConstants.NodeNormal);

        RunRewardGenerator.GenerateCombatRewards(elite.State);
        RunRewardGenerator.GenerateCombatRewards(normal.State);

        Assert.Equal(normal.State.RewardCards, elite.State.RewardCards);
        // And the elite really did take a relic, so this is not passing by doing nothing.
        Assert.NotEqual(0, elite.State.RelicReward);
        Assert.Equal(0, normal.State.RelicReward);
    }

    private static RunEngine At(string seed, int nodeType)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.CurrentNodeType = nodeType;
        // Far enough below zero that neither the elite's bonus nor the normal odds can
        // land a potion, so the two sides spend the same number of draws getting there.
        engine.State.PotionRewardOdds = -10;
        return engine;
    }
}
