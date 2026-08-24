using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What beating a boss pays, which nothing could check until a run finally won an act.
/// </summary>
/// <remarks>
/// <c>RewardsSet</c>'s Boss case is gold, potion and card — and, unlike the Elite case
/// directly above it, NO relic. Both defects here sat behind a boss fight no capture had
/// ever survived; a buffed run beat the Kin Priest and found them in consecutive steps.
/// </remarks>
public class BossRewardTests
{
    private static RunEngine At(int nodeType)
    {
        var engine = new RunEngine();
        engine.Reset("3PFLW9XC5D");
        engine.State.CurrentNodeType = nodeType;
        return engine;
    }

    /// <summary>
    /// Min and Max are both 100, and A8's Poverty multiplier takes a quarter off before
    /// the roll — the same 0.75 already baked into the monster range (10-20 becomes 7-15)
    /// and the elite one (35-45 becomes 26-33). A live capture is paid exactly 75.
    /// </summary>
    [Fact]
    public void ABossPaysSeventyFiveGoldNotAHundred()
    {
        var engine = At(RunConstants.NodeBoss);

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Equal(75, engine.State.RewardGold);
    }

    /// <summary>
    /// The roll still happens — it is <c>NextInt(75, 76)</c>, not a constant — so the
    /// stream stays aligned with the game's.
    /// </summary>
    [Fact]
    public void TheBossGoldStillSpendsItsDraw()
    {
        var engine = At(RunConstants.NodeBoss);
        int before = engine.State.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.True(engine.State.PlayerRng.Rewards.CallCount > before);
    }

    /// <summary>
    /// Only an ELITE hands over a relic. Giving one for a boss too meant beating act 1
    /// handed the run a Whetstone it never earned — and Whetstone's pickup upgraded two
    /// attacks in the deck, so the error spread past the relic list immediately.
    /// </summary>
    [Fact]
    public void ABossGivesNoRelicWhereAnEliteDoes()
    {
        var boss = At(RunConstants.NodeBoss);
        var elite = At(RunConstants.NodeElite);

        RunRewardGenerator.GenerateCombatRewards(boss.State);
        RunRewardGenerator.GenerateCombatRewards(elite.State);

        Assert.False(boss.State.PendingRelicReward);
        Assert.Equal(0, boss.State.RelicReward);
        Assert.True(elite.State.PendingRelicReward);
        Assert.NotEqual(0, elite.State.RelicReward);
    }
}
