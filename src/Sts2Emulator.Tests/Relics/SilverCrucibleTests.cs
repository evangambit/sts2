using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Silver Crucible: the first three card rewards of the run come upgraded.
/// </summary>
/// <remarks>
/// It is <c>TryModifyCardRewardOptionsLate</c> — the relic edits the three cards AFTER
/// <c>CardFactory.CreateForReward</c> has finished rolling them, so it changes what the
/// screen shows and nothing about what was rolled. The emulator had the upgrade written as
/// the first term of a <c>||</c> chain whose second term was the upgrade ROLL, and C#
/// short-circuits: a run holding the Crucible spent two rewards-stream values per card
/// where the game spends three. The first card survived that, and every card after it in
/// the run was somebody else's.
/// </remarks>
public class SilverCrucibleTests
{
    private static RunEngine AtFirstCardReward(string seed, bool withCrucible)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        if (withCrucible)
        {
            engine.State.Relics.Add(new RelicInstance(RunConstants.RelicSilverCrucible, 3));
        }

        RunRewardGenerator.PopulateCardReward(engine.State);
        return engine;
    }

    /// <summary>
    /// The whole point: holding the relic must not move the stream, so the three cards
    /// offered are the same three either way — only their upgrade flags differ.
    /// </summary>
    [Theory]
    [InlineData("RRRR6WR3C4")]
    [InlineData("J09SPL8Y3V")]
    [InlineData("NXV45HW43K")]
    public void ItChangesTheUpgradesWithoutChangingTheCards(string seed)
    {
        var without = AtFirstCardReward(seed, withCrucible: false).State;
        var with = AtFirstCardReward(seed, withCrucible: true).State;

        Assert.Equal(without.RewardCards, with.RewardCards);
        Assert.All(with.RewardUpgraded, upgraded => Assert.True(upgraded));
    }

    /// <summary>
    /// Three rewards and then it is spent, so the fourth screen rolls its own upgrades.
    /// </summary>
    [Fact]
    public void ItUpgradesOnlyThreeRewards()
    {
        var engine = new RunEngine();
        engine.Reset("RRRR6WR3C4");
        engine.State.Relics.Add(new RelicInstance(RunConstants.RelicSilverCrucible, 3));

        for (int screen = 0; screen < 3; screen++)
        {
            RunRewardGenerator.PopulateCardReward(engine.State);
            Assert.All(engine.State.RewardUpgraded, upgraded => Assert.True(upgraded));
        }

        RunRewardGenerator.PopulateCardReward(engine.State);
        Assert.All(engine.State.RewardUpgraded, upgraded => Assert.False(upgraded));
    }
}
