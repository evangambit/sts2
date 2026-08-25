using System.Reflection;
using System.Text.Json;
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
    ///
    /// Nothing in Act 1 is on this list any more: every encounter either act declares --
    /// weak, normal, elite and boss alike -- has a committed live capture that replays
    /// turn by turn. What is left is later-act content, which the emulator models well
    /// enough to build but has never been held to. That is not idle debt: sweeping a
    /// sample of it found eleven of fifteen diverging on intents or move cycles, so
    /// these are known-wrong rather than merely unchecked, and the captures were not
    /// committed precisely because they would have been committing failures.
    /// </summary>
    private static readonly HashSet<string> Pending =
    [
        "Aeonglass",
        "Architect",
        "Axebot",
        "BattlewornDummy1",
        "BattlewornDummy2",
        "BattlewornDummy3",
        "DevotedSculptor",
        "Fabricator",
        "FrogKnight",
        "GlobeHead",
        "Knights",
        "LostAndForgotten",
        "MechaKnight",
        "OwlMagistrate",
        "Queen",
        "Scrolls",
        "ScrollsWeak",
        "SlimedBerserker",
        "SoulNexus",
        "TestSubject",
        "TurretOperator",
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

    /// <summary>
    /// An encounter is covered by a C# suite, or by a committed live fight capture.
    ///
    /// The capture is the stronger of the two: tests/python/test_live_fixtures.py builds
    /// a case per fixture that replays the whole fight offline turn by turn, and every
    /// expected value in it is the game's rather than a transcription of the decompile.
    /// It supplies exactly what this guard asks an encounter test for -- the roster, the
    /// HP at a known seed and ascension, the opening intents, and the move cycle over
    /// enough turns to see it repeat -- so demanding a hand-written suite alongside one
    /// would be asking for a weaker copy of it.
    /// </summary>
    private static bool HasSuite(string encounterName) =>
        Assembly.GetExecutingAssembly().GetType($"Sts2Emulator.Tests.{encounterName}Tests") != null
        || CapturedFights.Contains(encounterName);

    /// <summary>
    /// Encounter names, in enum spelling, that a committed fight capture already covers.
    /// The fixtures name their encounter in kebab case, which is the enum name lowered
    /// and hyphenated.
    /// </summary>
    private static readonly HashSet<string> CapturedFights = LoadCapturedFights();

    private static HashSet<string> LoadCapturedFights()
    {
        string dir = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "fixtures",
            "combat"
        );
        var covered = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(dir))
        {
            return covered;
        }

        foreach (string path in Directory.GetFiles(dir, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (
                !document.RootElement.TryGetProperty("turn_trace", out var trace)
                || trace.GetArrayLength() == 0
                || !document.RootElement.TryGetProperty("capture", out var capture)
                || !capture.TryGetProperty("encounter", out var encounter)
            )
            {
                continue;
            }

            // The fixture names its encounter three ways and the enum spells it like
            // one of them: kebab from the sweep, and the game's own class name with a
            // pool suffix. The enum keeps that suffix for some (FlyconidNormal) and
            // drops it for others (SeapunkWeak is just Seapunk), so offer both --
            // PendingListHasNoEncounterThatIsNotModelled keeps the list honest either
            // way, and a name that matches nothing simply never matches.
            covered.Add(
                string.Concat(
                    (encounter.GetString() ?? "")
                        .Split('-')
                        .Select(part =>
                            part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]
                        )
                )
            );

            if (capture.TryGetProperty("live_encounter", out var live))
            {
                string name = live.GetString() ?? "";
                covered.Add(name);
                foreach (string suffix in (string[])["Elite", "Boss", "Weak", "Normal"])
                {
                    if (name.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        covered.Add(name[..^suffix.Length]);
                    }
                }
            }
        }

        return covered;
    }
}
