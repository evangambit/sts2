using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The events that end in a card selection, driven PAST the selection.
///
/// The live fixtures cannot reach here. A capture records the run at the moment the
/// selector opens, so everything the option does after the player picks -- the removal
/// itself, the second pick, the curse handed over in exchange -- is invisible to
/// <c>EventOutcomeTests</c>. Three deliberate regressions in that code went undetected by
/// the whole fixture suite, which is why this exists.
///
/// What each of these used to do instead of asking: remove the emulator's own idea of the
/// lowest-priority card, transform whichever card sat first in the deck, or upgrade it.
/// The choice IS the option, so choosing for the player was the defect.
/// </summary>
public class DeckSelectionEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int Selectable(RunEngine engine, int skip = 0) =>
        Enumerable
            .Range(0, engine.State.Deck.Count)
            .Where(i => RunNonCombatEffects.CanSelectCard(engine.State, i))
            .Skip(skip)
            .First();

    private static string Entry(RunState state, int index) =>
        GeneratedData.Cards.Get(state.Deck[index].DefId).Entry;

    private static int CountOf(RunState state, string name) =>
        state.Deck.Count(card =>
            card.DefId == RunNonCombatEffects.NamedCard(name)
        );

    // ── Luminous Choir ───────────────────────────────────────────────────────

    [Fact]
    public void ReachingIntoTheFleshRemovesTwoChosenCardsThenGivesTheSporeMind()
    {
        var engine = At(RunConstants.EventLuminousChoir);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.Equal(0, CountOf(engine.State, "SporeMind"));

        // First pick: one card gone, still on the screen, still no curse.
        string first = Entry(engine.State, Selectable(engine));
        Assert.Equal(0, engine.Step(Selectable(engine), -1, out _, out _, out _));
        Assert.Equal(deck - 1, engine.State.Deck.Count);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(0, CountOf(engine.State, "SporeMind"));

        // Second pick closes it and pays the curse.
        Assert.Equal(0, engine.Step(Selectable(engine), -1, out _, out _, out _));
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(deck - 2 + 1, engine.State.Deck.Count);
        Assert.Equal(1, CountOf(engine.State, "SporeMind"));
        Assert.NotNull(first);
    }

    [Fact]
    public void TheSporeMindArrivesOnlyAfterBothCardsAreGone()
    {
        var engine = At(RunConstants.EventLuminousChoir);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(2, engine.State.PendingSelectionCount);

        Assert.Equal(0, engine.Step(Selectable(engine), -1, out _, out _, out _));
        Assert.Equal(1, engine.State.PendingSelectionCount);
        Assert.Equal(0, CountOf(engine.State, "SporeMind"));
    }

    // ── The Wellspring ───────────────────────────────────────────────────────

    [Fact]
    public void BathingRemovesOneChosenCardAndAddsOneGuilty()
    {
        var engine = At(RunConstants.EventWellspring);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(deck, engine.State.Deck.Count);

        int index = Selectable(engine);
        string removed = Entry(engine.State, index);
        int copies = engine.State.Deck.Count(card =>
            GeneratedData.Cards.Get(card.DefId).Entry == removed
        );
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(deck, engine.State.Deck.Count); // one out, one Guilty in
        Assert.Equal(1, CountOf(engine.State, "Guilty"));
        Assert.Equal(
            copies - 1,
            engine.State.Deck.Count(card =>
                GeneratedData.Cards.Get(card.DefId).Entry == removed
            )
        );
    }

    /// <summary>
    /// Guilty is a real curse now, not the Ascender's Bane the emulator used to hand over
    /// for every curse in the game. The difference is not cosmetic: Ascender's Bane is
    /// Ethereal and Guilty is not.
    /// </summary>
    [Fact]
    public void TheCurseIsGuiltyAndNotAscendersBane()
    {
        var engine = At(RunConstants.EventWellspring);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(0, engine.Step(Selectable(engine), -1, out _, out _, out _));

        var guilty = GeneratedData.Cards.Get(RunNonCombatEffects.NamedCard("Guilty"));
        Assert.Equal("GUILTY", guilty.Entry);
        Assert.False(guilty.Ethereal);
        Assert.Equal(CardType.Curse, guilty.Type);
        Assert.Equal(1, CountOf(engine.State, "Guilty"));
    }

    // ── Aroma of Chaos ───────────────────────────────────────────────────────

    /// <summary>
    /// The transformed card goes to the BACK of the deck, not into the slot it came
    /// from: <c>CardCmd.Transform</c> records the original index but only uses it for
    /// combat piles -- for <c>PileType.Deck</c> it calls <c>pile.AddInternal(replacement)</c>
    /// with no index. So the check here is on what the deck HOLDS, not where.
    /// </summary>
    [Fact]
    public void LettingGoTransformsTheCardThePlayerPicks()
    {
        var engine = At(RunConstants.EventAromaOfChaos);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(DeckSelection.TransformToRandom, engine.State.PendingSelectionKind);

        int index = 3;
        int picked = engine.State.Deck[index].DefId;
        int copies = engine.State.Deck.Count(card => card.DefId == picked);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.Equal(copies - 1, engine.State.Deck.Count(card => card.DefId == picked));
        Assert.NotEqual(picked, engine.State.Deck[^1].DefId);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void MaintainingControlUpgradesTheCardThePlayerPicks()
    {
        var engine = At(RunConstants.EventAromaOfChaos);
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(DeckSelection.Upgrade, engine.State.PendingSelectionKind);

        int index = Selectable(engine, skip: 2);
        Assert.False(engine.State.Deck[index].Upgraded);
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.True(engine.State.Deck[index].Upgraded);
        Assert.Equal(1, engine.State.Deck.Count(card => card.Upgraded));
    }

    /// <summary>
    /// Only upgradable cards are offered for the upgrade, and every card for the
    /// transform -- two different sets from the same screen.
    /// </summary>
    [Fact]
    public void TheTwoDoorsOfferDifferentCards()
    {
        var transform = At(RunConstants.EventAromaOfChaos);
        Assert.Equal(0, transform.Step(0, -1, out _, out _, out _));

        var upgrade = At(RunConstants.EventAromaOfChaos);
        Assert.Equal(0, upgrade.Step(1, -1, out _, out _, out _));

        for (int i = 0; i < transform.State.Deck.Count; i++)
        {
            Assert.True(RunNonCombatEffects.CanSelectCard(transform.State, i));
            Assert.Equal(
                RunConstants.IsRunCardUpgradable(upgrade.State.Deck[i]),
                RunNonCombatEffects.CanSelectCard(upgrade.State, i)
            );
        }
    }

    // ── Morphic Grove ────────────────────────────────────────────────────────

    [Fact]
    public void GroupingSpendsEveryCoinAndTransformsTwoChosenCards()
    {
        var engine = At(RunConstants.EventMorphicGrove);
        int deck = engine.State.Deck.Count;
        int strike = engine.State.Deck[0].DefId;
        int strikes = engine.State.Deck.Count(card => card.DefId == strike);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(0, engine.State.Gold);
        Assert.Equal(2, engine.State.PendingSelectionCount);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(strikes - 1, engine.State.Deck.Count(card => card.DefId == strike));

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.Equal(strikes - 2, engine.State.Deck.Count(card => card.DefId == strike));
    }

    [Fact]
    public void TheLonerGainsMaxHpAndKeepsTheGold()
    {
        var engine = At(RunConstants.EventMorphicGrove);
        int maxHp = engine.State.PlayerMaxHp;
        int gold = engine.State.Gold;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(maxHp + 5, engine.State.PlayerMaxHp);
        Assert.Equal(gold, engine.State.Gold);
    }

    // ── Doors of Light and Dark ──────────────────────────────────────────────

    [Fact]
    public void TheDarkDoorRemovesTheCardThePlayerPicks()
    {
        var engine = At(RunConstants.EventDoorsOfLightAndDark);
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(deck, engine.State.Deck.Count);

        int index = 4;
        int removed = engine.State.Deck[index].DefId;
        int copies = engine.State.Deck.Count(card => card.DefId == removed);
        Assert.Equal(0, engine.Step(index, -1, out _, out _, out _));

        Assert.Equal(deck - 1, engine.State.Deck.Count);
        Assert.Equal(copies - 1, engine.State.Deck.Count(card => card.DefId == removed));
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    /// <summary>
    /// The Light door is NOT a choice -- it StableShuffles the upgradable cards and takes
    /// two -- so it must not open a selection.
    /// </summary>
    [Fact]
    public void TheLightDoorUpgradesTwoWithoutAsking()
    {
        var engine = At(RunConstants.EventDoorsOfLightAndDark);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(DeckSelection.None, engine.State.PendingSelectionKind);
        Assert.Equal(2, engine.State.Deck.Count(card => card.Upgraded));
    }

    // ── Sunken Treasury ──────────────────────────────────────────────────────

    [Fact]
    public void OnlyTheSecondChestCostsGreed()
    {
        var small = At(RunConstants.EventSunkenTreasury);
        Assert.Equal(0, small.Step(0, -1, out _, out _, out _));
        Assert.Equal(0, CountOf(small.State, "Greed"));

        var large = At(RunConstants.EventSunkenTreasury);
        Assert.Equal(0, large.Step(1, -1, out _, out _, out _));
        Assert.Equal(1, CountOf(large.State, "Greed"));
    }

    // ── The shared rule ──────────────────────────────────────────────────────

    /// <summary>
    /// A selection that cannot open must leave the run untouched. Every one of these
    /// events reads the deck, so an empty deck is the case that separates "refused" from
    /// "opened a screen the player cannot leave".
    /// </summary>
    [Theory]
    [InlineData(RunConstants.EventLuminousChoir, 0)]
    [InlineData(RunConstants.EventWellspring, 1)]
    [InlineData(RunConstants.EventAromaOfChaos, 0)]
    [InlineData(RunConstants.EventAromaOfChaos, 1)]
    [InlineData(RunConstants.EventMorphicGrove, 0)]
    [InlineData(RunConstants.EventDoorsOfLightAndDark, 1)]
    public void AnEmptyDeckRefusesTheOptionRatherThanOpeningAnEmptyScreen(
        int eventId,
        int option
    )
    {
        var engine = At(eventId);
        engine.State.Deck.Clear();
        int gold = engine.State.Gold;

        Assert.Equal(-1, engine.Step(option, -1, out _, out _, out _));

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(DeckSelection.None, engine.State.PendingSelectionKind);
        Assert.Equal(gold, engine.State.Gold);
    }
}
