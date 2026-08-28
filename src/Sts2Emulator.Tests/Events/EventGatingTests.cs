using System.Reflection;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Every event that refuses to appear under some condition must say so here.
///
/// <para>
/// <c>IsEventAllowed</c> ends in <c>_ =&gt; true</c>. An event whose rule nobody
/// transcribed is therefore silently ALLOWED: it does not fail, it turns up where the
/// game would never offer it, and because the sequence is consumed in order every later
/// event shifts with it. A live Underdocks run drew Sunken Treasury where the emulator
/// drew War Historian Repy -- whose rule is <c>return false</c>, so the game never offers
/// it from the sequence at all.
/// </para>
/// </summary>
public class EventGatingTests
{
    /// <summary>Map an event's decompiled class name to its RunConstants id.</summary>
    private static int? IdOf(string eventName)
    {
        FieldInfo? field = typeof(RunConstants).GetField(
            $"Event{eventName}",
            BindingFlags.Public | BindingFlags.Static
        );
        return field?.IsLiteral == true ? (int)field.GetRawConstantValue()! : null;
    }

    /// <summary>
    /// The generated list is the set of events the GAME gates. Every one of them has to
    /// be reachable as a constant, or the gate cannot be written for it.
    /// </summary>
    [Fact]
    public void EveryGatedEventHasAConstant()
    {
        var unknown = EventGatingCoverage
            .EventsWithARule.Where(name => IdOf(name) is null)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>
    /// War Historian Repy's rule is <c>return false</c>: it is never drawn from the
    /// sequence. This is the case the whole guard exists for.
    /// </summary>
    [Fact]
    public void WarHistorianRepyIsNeverOfferedFromTheSequence()
    {
        var engine = new RunEngine();
        engine.Reset("4KJ7X2MQND");

        Assert.False(
            RunNonCombatEffects.IsEventAllowedForTests(
                engine.State,
                RunConstants.EventWarHistorianRepy
            )
        );
    }

    /// <summary>
    /// The rules that a live run showed were wrong, each read off the decompiled
    /// <c>IsAllowed</c>. The thresholds are the point: the belt wanted 40 gold where the
    /// game wants 120, and the scriptorium's <c>|| Deck.Count &gt; 0</c> made a 55-gold
    /// test that a run always passed.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.EventEndlessConveyor, 119, false)]
    [InlineData(RunConstants.EventEndlessConveyor, 120, true)]
    [InlineData(RunConstants.EventWaterloggedScriptorium, 54, false)]
    [InlineData(RunConstants.EventWaterloggedScriptorium, 55, true)]
    [InlineData(RunConstants.EventWhisperingHollow, 43, false)]
    [InlineData(RunConstants.EventWhisperingHollow, 44, true)]
    public void GoldGatedEventsUseTheGamesThreshold(int eventId, int gold, bool allowed)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Gold = gold;

        Assert.Equal(allowed, RunNonCombatEffects.IsEventAllowedForTests(engine.State, eventId));
    }

    /// <summary>Trash Heap wants a run with more than 5 hp to spend.</summary>
    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void TrashHeapWantsHpToSpend(int hp, bool allowed)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.PlayerHp = hp;

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForTests(engine.State, RunConstants.EventTrashHeap)
        );
    }

    /// <summary>
    /// The Unrest Site only appears to a run that is actually hurt -- at or below 70% of
    /// max. It was grouped with the events that are always allowed.
    /// </summary>
    [Theory]
    [InlineData(80, false)]
    [InlineData(57, false)]
    [InlineData(56, true)]
    public void TheUnrestSiteOnlyMeetsAHurtRun(int hp, bool allowed)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.PlayerHp = hp; // max is 80, so the cut is 56

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForTests(engine.State, RunConstants.EventUnrestSite)
        );
    }

    /// <summary>Both floor-gated events read the run's floor, not its act.</summary>
    [Theory]
    [InlineData(RunConstants.EventPunchOff, 5, false)]
    [InlineData(RunConstants.EventPunchOff, 6, true)]
    [InlineData(RunConstants.EventSlipperyBridge, 6, false)]
    [InlineData(RunConstants.EventSlipperyBridge, 7, true)]
    public void FloorGatedEventsWaitForTheirFloor(int eventId, int floor, bool allowed)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.Floor = floor;

        Assert.Equal(allowed, RunNonCombatEffects.IsEventAllowedForTests(engine.State, eventId));
    }
}
