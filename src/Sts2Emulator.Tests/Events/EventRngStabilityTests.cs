using System.Reflection;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Looking at an event must not change it.
///
/// <para>
/// Several event values were exposed as accessors that re-rolled on every call --
/// the tribute the Luminous Choir asks, the card the Slippery Bridge names, the
/// relic Ranwid wants. That was invisible while every draw came off a freshly
/// built stream, because a fresh stream always returns the same first value. Once
/// the stream was made persistent (which is what the game has) the second read
/// returned something else, and the mask and the step disagreed about what the
/// event was even offering.
/// </para>
/// <para>
/// The invariant is the fix: writing the action mask is an observation, and an
/// observation may not advance the run's randomness. An agent that reads the mask
/// before deciding would otherwise perturb the very outcome it is deciding about.
/// </para>
/// </summary>
public class EventRngStabilityTests
{
    /// <summary>Every <c>EventX</c> id the engine knows, read off RunConstants.</summary>
    public static TheoryData<string, int> Events()
    {
        var data = new TheoryData<string, int>();
        foreach (
            FieldInfo field in typeof(RunConstants).GetFields(
                BindingFlags.Public | BindingFlags.Static
            )
        )
        {
            if (
                field.Name.StartsWith("Event", StringComparison.Ordinal)
                && field.FieldType == typeof(int)
                && field.IsLiteral
            )
            {
                int id = (int)field.GetRawConstantValue()!;
                if (id > 0)
                {
                    data.Add(field.Name, id);
                }
            }
        }

        Assert.True(data.Count > 30, "no event constants found — did they get renamed?");
        return data;
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void WritingTheMaskDoesNotAdvanceTheEventStream(string name, int eventId)
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        RunNonCombatEffects.BeginEvent(engine.State, eventId);

        int before = engine.State.EventRngStream?.CallCount ?? 0;
        var first = new int[RunConstants.MaxActions];
        engine.WriteActionMask(first);

        for (int i = 0; i < 4; i++)
        {
            var again = new int[RunConstants.MaxActions];
            engine.WriteActionMask(again);
            Assert.Equal(first, again);
        }

        Assert.Equal(before, engine.State.EventRngStream?.CallCount ?? 0);
        Assert.Equal(name, name);
    }

    /// <summary>
    /// Entering the same event on the same seed twice must produce the same event.
    /// A cached roll that outlived its event would break this.
    /// </summary>
    [Theory]
    [MemberData(nameof(Events))]
    public void EnteringTheSameEventTwiceRollsTheSameThing(string name, int eventId)
    {
        static (int, int, int) Enter(int eventId)
        {
            var engine = new RunEngine();
            engine.Reset("ABCDEF");
            RunNonCombatEffects.BeginEvent(engine.State, eventId);
            var mask = new int[RunConstants.MaxActions];
            engine.WriteActionMask(mask);
            return (
                engine.State.EventValue0 ?? int.MinValue,
                engine.State.EventValue1 ?? int.MinValue,
                engine.State.EventRngStream?.CallCount ?? 0
            );
        }

        Assert.Equal(Enter(eventId), Enter(eventId));
        Assert.Equal(name, name);
    }
}
