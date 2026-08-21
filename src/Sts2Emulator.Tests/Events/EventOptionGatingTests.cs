using System.Reflection;
using System.Text.Json;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Which options each Act 1 event puts in front of the player, checked against what the
/// game put in front of it.
///
/// An event's option list is a fixed-size array -- an option the run cannot take is a
/// locked variant occupying the same slot, not a missing entry -- so "which options" is
/// two separate questions: how many there are, and which of them are takeable. The
/// emulator got both wrong. Its fallback offered a flat four actions to any event it had
/// no bespoke case for, so an agent could pick a third option at the many events that
/// only have two; and three events gate their options on run state that was not modelled
/// at all.
///
/// This covers the offer, not the outcome. What each option *does* is still untested,
/// which is why the events stay on EventCoverageTests.Pending: a per-event suite owes
/// the effects as well.
/// </summary>
public class EventOptionGatingTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "tests",
        "fixtures",
        "events"
    );

    private static Dictionary<string, int> EventIds() =>
        typeof(RunConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("Event") && field.FieldType == typeof(int))
            .ToDictionary(
                field => field.Name["Event".Length..],
                field => (int)field.GetValue(null)!
            );

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(FixtureDir, "*-options.json").OrderBy(p => p))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void OffersExactlyTheOptionsTheGameLeftUnlocked(string fixtureName)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDir, fixtureName))
        );
        var root = document.RootElement;
        string name = root.GetProperty("event").GetString()!;
        var ids = EventIds();
        Assert.True(ids.ContainsKey(name), $"No RunConstants.Event{name} for {fixtureName}");

        var engine = new RunEngine();
        engine.Reset(root.GetProperty("seed").GetString()!);

        // The capture was taken on a fresh run, so the emulator's own fresh run has to
        // agree on the state the gating reads before any comparison of the gating means
        // anything.
        var player = root.GetProperty("before").GetProperty("player");
        Assert.Equal(player.GetProperty("gold").GetInt32(), engine.State.Gold);
        Assert.Equal(player.GetProperty("hp").GetInt32(), engine.State.PlayerHp);
        Assert.Equal(root.GetProperty("floor").GetInt32(), engine.State.Floor);

        engine.State.EventId = ids[name];
        engine.State.Phase = RunPhase.Event;
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        // Compare the whole action range, not just the slots the event has: offering an
        // option the event does not have is the failure this exists to catch, and it
        // lives past the end of the option list.
        var options = root.GetProperty("options");
        var expected = options
            .EnumerateArray()
            .Where(option => !option.GetProperty("is_locked").GetBoolean())
            .Select(option => option.GetProperty("index").GetInt32())
            // The emulator also offers a "leave without choosing" action that most
            // events do not actually offer. That is its own divergence, tracked
            // separately; here it is expected so this test can be about the options.
            .Append(RunConstants.EventSkipAction)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        var actual = Enumerable
            .Range(0, RunConstants.EventSkipAction + 1)
            .Where(index => mask[index] != 0)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
