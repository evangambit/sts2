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
        "Clash",
        "CloakOfStars",
        "Comet",
        "CrashLanding",
        "DeathMarch",
        "Deathbringer",
        "DeathsDoor",
        "Debris",
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
        "GlimpseBeyond",
        "Glitterstream",
        "GraveWarden",
        "Graveblast",
        "GuidingStar",
        "Hegemony",
        "HeirloomHammer",
        "HelloWorld",
        "HighFive",
        "Infection",
        "KinglyKick",
        "KinglyPunch",
        "KnockoutBlow",
        "Luminesce",
        "MakeItSo",
        "ManifestAuthority",
        "Maul",
        "Metamorphosis",
        "MeteorShower",
        "MinionDiveBomb",
        "MinionSacrifice",
        "MinionStrike",
        "Misery",
        "NegativePulse",
        "Outmaneuver",
        "Parse",
        "Patter",
        "Peck",
        "Poke",
        "Protector",
        "PullAggro",
        "Radiate",
        "Rattle",
        "Reanimate",
        "Reap",
        "Reave",
        "Rebound",
        "Relax",
        "RightHandHand",
        "RipAndTear",
        "Sacrifice",
        "Scourge",
        "SevenStars",
        "Severance",
        "ShiningStrike",
        "SolarStrike",
        "Soul",
        "Sow",
        "SpoilsMap",
        "SporeMind",
        "Spur",
        "Squash",
        "Squeeze",
        "Stack",
        "StrikeNecrobinder",
        "StrikeRegent",
        "SweepingGaze",
        "TheScythe",
        "TimesUp",
        "ToricToughness",
        "Toxic",
        "Undeath",
        "Unleash",
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
