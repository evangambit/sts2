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
        "Apotheosis",
        "Apparition",
        "BansheesCry",
        "Beckon",
        "Begone",
        "BlackHole",
        "Bodyguard",
        "Bombardment",
        "BoneShards",
        "BrightestFlame",
        "BundleOfJoy",
        "Burn",
        "Bury",
        "ByrdSwoop",
        "Caltrops",
        "Cascade",
        "CelestialMight",
        "Charge",
        "Clash",
        "CloakOfStars",
        "Comet",
        "CrashLanding",
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
        "FranticEscape",
        "Fuel",
        "GammaBlast",
        "GatherLight",
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
        "Outmaneuver",
        "PaleBlueDot",
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
        "Reflect",
        "Relax",
        "Resonance",
        "RightHandHand",
        "RipAndTear",
        "Royalties",
        "Sacrifice",
        "Scourge",
        "SevenStars",
        "Severance",
        "ShiningStrike",
        "SolarStrike",
        "Soul",
        "Sow",
        "SpectrumShift",
        "SpoilsMap",
        "SporeMind",
        "Spur",
        "Squash",
        "Squeeze",
        "Stack",
        "Stardust",
        "StrikeNecrobinder",
        "StrikeRegent",
        "Supermassive",
        "SweepingGaze",
        "SwordSage",
        "Terraforming",
        "TheScythe",
        "TheSealedThrone",
        "TimesUp",
        "ToricToughness",
        "Toxic",
        "Tyranny",
        "Undeath",
        "Unleash",
        "VoidForm",
        "Whistle",
        "Wish",
        "Wisp",
        "Wither",
        "Wound",
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
