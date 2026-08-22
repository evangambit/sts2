using System.Reflection;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Holds the line between "the emulator can run this event" and "we know it runs it
/// right". An event counts as covered when a <c>&lt;Event&gt;Tests</c> class exists;
/// everything still uncovered is listed in <see cref="Pending"/>, so adding an event
/// without either testing it or deliberately deferring it breaks the build.
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
        "AromaOfChaos",
        "BattlewornDummy",
        "BrainLeech",
        "Bugslayer",
        "ByrdonisNest",
        "ColorfulPhilosophers",
        "ColossalFlower",
        "DenseVegetation",
        "DollRoom",
        "DoorsOfLightAndDark",
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
        "LuminousChoir",
        "MorphicGrove",
        "PotionCourier",
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
        "StoneOfAllTime",
        "Symbiote",
        "TabletOfTruth",
        "TheFutureOfPotions",
        "TheLanternKey",
        "TheLegendsWereTrue",
        "ThisOrThat",
        "TinkerTime",
        "Trial",
        "UnrestSite",
        "WarHistorianRepy",
        "WelcomeToWongos",
        "Wellspring",
        "WhisperingHollow",
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
        Assembly.GetExecutingAssembly().GetType($"Sts2Emulator.Tests.{eventName}Tests") != null;
}
