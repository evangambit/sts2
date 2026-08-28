using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// <c>CardKeyword.Sly</c>: a card discarded BY AN EFFECT is played instead of joining the
/// pile. <c>CardCmd.DiscardAndDraw</c> gathers the sly cards as it moves them and
/// auto-plays each once the rest are down.
/// </summary>
/// <remarks>
/// It was unmodelled entirely, and it is the point of Silent's discard theme — eight cards
/// carry the keyword and Hand Trick grants it. The reason it was invisible is worth
/// keeping: the generated <c>CardDef</c> had no Sly field, so `extract_data.py` never
/// emitted it, and a keyword the extractor does not emit reads exactly like a card that
/// does not have it. `Retain` was in the same state — a field on `CardDef`, read by
/// `IsRetained`, and never once set.
/// </remarks>
public class SlyTests
{
    /// <summary>The eight cards that declare Sly in their own CanonicalKeywords.</summary>
    public static TheoryData<int> SlyCards =>
        new()
        {
            SI.Abrasive,
            SI.FlickFlack,
            SI.Haze,
            SI.Reflex,
            SI.Ricochet,
            SI.Sneaky,
            SI.Tactician,
            SI.Untouchable,
        };

    [Theory]
    [MemberData(nameof(SlyCards))]
    public void TheKeywordIsExtracted(int defId)
    {
        Assert.True(GeneratedData.Cards.Get(defId).Sly, GeneratedData.Cards.Get(defId).Name);
    }

    /// <summary>
    /// Discarding a Tactician plays it: three energy back for a card thrown away, which is
    /// the whole reason to run one. Before, it simply landed in the discard pile.
    /// </summary>
    [Fact]
    public void DiscardingASlyCardPlaysIt()
    {
        var fight = Fight.Hand(Card(SI.Survivor), Card(SI.Tactician)).Energy(1);

        fight.Play(); // Survivor: block, then ask what to discard
        int energyBefore = fight.State.Energy;
        fight.Choose(0); // the Tactician

        // Tactician gives 1 energy when played, and playing is what a sly discard does.
        Assert.Equal(energyBefore + 1, fight.State.Energy);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Tactician);
    }

    /// <summary>A card without the keyword goes to the pile, as it always did.</summary>
    [Fact]
    public void DiscardingAnOrdinaryCardDoesNotPlayIt()
    {
        var fight = Fight.Hand(Card(SI.Survivor), Card(SI.StrikeSilent)).Energy(1);

        fight.Play();
        fight.Choose(0);

        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.StrikeSilent);
    }

    /// <summary>
    /// The END-OF-TURN hand discard is not a <c>CardCmd.Discard</c> and does not trigger
    /// Sly — so holding a Tactician to the end of the turn buys nothing. Getting this
    /// wrong would hand a Silent player free energy every turn for doing nothing.
    /// </summary>
    [Fact]
    public void EndingTheTurnHoldingASlyCardDoesNotPlayIt()
    {
        var fight = Fight.Hand(Card(SI.Tactician)).Energy(3);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Tactician);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.Tactician);
    }

    /// <summary>
    /// Shadow Step discards the whole hand at once, so every sly card in it plays — the
    /// chokepoint means a card that never mentions Sly still honours it.
    /// </summary>
    [Fact]
    public void AWholeHandDiscardPlaysEverySlyCardInIt()
    {
        var fight = Fight
            .Hand(
                Card(SI.ShadowStep),
                Card(SI.Tactician),
                Card(SI.Tactician),
                Card(SI.StrikeSilent)
            )
            .Energy(3);
        int energyBefore = fight.State.Energy;

        fight.Play();

        // Two Tacticians, each worth an energy, minus the one Shadow Step cost.
        Assert.Equal(energyBefore - 1 + 2, fight.State.Energy);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.StrikeSilent);
    }
}

