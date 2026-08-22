using System.Reflection;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Holds the line between "the emulator can run this event" and "we know it runs it
/// right". An event counts as covered when a <c>&lt;Event&gt;Tests</c> class exists, or
/// when some suite declares it with <see cref="CoversEventAttribute"/>; everything still
/// uncovered is listed in <see cref="Pending"/>, so adding an event without either
/// testing it or deliberately deferring it breaks the build.
///
/// The second shape exists because several events are only interesting as a GROUP -- the
/// ones that end in a card selection, the ones that offer a potion -- and the thing worth
/// testing is the shared mechanic driven past the screen. Splitting those into one class
/// per event would duplicate the driving code and hide what they have in common, so the
/// suite names the events it drives instead. The gate stays mechanical either way: it
/// still needs a real test class, it just lets one class speak for several events.
///
/// This is the third of the coverage twins, after <c>CardCoverageTests</c> and
/// <c>CombatCoverageTests</c>, and works the same way: <see cref="Pending"/> is a
/// burn-down list, not a config knob. It starts full, because events are the layer
/// with no per-element tests at all -- the whole list is the backlog.
///
/// What an event test owes, in rough order of what has actually been wrong before:
/// which options the event offers and in what order (they are not fixed -- most events
/// hide or lock options the run cannot afford), what each option does to hp, gold, deck
/// and relics, and which RNG stream it draws from. Self-Help Book is the cautionary
/// tale: the emulator offered it, took the choice, and then every hand-written card
/// ignored the enchantment it granted.
///
/// Expected values come from <c>decompiled/</c> or a live capture via
/// <c>scripts/capture_event.py</c>, never from the emulator's own output.
/// </summary>
public class EventCoverageTests
{
    /// <summary>
    /// Events the emulator can run that no test exercises yet. Every entry is an event
    /// the emulator will happily run wrongly in silence.
    /// </summary>
    private static readonly HashSet<string> Pending =
    [
        "AbyssalBaths",
        "Amalgamator",
        "BattlewornDummy",
        "BrainLeech",
        "Bugslayer",
        "ByrdonisNest",
        "ColorfulPhilosophers",
        "ColossalFlower",
        "DenseVegetation",
        "DollRoom",
        "EndlessConveyor",
        // No option-list capture exists for this one: it presents as its own
        // "fake_merchant" state with a shop of fake relics rather than a list of
        // options, so it needs a shop-shaped capture. See scripts/capture_event.py.
        "FakeMerchant",
        "FieldOfManSizedHoles",
        "GraveOfTheForgotten",
        "HungryForMushrooms",
        "InfestedAutomaton",
        "JungleMazeAdventure",
        "LostWisp",
        "PunchOff",
        "RanwidTheElder",
        "Reflections",
        "RelicTrader",
        "RoomFullOfCheese",
        "RoundTeaParty",
        "SapphireSeed",
        "SelfHelpBook",
        "SlipperyBridge",
        "SpiritGrafter",
        "Symbiote",
        "TabletOfTruth",
        "TheFutureOfPotions",
        "TheLanternKey",
        "ThisOrThat",
        "TinkerTime",
        "Trial",
        "UnrestSite",
        "WarHistorianRepy",
        "WelcomeToWongos",
        "ZenWeaver",
    ];

    [Fact]
    public void EveryModelledEventHasATestSuite()
    {
        var missing = ImplementedEvents
            .Names.Where(name => !HasSuite(name) && !Pending.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Modelled with no <Event>Tests class: {string.Join(", ", missing)}. "
                + "Add the tests, or add the event to EventCoverageTests.Pending to defer it."
        );
    }

    [Fact]
    public void PendingListHasNoEventThatIsNowTested()
    {
        var stale = Pending.Where(HasSuite).OrderBy(name => name).ToList();

        Assert.True(
            stale.Count == 0,
            $"Now tested, so remove from EventCoverageTests.Pending: {string.Join(", ", stale)}."
        );
    }

    [Fact]
    public void PendingListHasNoEventThatIsNotModelled()
    {
        var unknown = Pending.Except(ImplementedEvents.Names).OrderBy(name => name).ToList();

        Assert.True(
            unknown.Count == 0,
            $"Not an event RunEngine.StepEvent runs: {string.Join(", ", unknown)}. "
                + "Re-run scripts/generate_event_coverage.py."
        );
    }

    private static bool HasSuite(string eventName) =>
        Assembly.GetExecutingAssembly().GetType($"Sts2Emulator.Tests.{eventName}Tests") != null
        || DeclaredCoverage.Contains(eventName);

    /// <summary>Every event named by a <see cref="CoversEventAttribute"/> anywhere in the suite.</summary>
    private static readonly HashSet<string> DeclaredCoverage = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .SelectMany(type => type.GetCustomAttributes<CoversEventAttribute>())
        .Select(attribute => attribute.EventName)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every declared name has to be an event the emulator actually runs, so a typo or a
    /// renamed event fails rather than silently claiming coverage of nothing.
    /// </summary>
    [Fact]
    public void EveryDeclaredCoverageNamesARealEvent()
    {
        var unknown = DeclaredCoverage.Except(ImplementedEvents.Names).Order().ToList();

        Assert.True(
            unknown.Count == 0,
            $"[CoversEvent] names no such event: {string.Join(", ", unknown)}."
        );
    }
}

/// <summary>
/// Declares that this suite tests the named event, for suites organised around a shared
/// mechanic rather than around one event. Put it only on a class that actually drives the
/// event through <c>RunEngine.Step</c> -- it is a claim about coverage, and
/// <see cref="EventCoverageTests"/> believes it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CoversEventAttribute(string eventName) : Attribute
{
    public string EventName { get; } = eventName;
}
