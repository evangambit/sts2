using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The curses and statuses whose whole existence is a KEYWORD. Nine of them do nothing at
/// all beyond what Unplayable, Eternal, Ethereal, Innate and Retain already mean, so the
/// thing worth pinning is that the extracted data carries the right ones — the card has no
/// code to be wrong, only data.
/// </summary>
public class KeywordOnlyCurseTests
{
    /// <summary>
    /// Each curse and exactly the keywords its source declares. Written out rather than
    /// derived so a patch that quietly drops one fails here.
    /// </summary>
    [Theory]
    [InlineData("Clumsy", "Unplayable,Ethereal")]
    [InlineData("Folly", "Unplayable,Eternal,Ethereal,Innate")]
    [InlineData("Greed", "Unplayable,Eternal")]
    [InlineData("Injury", "Unplayable")]
    [InlineData("PoorSleep", "Unplayable,Retain")]
    [InlineData("Writhe", "Unplayable,Innate")]
    [InlineData("CurseOfTheBell", "Unplayable,Eternal")]
    [InlineData("Wound", "Unplayable")]
    [InlineData("Soot", "Unplayable")]
    public void ItCarriesExactlyItsKeywords(string name, string expected)
    {
        var def = GeneratedData.Cards.Get(GeneratedData.Cards.FindId(name)!.Value);
        var actual = new[]
        {
            def.Unplayable ? "Unplayable" : null,
            def.Eternal ? "Eternal" : null,
            def.Ethereal ? "Ethereal" : null,
            def.Innate ? "Innate" : null,
            def.Retain ? "Retain" : null,
        }
            .Where(k => k is not null)
            .ToList();

        Assert.Equal(expected.Split(','), actual);
    }

    /// <summary>
    /// Eternal is what makes a curse a punishment rather than a chore — the removal
    /// screens will not so much as offer it.
    /// </summary>
    [Theory]
    [InlineData("Folly")]
    [InlineData("Greed")]
    [InlineData("CurseOfTheBell")]
    public void AnEternalCurseCannotBeRemoved(string name)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int id = GeneratedData.Cards.FindId(name)!.Value;
        engine.State.Deck.Add(new CardInstance(id, false));

        RunNonCombatEffects.BeginDeckSelection(engine.State, DeckSelection.Remove, 0, count: 1);

        int index = engine.State.Deck.FindIndex(card => card.DefId == id);
        Assert.False(RunNonCombatEffects.CanSelectCard(engine.State, index));
    }

    /// <summary>
    /// `PoorSleep` RETAINS: unlike every other curse it does not leave the hand at end of
    /// turn, so it clogs the same five cards all fight.
    /// </summary>
    [Fact]
    public void PoorSleepStaysInHand()
    {
        int poorSleep = GeneratedData.Cards.FindId("PoorSleep")!.Value;
        var fight = Fight.Hand(new CardInstance(poorSleep, false)).Enemy();

        fight.EndTurn();

        Assert.Contains(fight.State.Hand, card => card.DefId == poorSleep);
    }

    /// <summary>
    /// `Clumsy` is ETHEREAL, so the opposite happens: it exhausts itself at end of turn
    /// and is gone from the combat entirely.
    /// </summary>
    [Fact]
    public void ClumsyExhaustsItself()
    {
        int clumsy = GeneratedData.Cards.FindId("Clumsy")!.Value;
        var fight = Fight.Hand(new CardInstance(clumsy, false)).Enemy();

        fight.EndTurn();

        Assert.DoesNotContain(fight.State.Hand, card => card.DefId == clumsy);
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == clumsy);
    }
}

/// <summary>
/// `Void.AfterCardDrawn`: drawing it costs one energy. Unplayable and Ethereal, so the
/// energy it takes on the way past is the whole card.
/// </summary>
public class VoidTests
{
    [Fact]
    public void DrawingItCostsAnEnergy()
    {
        var fight = Fight.Hand().Enemy();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(ST.Void, false));
        int before = fight.State.Energy;

        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(before - 1, fight.State.Energy);
    }

    /// <summary>Only the Void itself — drawing an ordinary card beside it costs nothing.</summary>
    [Fact]
    public void AnOrdinaryCardIsFree()
    {
        var fight = Fight.Hand().Enemy();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        int before = fight.State.Energy;

        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(before, fight.State.Energy);
    }

    /// <summary>Energy does not go negative — `LoseEnergy` floors at zero.</summary>
    [Fact]
    public void ItCannotTakeEnergyYouDoNotHave()
    {
        var fight = Fight.Hand().Enemy();
        fight.State.Energy = 0;
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(ST.Void, false));

        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(0, fight.State.Energy);
    }
}

