using System.Reflection;
using System.Text.Json;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What taking an event's option actually does, checked against what it did in the game.
///
/// The companion to <c>EventOptionGatingTests</c>, which only covers what an event
/// offers. Each fixture was captured by entering the event on a fresh run, taking one
/// option, and recording the run either side of it -- so what is asserted here is the
/// game's own before and after, never the emulator's.
///
/// Four things are compared, being what an event can move that the game reports
/// outside combat: the player's hp and max hp, their gold, their deck and their
/// relics. The screen the
/// choice leads to is compared as well, because half of these options do their work by
/// opening one -- a card select to transform, a reward to claim -- and routing to the
/// wrong screen is as wrong as the wrong number.
/// </summary>
public class EventOutcomeTests
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

    /// <summary>The emulator's phase, named the way the mod names its screens.</summary>
    private static string ScreenFor(RunPhase phase) =>
        phase switch
        {
            RunPhase.CardReward => "card_reward",
            RunPhase.Complete => "game_over",
            RunPhase.Event or RunPhase.Ancient => "event",
            RunPhase.Map => "map",
            RunPhase.RelicReward => "rewards",
            RunPhase.Rest => "rest_site",
            RunPhase.Shop => "shop",
            RunPhase.Treasure => "treasure",
            RunPhase.TransformSelect => "card_select",
            _ => "unknown",
        };

    /// <summary>
    /// Options whose outcome does not yet match the game. Every entry is an event the
    /// emulator will resolve wrongly in silence -- the wrong hp, the wrong gold, the
    /// wrong card, or the wrong screen next.
    ///
    /// A burn-down list, not a config knob: 53 of the 74 takeable Act 1 options were
    /// wrong the first time they were ever compared, which is what forty events written
    /// without a way to check them against the game looks like. Delete an entry when its
    /// event is fixed; <see cref="PendingListHasNoOptionThatNowMatches"/> fails if one
    /// lingers.
    ///
    /// What is left needs mechanics the emulator does not have: multi-page events, grid
    /// card selection, and the events with a screen of their own.
    /// </summary>
    private static readonly HashSet<string> Pending =
    [
        // What is left all needs a screen or a mechanic the emulator does not have. The
        // numbers and the gates are done: everything that was a wrong amount or a wrongly
        // refused option has been fixed.
        //
        // Crystal Sphere runs its own "crystal_sphere" minigame screen, which has no
        // phase here. Paying for it is refused outright, and Payment Plan's OUTCOME
        // already matches -- the deck gains the same card -- so opt1 fails on the screen
        // name alone.
        "CrystalSphere-opt0.json",
        "CrystalSphere-opt1.json",
        // Share Knowledge rolls five cards through the reward machinery and opens a grid
        // to keep one. RunState.RewardCards holds three, so the card-reward phase cannot
        // carry the offer; the emulator picks a card itself instead, and off the wrong
        // stream at that.
        "BrainLeech-opt0.json",
        // Gorge offers eight rolled Commons on a grid and takes two.
        "RoomFullOfCheese-opt0.json",
        // Grabbing off the belt pays 40 gold for whatever RollDish landed on, a weighted
        // dish machine that is not modelled. Refused rather than paying out a dish the
        // belt never had.
        "EndlessConveyor-opt0.json",
        // "I Can Take Them" starts a fight, which an event cannot do in the emulator yet.
        "PunchOff-opt1.json",
    ];

    // "-opt0.json" and friends, but not "-options.json": one character after "opt".
    private static IEnumerable<string> AllFixtures() =>
        Directory
            .GetFiles(FixtureDir, "*-opt?.json")
            .Select(path => Path.GetFileName(path))
            .Order();

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (string name in AllFixtures().Where(name => !Pending.Contains(name)))
        {
            data.Add(name);
        }

        return data;
    }

    [Fact]
    public void PendingListHasNoOptionThatNowMatches()
    {
        var matching = new List<string>();
        foreach (string name in AllFixtures().Where(Pending.Contains))
        {
            try
            {
                TakingTheOptionMovesTheRunTheWayTheGameDid(name);
                matching.Add(name);
            }
            catch (Exception)
            {
                // Still diverging, which is what the list says.
            }
        }

        Assert.True(
            matching.Count == 0,
            $"Now matching, so remove from EventOutcomeTests.Pending: {string.Join(", ", matching)}."
        );
    }

    [Fact]
    public void PendingListHasNoFixtureThatIsGone()
    {
        var unknown = Pending.Except(AllFixtures()).Order().ToList();

        Assert.True(
            unknown.Count == 0,
            $"No such fixture: {string.Join(", ", unknown)}. Re-capture it or drop the entry."
        );
    }

    private static Dictionary<string, int> EventIds() =>
        typeof(RunConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("Event") && field.FieldType == typeof(int))
            .ToDictionary(
                field => field.Name["Event".Length..],
                field => (int)field.GetValue(null)!
            );

    private static List<string> DeckSlugs(JsonElement player) =>
        player.TryGetProperty("deck", out var deck)
            ? deck.EnumerateArray()
                .Select(card => card.GetProperty("id").GetString() ?? "")
                .OrderBy(slug => slug, StringComparer.Ordinal)
                .ToList()
            : [];

    private static List<string> DeckSlugs(RunState state) =>
        state
            .Deck.Select(card => GeneratedData.Cards.Get(card.DefId).Entry)
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A relic name reduced to letters only and upper-cased, so the game's
    /// <c>SWORD_OF_STONE</c> and the emulator's <c>SwordOfStone</c> compare equal
    /// without either side having to guess where the other puts its word breaks.
    /// </summary>
    private static string RelicKey(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static List<string> RelicKeys(JsonElement player) =>
        player.TryGetProperty("relics", out var relics)
            ? relics
                .EnumerateArray()
                .Select(relic => RelicKey(relic.GetProperty("id").GetString() ?? ""))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList()
            : [];

    private static List<string> RelicKeys(RunState state) =>
        state
            .Relics.Select(relic => RelicKey(GeneratedData.Relics.Get(relic.DefId).Name))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TakingTheOptionMovesTheRunTheWayTheGameDid(string fixtureName)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDir, fixtureName))
        );
        var root = document.RootElement;
        string name = root.GetProperty("event").GetString()!;
        int chosen = root.GetProperty("chosen").GetInt32();

        var engine = new RunEngine();
        engine.Reset(root.GetProperty("seed").GetString()!);

        var before = root.GetProperty("before").GetProperty("player");
        Assert.Equal(before.GetProperty("gold").GetInt32(), engine.State.Gold);
        Assert.Equal(before.GetProperty("hp").GetInt32(), engine.State.PlayerHp);
        Assert.Equal(DeckSlugs(before), DeckSlugs(engine.State));
        Assert.Equal(RelicKeys(before), RelicKeys(engine.State));

        engine.State.EventId = EventIds()[name];
        engine.State.Phase = RunPhase.Event;
        int status = engine.Step(chosen, -1, out _, out _, out _);
        Assert.True(status == 0, $"{name} refused option {chosen}, which the game accepted");

        var after = root.GetProperty("after").GetProperty("player");
        Assert.Equal(after.GetProperty("hp").GetInt32(), engine.State.PlayerHp);
        Assert.Equal(after.GetProperty("max_hp").GetInt32(), engine.State.PlayerMaxHp);
        Assert.Equal(after.GetProperty("gold").GetInt32(), engine.State.Gold);
        Assert.Equal(DeckSlugs(after), DeckSlugs(engine.State));
        Assert.Equal(RelicKeys(after), RelicKeys(engine.State));
        Assert.Equal(
            root.GetProperty("after_state_type").GetString(),
            ScreenFor(engine.State.Phase)
        );
    }
}