public class HandTrickTests
{
    // BlockVar(7m), OnUpgrade +3, then CardSelectCmd.FromHand filtered to
    // `card.Type == Skill && !card.IsSlyThisTurn`, and ApplySingleTurnSly on the pick.
    // The marking is the second half of the card and did nothing at all: with Sly
    // unmodelled, Hand Trick was seven block.
    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 10)]
    public void BlocksThenOffersOnlyTheSkills(bool upgraded, int block)
    {
        var fight = Fight
            .Hand(Card(SI.HandTrick, upgraded), Card(SI.StrikeSilent), Card(SI.DefendSilent))
            .Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(CardSelectionKind.MarkHandCardSly, fight.Pending!.Kind);
        // The Strike is an Attack, so only the Defend is offered.
        Assert.Equal([1], fight.Pending.Candidates);
    }

    /// <summary>
    /// And the mark is what it is for: the chosen Skill, discarded later in the same turn,
    /// PLAYS. Marked here with Hand Trick and discarded with Shadow Step, which throws the
    /// whole hand away.
    /// </summary>
    [Fact]
    public void TheMarkedSkillPlaysWhenDiscarded()
    {
        var fight = Fight
            .Hand(Card(SI.HandTrick), Card(SI.ShadowStep), Card(SI.DefendSilent))
            .Energy(5);

        fight.Play(); // Hand Trick: 7 block, then mark a Skill
        fight.Choose(fight.Pending!.Candidates.IndexOf(1)); // hand index 1, the Defend
        Assert.True(fight.State.Hand.Single(c => c.DefId == SI.DefendSilent).SlyThisTurn);

        int blockBefore = fight.State.PlayerBlock;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.ShadowStep));

        // The Defend was discarded, and being Sly it played: five more block, and it is
        // not sitting in the hand.
        Assert.Equal(blockBefore + 5, fight.State.PlayerBlock);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.DefendSilent);
    }

    /// <summary>The grant lasts a single TURN, so it does not survive into the next hand.</summary>
    [Fact]
    public void TheMarkExpiresWithTheTurn()
    {
        int purity = GeneratedData.Cards.All.ToArray().Single(d => d.Name == "Purity").Id;
        var fight = Fight.Hand(Card(SI.HandTrick), Card(purity)).Energy(3);
        fight.State.PlayerHp = 999;

        fight.Play();
        fight.Choose(0); // Purity is a Skill, and it RETAINS, so it survives the turn
        while (fight.Pending is not null)
        {
            fight.Choose(0);
        }

        fight.EndTurn();

        var retained = fight.State.Hand.Single(c => c.DefId == purity);
        Assert.False(retained.SlyThisTurn);
    }

    /// <summary>A card already Sly is filtered out of the offer, as the game's filter says.</summary>
    [Fact]
    public void AnAlreadySlySkillIsNotOffered()
    {
        var fight = Fight
            .Hand(Card(SI.HandTrick), Card(SI.Tactician), Card(SI.DefendSilent))
            .Energy(1);

        fight.Play();

        // The Tactician carries the keyword, so only the Defend is on offer.
        Assert.Equal([1], fight.Pending!.Candidates);
    }
}

/// <summary>
/// <c>CardKeyword.Retain</c> was the same defect as Sly and a live one: `CardDef.Retain`
/// is read by <c>IsRetained</c>, which decides what survives the end-of-turn hand discard,
/// and `extract_data.py` never emitted it — so eleven cards that should stay in hand were
/// discarded every turn.
/// </summary>
public class RetainKeywordTests
{
    public static TheoryData<string> RetainCards =>
        new()
        {
            "Eradicate",
            "Luminesce",
            "PoorSleep",
            "Purity",
            "Reap",
            "Restlessness",
            "Sacrifice",
            "Snakebite",
            "SovereignBlade",
            "Sow",
            "Spur",
        };

    [Theory]
    [MemberData(nameof(RetainCards))]
    public void TheKeywordIsExtracted(string name)
    {
        var def = GeneratedData.Cards.All.ToArray().Single(d => d.Name == name);

        Assert.True(def.Retain, name);
    }

    [Fact]
    public void ARetainedCardSurvivesTheEndOfTheTurn()
    {
        int snakebite = GeneratedData.Cards.All.ToArray().Single(d => d.Name == "Snakebite").Id;
        var fight = Fight.Hand(Card(snakebite), Card(SI.StrikeSilent)).Energy(3);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Contains(fight.State.Hand, c => c.DefId == snakebite);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == SI.StrikeSilent);
    }
}
