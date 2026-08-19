using System.Reflection;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Holds the line between "the emulator plays this card" and "we know it plays it
/// right". A card counts as covered when a <c>&lt;CardName&gt;Tests</c> class exists;
/// everything still uncovered is listed in <see cref="Pending"/>, so implementing a
/// card without either testing it or deliberately deferring it breaks the build.
///
/// <see cref="Pending"/> is a burn-down list, not a config knob: delete the name when
/// the tests land. Growing it is a decision worth arguing about in review.
/// </summary>
public class CardCoverageTests
{
    /// <summary>
    /// Cards implemented in <c>CardEffects.Apply</c> that no test exercises yet.
    /// Every entry is a card the emulator will happily play wrongly in silence.
    /// </summary>
    private static readonly HashSet<string> Pending =
    [
        "Abrasive",
        "Accelerant",
        "Accuracy",
        "Acrobatics",
        "Adrenaline",
        "Afterimage",
        "Alchemize",
        "Anointed",
        "Anticipate",
        "Assassinate",
        "Automation",
        "Backflip",
        "BeaconOfHope",
        "Beckon",
        "BelieveInYou",
        "BladeDance",
        "BladeOfInk",
        "Blur",
        "BouncingFlask",
        "Brand",
        "BubbleBubble",
        "BulletTime",
        "Burn",
        "Burst",
        "Calamity",
        "CalculatedGamble",
        "CloakAndDagger",
        "Conflagration",
        "CorrosiveWave",
        "CrimsonMantle",
        "Cruelty",
        "DaggerSpray",
        "DaggerThrow",
        "DeadlyPoison",
        "Debris",
        "DefendSilent",
        "Deflect",
        "Discovery",
        "Disintegration",
        "DodgeAndRoll",
        "EchoingSlash",
        "Enthralled",
        "Entropy",
        "Envenom",
        "EscapePlan",
        "EternalArmor",
        "Expertise",
        "Expose",
        "FanOfKnives",
        "Fasten",
        "FeelNoPain",
        "FiendFire",
        "Finesse",
        "Flanking",
        "FlickFlack",
        "Footwork",
        "FranticEscape",
        "GoldAxe",
        "GrandFinale",
        "HandOfGreed",
        "HandTrick",
        "Haze",
        "HiddenDaggers",
        "HuddleUp",
        "Impatience",
        "Infection",
        "InfiniteBlades",
        "JackOfAllTrades",
        "KnifeTrap",
        "LeadingStrike",
        "LegSweep",
        "Malaise",
        "MasterOfStrategy",
        "MasterPlanner",
        "Mayhem",
        "Mimic",
        "Mirage",
        "Murder",
        "Neutralize",
        "Nightmare",
        "NotYet",
        "NoxiousFumes",
        "Offering",
        "Outbreak",
        "PactsEnd",
        "Panache",
        "PanicButton",
        "PhantomBlades",
        "PiercingWail",
        "PoisonedStab",
        "Predator",
        "PrepTime",
        "Prepared",
        "PrimalForce",
        "Production",
        "Prowess",
        "Purity",
        "Pyre",
        "Reflex",
        "Rend",
        "Ricochet",
        "RollingBoulder",
        "Rupture",
        "Scare",
        "Scrawl",
        "SecretTechnique",
        "SecretWeapon",
        "SerpentForm",
        "ShadowStep",
        "Shadowmeld",
        "Shockwave",
        "Slice",
        "Snakebite",
        "Sneaky",
        "Speedster",
        "SpoilsMap",
        "Stoke",
        "StormOfSteel",
        "Stratagem",
        "StrikeSilent",
        "SuckerPunch",
        "Suppress",
        "Survivor",
        "Tactician",
        "Tank",
        "TheBomb",
        "TheHunt",
        "ThinkingAhead",
        "ToolsOfTheTrade",
        "Toxic",
        "Tracking",
        "Unmovable",
        "Untouchable",
        "UpMySleeve",
        "WellLaidPlans",
        "Wither",
        "Wound",
        "WraithForm",
    ];

    [Fact]
    public void EveryImplementedCardHasATestSuite()
    {
        var missing = ImplementedCards
            .Names.Where(name => !HasSuite(name) && !Pending.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Implemented with no <Name>Tests class: {string.Join(", ", missing)}. "
                + "Add the tests, or add the card to CardCoverageTests.Pending to defer it."
        );
    }

    [Fact]
    public void PendingListHasNoCardThatIsNowTested()
    {
        var stale = Pending.Where(HasSuite).OrderBy(name => name).ToList();

        Assert.True(
            stale.Count == 0,
            $"Now tested, so remove from CardCoverageTests.Pending: {string.Join(", ", stale)}."
        );
    }

    [Fact]
    public void PendingListHasNoCardThatIsNotImplemented()
    {
        var unknown = Pending.Except(ImplementedCards.Names).OrderBy(name => name).ToList();

        Assert.True(
            unknown.Count == 0,
            $"In CardCoverageTests.Pending but not implemented (renamed or dropped?): "
                + $"{string.Join(", ", unknown)}."
        );
    }

    private static bool HasSuite(string cardName) =>
        Assembly.GetExecutingAssembly().GetType($"Sts2Emulator.Tests.{cardName}Tests") != null;
}
