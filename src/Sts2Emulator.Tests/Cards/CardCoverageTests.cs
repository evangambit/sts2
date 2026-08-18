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
        "Anger",
        "Anointed",
        "Anticipate",
        "AshenStrike",
        "Assassinate",
        "Automation",
        "Backflip",
        "Backstab",
        "BattleTrance",
        "BeaconOfHope",
        "Beckon",
        "BelieveInYou",
        "BladeDance",
        "BladeOfInk",
        "BloodWall",
        "Bloodletting",
        "Blur",
        "BodySlam",
        "BouncingFlask",
        "Brand",
        "BubbleBubble",
        "BulletTime",
        "Bully",
        "Burn",
        "BurningPact",
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
        "Dash",
        "DeadlyPoison",
        "Debris",
        "DefendSilent",
        "Deflect",
        "DemonicShield",
        "Discovery",
        "Disintegration",
        "Dismantle",
        "DodgeAndRoll",
        "Dominate",
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
        "Finisher",
        "FlameBarrier",
        "Flanking",
        "FlashOfSteel",
        "Flechettes",
        "FlickFlack",
        "Footwork",
        "FranticEscape",
        "GangUp",
        "GoldAxe",
        "GrandFinale",
        "HandOfGreed",
        "HandTrick",
        "Haze",
        "Headbutt",
        "Hemokinesis",
        "HiddenDaggers",
        "HowlFromBeyond",
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
        "MementoMori",
        "Mimic",
        "MindBlast",
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
        "PerfectedStrike",
        "PhantomBlades",
        "PiercingWail",
        "Pinpoint",
        "PoisonedStab",
        "PommelStrike",
        "Pounce",
        "PreciseCut",
        "Predator",
        "PrepTime",
        "Prepared",
        "PrimalForce",
        "Production",
        "Prowess",
        "Purity",
        "Pyre",
        "Rage",
        "Rampage",
        "Reflex",
        "Rend",
        "Ricochet",
        "RollingBoulder",
        "Rupture",
        "Scare",
        "Scrawl",
        "SecondWind",
        "SecretTechnique",
        "SecretWeapon",
        "SerpentForm",
        "ShadowStep",
        "Shadowmeld",
        "Shockwave",
        "ShrugItOff",
        "Skewer",
        "Slice",
        "Snakebite",
        "Sneaky",
        "Speedster",
        "SpoilsMap",
        "Stoke",
        "StormOfSteel",
        "Strangle",
        "Stratagem",
        "StrikeSilent",
        "SuckerPunch",
        "Suppress",
        "Survivor",
        "SwordBoomerang",
        "Tactician",
        "Tank",
        "Taunt",
        "TheBomb",
        "TheHunt",
        "ThinkingAhead",
        "Thunderclap",
        "ToolsOfTheTrade",
        "Toxic",
        "Tracking",
        "Tremble",
        "TrueGrit",
        "Unmovable",
        "Untouchable",
        "UpMySleeve",
        "Uppercut",
        "WellLaidPlans",
        "Whirlwind",
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