/// <summary>
/// `Normality.ShouldPlay` is false once THREE cards have been played this turn, and only
/// while the curse is in HAND — so drawing it late can stop a turn dead, and shuffling it
/// away costs nothing.
/// </summary>
public class NormalityTests
{
    private static Fight WithNormality(bool inHand)
    {
        int normality = GeneratedData.Cards.FindId("Normality")!.Value;
        var fight = Fight
            .Hand(
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.StrikeIronclad, false)
            )
            .Energy(9)
            .Enemy(hp: 300);
        if (inHand)
        {
            fight.State.Hand.Add(new CardInstance(normality, false));
        }
        else
        {
            fight.State.DrawPile.Add(new CardInstance(normality, false));
        }

        return fight;
    }

    [Fact]
    public void TheFourthCardIsRefused()
    {
        var fight = WithNormality(inHand: true);

        for (int i = 0; i < 3; i++)
        {
            fight.Play(0);
        }

        Assert.Equal(3, fight.State.CardPlaysThisTurn);
        Assert.DoesNotContain(0, CombatEngine.ValidActions(fight.State));
    }

    /// <summary>Three is allowed — the limit is a ceiling, not a cut at two.</summary>
    [Fact]
    public void ThreeCardsAreFine()
    {
        var fight = WithNormality(inHand: true);

        for (int i = 0; i < 3; i++)
        {
            Assert.Contains(0, CombatEngine.ValidActions(fight.State));
            fight.Play(0);
        }
    }

    /// <summary>In the draw pile it does nothing: the rule reads the HAND.</summary>
    [Fact]
    public void ItOnlyBitesFromTheHand()
    {
        var fight = WithNormality(inHand: false);

        for (int i = 0; i < 3; i++)
        {
            fight.Play(0);
        }

        Assert.Contains(0, CombatEngine.ValidActions(fight.State));
    }
}

/// <summary>
/// `Guilty.AfterCombatEnd`: it counts the combats it sits through in the DECK and removes
/// itself at five. The only card in the game that leaves on its own.
/// </summary>
public class GuiltyTests
{
    private static RunEngine WithGuilties(int count)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        for (int i = 0; i < count; i++)
        {
            engine.State.Deck.Add(
                new CardInstance(RunNonCombatEffects.NamedCard("Guilty"), false)
            );
        }

        return engine;
    }

    private static int GuiltiesIn(RunEngine engine) =>
        engine.State.Deck.Count(card =>
            card.DefId == RunNonCombatEffects.NamedCard("Guilty")
        );

    [Fact]
    public void ItLeavesAfterFiveCombats()
    {
        var engine = WithGuilties(1);

        for (int combat = 1; combat <= 4; combat++)
        {
            RunNonCombatEffects.ServeGuiltySentences(engine.State);
            Assert.Equal(1, GuiltiesIn(engine));
        }

        RunNonCombatEffects.ServeGuiltySentences(engine.State);

        Assert.Equal(0, GuiltiesIn(engine));
    }

    /// <summary>
    /// The count is per COPY — `[SavedProperty]` on the card — so two Guilties taken at
    /// different times serve separate sentences rather than one shared clock.
    /// </summary>
    [Fact]
    public void TwoCopiesServeSeparateSentences()
    {
        var engine = WithGuilties(1);

        for (int combat = 0; combat < 3; combat++)
        {
            RunNonCombatEffects.ServeGuiltySentences(engine.State);
        }

        // A second Guilty arrives three combats in, and starts from zero.
        engine.State.Deck.Add(
            new CardInstance(RunNonCombatEffects.NamedCard("Guilty"), false)
        );

        for (int combat = 0; combat < 2; combat++)
        {
            RunNonCombatEffects.ServeGuiltySentences(engine.State);
        }

        // The first has served five and gone; the second has served two.
        Assert.Equal(1, GuiltiesIn(engine));
    }

    /// <summary>A deck with no Guilty in it counts nothing.</summary>
    [Fact]
    public void ADeckWithoutOneIsUnaffected()
    {
        var engine = WithGuilties(0);
        int before = engine.State.Deck.Count;

        for (int combat = 0; combat < 10; combat++)
        {
            RunNonCombatEffects.ServeGuiltySentences(engine.State);
        }

        Assert.Equal(before, engine.State.Deck.Count);
    }
}

/// <summary>
/// `Wound` is Unplayable and nothing else — the plainest status in the game, and the one
/// most enemies deal. Its own class because the coverage gate keys on the name.
/// </summary>
public class WoundTests
{
    [Fact]
    public void ItIsDeadWeightAndNothingMore()
    {
        var def = GeneratedData.Cards.Get(ST.Wound);

        Assert.True(def.Unplayable);
        Assert.False(def.Ethereal);
        Assert.False(def.Retain);
        Assert.False(def.TurnEndInHand);
        Assert.Equal(CardType.Status, def.Type);
    }

    /// <summary>
    /// It goes to the discard at end of turn like any other card — no Ethereal to clear
    /// it, no Retain to keep it, so it comes back round with the pile.
    /// </summary>
    [Fact]
    public void ItCyclesWithTheDeck()
    {
        var fight = Fight.Hand(new CardInstance(ST.Wound, false)).Enemy();

        fight.EndTurn();

        Assert.Contains(fight.State.DiscardPile, card => card.DefId == ST.Wound);
    }
}
