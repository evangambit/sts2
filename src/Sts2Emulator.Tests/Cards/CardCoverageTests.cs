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
        "Afterlife",
        "Alignment",
        "Apotheosis",
        "Apparition",
        "Arsenal",
        "AstralPulse",
        "BansheesCry",
        "BeatIntoShape",
        "Beckon",
        "Begone",
        "BigBang",
        "BlackHole",
        "Bodyguard",
        "Bombardment",
        "BoneShards",
        "BrightestFlame",
        "Bulwark",
        "BundleOfJoy",
        "Burn",
        "Bury",
        "ByrdSwoop",
        "Caltrops",
        "CaptureSpirit",
        "Cascade",
        "CelestialMight",
        "Charge",
        "ChildOfTheStars",
        "Clash",
        "Cleanse",
        "CloakOfStars",
        "CollisionCourse",
        "Comet",
        "Conqueror",
        "Convergence",
        "CosmicIndifference",
        "CrashLanding",
        "CrescentSpear",
        "CrushUnder",
        "DeathMarch",
        "Deathbringer",
        "DeathsDoor",
        "Debris",
        "DecisionsDecisions",
        "DefendNecrobinder",
        "DefendRegent",
        "Defile",
        "Defy",
        "Delay",
        "Devastate",
        "Disintegration",
        "Distraction",
        "DrainPower",
        "Dredge",
        "DualWield",
        "DyingStar",
        "Eidolon",
        "EndOfDays",
        "Enlightenment",
        "Enthralled",
        "Entrench",
        "Exterminate",
        "FallingStar",
        "Fear",
        "FeedingFrenzy",
        "Fetch",
        "Flatten",
        "ForegoneConclusion",
        "FranticEscape",
        "Fuel",
        "Furnace",
        "GammaBlast",
        "GatherLight",
        "Genesis",
        "Glimmer",
        "GlimpseBeyond",
        "Glitterstream",
        "Glow",
        "GraveWarden",
        "Graveblast",
        "Guards",
        "GuidingStar",
        "HammerTime",
        "HeavenlyDrill",
        "Hegemony",
        "HeirloomHammer",
        "HelloWorld",
        "HiddenCache",
        "HighFive",
        "IAmInvincible",
        "Infection",
        "KinglyKick",
        "KinglyPunch",
        "KnockoutBlow",
        "KnowThyPlace",
        "Largesse",
        "Luminesce",
        "LunarBlast",
        "MakeItSo",
        "ManifestAuthority",
        "Maul",
        "Metamorphosis",
        "MeteorShower",
        "MinionDiveBomb",
        "MinionSacrifice",
        "MinionStrike",
        "Misery",
        "MonarchsGaze",
        "Monologue",
        "NegativePulse",
        "NeutronAegis",
        "Outmaneuver",
        "PaleBlueDot",
        "Parry",
        "Parse",
        "ParticleWall",
        "Patter",
        "Peck",
        "PhotonCut",
        "PillarOfCreation",
        "Poke",
        "Prophesize",
        "Protector",
        "PullAggro",
        "Quasar",
        "Radiate",
        "Rattle",
        "Reanimate",
        "Reap",
        "Reave",
        "Rebound",
        "RefineBlade",
        "Reflect",
        "Relax",
        "Resonance",
        "RightHandHand",
        "RipAndTear",
        "RoyalGamble",
        "Royalties",
        "Sacrifice",
        "Scourge",
        "SculptingStrike",
        "SeekingEdge",
        "SevenStars",
        "Severance",
        "ShiningStrike",
        "SolarStrike",
        "Soul",
        "SovereignBlade",
        "Sow",
        "SpectrumShift",
        "SpoilsMap",
        "SpoilsOfBattle",
        "SporeMind",
        "Spur",
        "Squash",
        "Squeeze",
        "Stack",
        "Stardust",
        "StrikeNecrobinder",
        "StrikeRegent",
        "SummonForth",
        "Supermassive",
        "SweepingGaze",
        "SwordSage",
        "Terraforming",
        "TheScythe",
        "TheSealedThrone",
        "TheSmith",
        "TimesUp",
        "ToricToughness",
        "Toxic",
        "Transfigure",
        "Tyranny",
        "Undeath",
        "Unleash",
        "Venerate",
        "VoidForm",
        "Whistle",
        "Wish",
        "Wisp",
        "Wither",
        "Wound",
        "WroughtInWar",
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
