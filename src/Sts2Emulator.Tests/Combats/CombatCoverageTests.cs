using System.Reflection;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Holds the line between "the emulator can build this encounter" and "we know it builds
/// it right". An encounter counts as covered when a <c>&lt;Encounter&gt;Tests</c> class
/// exists; everything still uncovered is listed in <see cref="Pending"/>, so adding an
/// encounter without either testing it or deliberately deferring it breaks the build.
///
/// This is the combat-side twin of <c>CardCoverageTests</c>, and works the same way:
/// <see cref="Pending"/> is a burn-down list, not a config knob.
///
/// What an encounter test owes, in rough order of what has actually been wrong before:
/// the roster (which enemies, how many, in what order), their HP at a known seed and
/// ascension, the opening intents, and the move cycle over enough turns to see it repeat.
/// Ascension is an input, not a constant — the same encounter deals different damage at
/// A8 and A10, and transcribing one branch for both is a defect this suite has already
/// had.
/// </summary>
public class CombatCoverageTests
{
    /// <summary>
    /// Encounters the emulator can build that no test exercises yet. Every entry is a
    /// fight the emulator will happily run wrongly in silence.
    /// </summary>
    private static readonly HashSet<string> Pending =
    [
        "Aeonglass",
        "Architect",
        "Axebot",
        "BattlewornDummy1",
        "BattlewornDummy2",
        "BattlewornDummy3",
        "Bowlbugs",
        "BowlbugsWeak",
        "BygoneEffigy",
        "Byrdonis",
        "CeremonialBeast",
        "Chompers",
        "ConstructMenagerie",
        "CorpseSlugs",
        "CubexConstruct",
        "CultistAndSeapunk",
        "Cultists",
        "Decimillipede",
        "DenseVegetation",
        "DevotedSculptor",
        "Entomancer",
        "Exoskeletons",
        "Fabricator",
        "FakeMerchant",
        "FlyconidNormal",
        "Fogmog",
        "FossilStalker",
        "FrogKnight",
        "FuzzyWurmCrawler",
        "GlobeHead",
        "GremlinMerc",
        "HunterKiller",
        "InfestedPrisms",
        "Inklets",
        "KaiserCrab",
        "Knights",
        "KnowledgeDemon",
        "LagavulinMatriarch",
        "LivingFog",
        "LostAndForgotten",
        "LouseProgenitor",
        "Mawler",
        "MechaKnight",
        "MysteriousKnight",
        "Mytes",
        "NibbitsNormal",
        "NibbitsWeak",
        "Obscura",
        "OvergrowthCrawlers",
        "Ovicopter",
        "OwlMagistrate",
        "PhantasmalGardeners",
        "PhrogParasite",
        "PunchConstruct",
        "PunchOff",
        "Queen",
        "RubyRaiders",
        "Scrolls",
        "ScrollsWeak",
        "Seapunk",
        "ShrinkerBeetle",
        "SkulkingColony",
        "SlimedBerserker",
        "SlimesNormal",
        "SlimesWeak",
        "SlitheringStrangler",
        "SludgeSpinner",
        "SlumberingBeetle",
        "SnappingJaxfruitNormal",
        "SoulFysh",
        "SoulNexus",
        "SpinyToad",
        "TerrorEel",
        "TestSubject",
        "TheInsatiable",
        "TheKin",
        "ThievingHopper",
        "Tunneler",
        "TunnelerAndChomper",
        "TurretOperator",
        "TwoTailedRats",
        "Vantom",
        "VineShambler",
        "WaterfallGiant",
    ];

    [Fact]
    public void EveryModelledEncounterHasATestSuite()
    {
        var missing = ImplementedEncounters
            .Names.Where(name => !HasSuite(name) && !Pending.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Modelled with no <Encounter>Tests class: {string.Join(", ", missing)}. "
                + "Add the tests, or add the encounter to CombatCoverageTests.Pending to defer it."
        );
    }

    [Fact]
    public void PendingListHasNoEncounterThatIsNowTested()
    {
        var stale = Pending.Where(HasSuite).OrderBy(name => name).ToList();

        Assert.True(
            stale.Count == 0,
            $"Now tested, so remove from CombatCoverageTests.Pending: {string.Join(", ", stale)}."
        );
    }

    [Fact]
    public void PendingListHasNoEncounterThatIsNotModelled()
    {
        var unknown = Pending.Except(ImplementedEncounters.Names).OrderBy(name => name).ToList();

        Assert.True(
            unknown.Count == 0,
            $"Not an encounter CombatFactory builds: {string.Join(", ", unknown)}. "
                + "Re-run scripts/generate_combat_coverage.py."
        );
    }

    private static bool HasSuite(string encounterName) =>
        Assembly.GetExecutingAssembly().GetType($"Sts2Emulator.Tests.{encounterName}Tests") != null;
}
