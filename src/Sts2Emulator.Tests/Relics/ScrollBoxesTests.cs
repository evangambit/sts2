using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Scroll Boxes: two bundles of three cards, and the player takes one whole.
/// </summary>
/// <remarks>
/// <c>GenerateRandomBundles</c> draws six cards off <c>PlayerRng.Rewards</c> — two Commons
/// and an Uncommon per bundle, all six distinct — and <c>FromChooseABundleScreen</c> offers
/// them. The emulator used to hand over three cards rolled off <c>Rng.UpFront</c>: the
/// wrong stream, the wrong number of draws, six cards' worth of choice collapsed to none,
/// and no screen at all. It is the last of the twenty-five Neow blessings to be captured.
/// </remarks>
public class ScrollBoxesTests
{
    private static RunEngine AtScrollBoxes(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Ancient;
        engine.State.NeowOptions[0] = RunConstants.RelicScrollBoxes;
        engine.Step(0, -1, out _, out _, out _);
        return engine;
    }

    private static List<string> Entries(RunEngine engine, int bundle) =>
        Enumerable
            .Range(0, 3)
            .Select(i => GeneratedData.Cards.Get(engine.State.BundleOffer[bundle * 3 + i]).Entry)
            .ToList();

    [Fact]
    public void ItOpensABundleScreenRatherThanGrantingCards()
    {
        var engine = AtScrollBoxes("ZY1E5128P6");

        Assert.Equal(RunPhase.BundleSelect, engine.State.Phase);
        Assert.Equal(6, engine.State.BundleOffer.Length);
        // Ascender's Bane and the starting ten, and nothing else yet.
        Assert.Equal(11, engine.State.Deck.Count);
    }

    /// <summary>
    /// The live capture of this seed is offered True Grit / Sword Boomerang / Dominate
    /// against Pommel Strike / Body Slam / Burning Pact.
    /// </summary>
    [Fact]
    public void TheBundlesMatchTheCapturedRun()
    {
        var engine = AtScrollBoxes("ZY1E5128P6");

        Assert.Equal(
            new List<string> { "TRUE_GRIT", "SWORD_BOOMERANG", "DOMINATE" },
            Entries(engine, 0)
        );
        Assert.Equal(
            new List<string> { "POMMEL_STRIKE", "BODY_SLAM", "BURNING_PACT" },
            Entries(engine, 1)
        );
    }

    /// <summary>
    /// Two Commons and an Uncommon per bundle, and the used-card set spans BOTH — so the
    /// second bundle draws from a pool three cards smaller and can never repeat the first.
    /// </summary>
    [Theory]
    [InlineData("ZY1E5128P6")]
    [InlineData("J09SPL8Y3V")]
    [InlineData("RRRR6WR3C4")]
    public void AllSixCardsAreDistinctAndCorrectlyRare(string seed)
    {
        var engine = AtScrollBoxes(seed);
        var offer = engine.State.BundleOffer;

        Assert.Equal(6, offer.Distinct().Count());
        foreach (int bundle in new[] { 0, 1 })
        {
            Assert.Equal(
                [CardRarity.Common, CardRarity.Common, CardRarity.Uncommon],
                Enumerable
                    .Range(0, 3)
                    .Select(i => GeneratedData.Cards.Get(offer[bundle * 3 + i]).Rarity)
            );
        }
    }

    /// <summary>
    /// The screen takes two actions, the way the game's does: highlight, then confirm.
    /// </summary>
    [Fact]
    public void ItTakesASelectionAndThenAConfirmation()
    {
        var engine = AtScrollBoxes("ZY1E5128P6");
        int before = engine.State.Deck.Count;
        var wanted = Entries(engine, 1);

        engine.Step(1, -1, out _, out _, out _);
        Assert.Equal(RunPhase.BundleSelect, engine.State.Phase);
        Assert.Equal(1, engine.State.SelectedBundle);
        Assert.Equal(before, engine.State.Deck.Count);

        engine.Step(RunConstants.BundleConfirmAction, -1, out _, out _, out _);

        Assert.Equal(before + 3, engine.State.Deck.Count);
        foreach (string entry in wanted)
        {
            Assert.Contains(
                engine.State.Deck,
                card => GeneratedData.Cards.Get(card.DefId).Entry == entry
            );
        }

        // A screen Neow opened returns to Neow, which stays up for one more Proceed.
        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
    }

    /// <summary>The highlight moves; confirming before choosing is refused.</summary>
    [Fact]
    public void ConfirmingWithNothingHighlightedIsRejected()
    {
        var engine = AtScrollBoxes("ZY1E5128P6");

        Assert.Equal(-1, engine.Step(RunConstants.BundleConfirmAction, -1, out _, out _, out _));

        engine.Step(0, -1, out _, out _, out _);
        engine.Step(1, -1, out _, out _, out _);
        Assert.Equal(1, engine.State.SelectedBundle);
    }
}
