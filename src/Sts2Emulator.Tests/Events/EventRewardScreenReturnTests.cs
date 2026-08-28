using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// An event that hands out rewards gets its result page back once the screen is answered.
/// </summary>
/// <remarks>
/// Every such event has the same two lines: <c>await RewardsCmd.OfferCustom(...)</c> and
/// then <c>SetEventFinished(...)</c>. Neow already had this return and events did not, so
/// the run lost one Proceed every time — and the SKIP path walked past the return
/// entirely, which is invisible for Neow (whose rewards are WithSkippingDisallowed) and
/// wrong for every event, since Whispering Hollow offers two potions and a run with a full
/// belt declines the second far more often than it takes it. `RRRR6WR3C4` is the capture:
/// it claims one potion, proceeds, and is back on the event page.
/// </remarks>
public class EventRewardScreenReturnTests
{
    private static RunEngine AtWhisperingHollowGoldOption()
    {
        var engine = new RunEngine();
        engine.Reset("RRRR6WR3C4");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventWhisperingHollow;
        engine.State.Gold = 500;
        engine.Step(0, -1, out _, out _, out _);
        return engine;
    }

    [Fact]
    public void TheOptionOpensARewardScreen()
    {
        var engine = AtWhisperingHollowGoldOption();

        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
    }

    /// <summary>
    /// Declining the screen still leaves it the way answering it does.
    /// </summary>
    [Fact]
    public void SkippingTheScreenReturnsToTheEventRatherThanTheMap()
    {
        var engine = AtWhisperingHollowGoldOption();

        engine.Step(RunConstants.RewardSkipAction, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(RunConstants.EventResultPending, engine.State.EventId);
    }

    /// <summary>
    /// And the event page stays up for exactly one more Proceed, then the map.
    /// </summary>
    [Fact]
    public void TheEventPageTakesOneMoreProceed()
    {
        var engine = AtWhisperingHollowGoldOption();
        engine.Step(RunConstants.RewardSkipAction, -1, out _, out _, out _);

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    /// <summary>
    /// A screen nobody is waiting on still goes to the map, which is the case this
    /// return had to not break.
    /// </summary>
    [Fact]
    public void ACombatRewardScreenStillAdvancesToTheMap()
    {
        var engine = new RunEngine();
        engine.Reset("RRRR6WR3C4");
        Assert.False(engine.State.EventAwaitingProceed);
    }
}
