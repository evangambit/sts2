using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Amalgamator: two cards the PLAYER picks, removed, and one named card in their place.
/// </summary>
/// <remarks>
/// `FromDeckForRemoval(count: 2, filter: IsValid(tag, c))` then `CardPileCmd.Add` of
/// Ultimate Strike or Ultimate Defend. `IsValid` is the TAG plus BASIC rarity plus
/// removable, which is narrower than a plain removal in two directions at once — Ultimate
/// Strike is itself Strike-tagged, so without the rarity clause the event could eat its
/// own reward.
///
/// The emulator TRANSFORMED two Ironclad Strikes into random cards off the Transformations
/// stream: wrong effect, wrong stream, wrong card, and no choice. It also matched
/// Ironclad's Strike id rather than the tag, so for every other character it found nothing
/// and silently did nothing.
/// </remarks>
[CoversEvent("Amalgamator")]
public class AmalgamatorTests
{
    private static RunEngine At(string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventAmalgamator;
        return engine;
    }

    private static int Card(string name) => RunNonCombatEffects.NamedCard(name);

    [Theory]
    [InlineData(0, "UltimateStrike")]
    [InlineData(1, "UltimateDefend")]
    public void ItAsksForTwoCardsAndPaysWithOne(int option, string reward)
    {
        var engine = At();
        int before = engine.State.Deck.Count;

        engine.Step(option, -1, out _, out _, out _);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        // Two picks, taken from the front of whatever the filter offers.
        for (int i = 0; i < 2; i++)
        {
            int index = Enumerable
                .Range(0, engine.State.Deck.Count)
                .First(j => RunNonCombatEffects.CanSelectCard(engine.State, j));
            engine.Step(index, -1, out _, out _, out _);
        }

        Assert.Equal(before - 1, engine.State.Deck.Count);
        Assert.Contains(engine.State.Deck, c => c.DefId == Card(reward));
    }

    /// <summary>Only the matching tag is offered — Defends are not on the Strike screen.</summary>
    [Fact]
    public void TheScreenOffersOnlyTheTaggedBasics()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            var def = GeneratedData.Cards.Get(engine.State.Deck[i].DefId);
            Assert.Equal(
                def.StrikeTag && def.Rarity == CardRarity.Basic && !def.Eternal,
                RunNonCombatEffects.CanSelectCard(engine.State, i)
            );
        }
    }

    /// <summary>
    /// The reward is not a candidate for the next Amalgamator: Ultimate Strike carries the
    /// Strike tag and is Rare, and `IsValid` wants Basic.
    /// </summary>
    [Fact]
    public void ItCannotEatItsOwnReward()
    {
        var ultimate = GeneratedData.Cards.Get(Card("UltimateStrike"));

        Assert.True(ultimate.StrikeTag);
        Assert.NotEqual(CardRarity.Basic, ultimate.Rarity);
    }

    /// <summary>
    /// The tag is the real `CanonicalTags` entry now, not a `STRIKE_` name prefix. The
    /// prefix missed Perfected Strike, Twin Strike, Pommel Strike and fifteen more, and on
    /// the Defend side it missed Fasten — which is what Goopy is allowed to enchant.
    /// </summary>
    [Fact]
    public void TheTagsAreExtractedNotGuessedFromTheName()
    {
        Assert.True(GeneratedData.Cards.Get(Card("PerfectedStrike")).StrikeTag);
        Assert.True(GeneratedData.Cards.Get(Card("TwinStrike")).StrikeTag);
        Assert.True(GeneratedData.Cards.Get(Card("Fasten")).DefendTag);
        Assert.False(GeneratedData.Cards.Get(Card("Anger")).StrikeTag);
    }
}
