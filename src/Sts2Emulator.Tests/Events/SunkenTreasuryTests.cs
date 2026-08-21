using System.Text.Json;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Sunken Treasury's two chests, whose gold is rolled from the event's own stream.
///
/// The amounts are the point of the event, and they are checkable against the game
/// without playing it: the option text states them. That is what caught the seed being
/// wrong -- the emulator added the player slot index as 1 where a solo run's only player
/// is slot 0, so both chests paid a few gold over.
/// </summary>
public class SunkenTreasuryTests
{
    private static JsonElement Fixture()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "fixtures",
            "events",
            "SunkenTreasury-options.json"
        );
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void BothChestsPayWhatTheGameOffered()
    {
        var fixture = Fixture();
        var engine = new RunEngine();
        engine.Reset(fixture.GetProperty("seed").GetString()!);
        engine.State.EventId = RunConstants.EventSunkenTreasury;

        // "Gain 63 Gold." / "Gain 340 Gold. Receive Greed." -- the game's own words.
        var descriptions = fixture
            .GetProperty("options")
            .EnumerateArray()
            .Select(option => option.GetProperty("description").GetString() ?? "")
            .ToArray();

        Assert.Contains(
            $"{RunNonCombatEffects.SunkenTreasurySmallChestGold(engine.State)} Gold",
            descriptions[0]
        );
        Assert.Contains(
            $"{RunNonCombatEffects.SunkenTreasuryLargeChestGold(engine.State)} Gold",
            descriptions[1]
        );
    }

    [Fact]
    public void TheChestsAreTheAmountsTheGameRolled()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        engine.State.EventId = RunConstants.EventSunkenTreasury;

        Assert.Equal(63, RunNonCombatEffects.SunkenTreasurySmallChestGold(engine.State));
        Assert.Equal(340, RunNonCombatEffects.SunkenTreasuryLargeChestGold(engine.State));
    }
}
