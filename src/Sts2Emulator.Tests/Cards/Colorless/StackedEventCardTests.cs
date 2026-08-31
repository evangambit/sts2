using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Four cards that shared one "deal the printed damage" body and should not have. The
/// label-stack hazard again: a `case` added to an existing stack inherits whatever that
/// stack does, and three of these four do something else entirely.
/// </summary>
public class MaulTests
{
    /// <summary>
    /// FIVE damage TWICE — `WithHitCount(2)` — where the shared body hit once. Half the
    /// card's damage, silently.
    /// </summary>
    [Fact]
    public void ItHitsTwice()
    {
        int maul = GeneratedData.Cards.FindId("Maul")!.Value;
        var fight = Fight.Hand(new CardInstance(maul, false)).Energy(9).Enemy(hp: 300);

        fight.Play(0);

        Assert.Equal(300 - 10, fight.State.Enemies[0].Hp);
    }

    /// <summary>
    /// Every play raises the damage of EVERY Maul the player owns by `Increase` — not
    /// just the copy played. Rampage's growth is the other shape, on its own copy only,
    /// and the difference shows only in a deck holding two.
    /// </summary>
    [Fact]
    public void EveryCopyGrows()
    {
        int maul = GeneratedData.Cards.FindId("Maul")!.Value;
        var fight = Fight
            .Hand(new CardInstance(maul, false), new CardInstance(maul, false))
            .Energy(9)
            .Enemy(hp: 300);

        fight.Play(0);
        int afterFirst = fight.State.Enemies[0].Hp;

        // The second copy was in HAND while the first was played, so it grew too: 6 twice.
        fight.Play(0);

        Assert.Equal(300 - 10, afterFirst);
        Assert.Equal(afterFirst - 12, fight.State.Enemies[0].Hp);
    }

    /// <summary>The growth rides the copy into the discard and comes back with it.</summary>
    [Fact]
    public void TheGrowthSurvivesThePile()
    {
        int maul = GeneratedData.Cards.FindId("Maul")!.Value;
        var fight = Fight.Hand(new CardInstance(maul, false)).Energy(9).Enemy(hp: 300);

        fight.Play(0);

        Assert.Contains(
            fight.State.DiscardPile,
            card => card.DefId == maul && card.BonusDamage > 0
        );
    }
}

/// <summary>
/// `Clash.IsPlayable`: every card in HAND must be an Attack. A single Skill makes it
/// unplayable, which is the entire deckbuilding constraint the card exists for — and it
/// was stacked into a plain damage body, so any hand could play it.
/// </summary>
public class ClashTests
{
    private static Fight WithHand(params int[] extra)
    {
        int clash = GeneratedData.Cards.FindId("Clash")!.Value;
        var cards = new[] { new CardInstance(clash, false) }
            .Concat(extra.Select(id => new CardInstance(id, false)))
            .ToArray();
        return Fight.Hand(cards).Energy(9).Enemy(hp: 300);
    }

    [Fact]
    public void AnAllAttackHandCanPlayIt()
    {
        var fight = WithHand(IC.StrikeIronclad, IC.StrikeIronclad);

        Assert.Contains(0, CombatEngine.ValidActions(fight.State));
    }

    [Fact]
    public void OneSkillInHandStopsIt()
    {
        var fight = WithHand(IC.StrikeIronclad, IC.DefendIronclad);

        Assert.DoesNotContain(0, CombatEngine.ValidActions(fight.State));
    }

    /// <summary>It counts ITSELF, and it is an Attack — a lone Clash is playable.</summary>
    [Fact]
    public void ALoneClashIsPlayable()
    {
        var fight = WithHand();

        Assert.Contains(0, CombatEngine.ValidActions(fight.State));
    }
}

/// <summary>
/// `Rebound` deals 9 AND applies `ReboundPower`, which sends the next card bound for the
/// discard to the TOP OF THE DRAW PILE instead. The power was missing entirely.
/// </summary>
public class ReboundTests
{
    private static int ReboundId => GeneratedData.Cards.FindId("Rebound")!.Value;

    /// <summary>
    /// The power has no guard against its own source, so Rebound recycles ITSELF: it goes
    /// to the top of the draw pile rather than the discard, and spends the power doing it.
    /// That is the game's behaviour, not a modelling shortcut.
    /// </summary>
    [Fact]
    public void ItPutsItselfBackOnTopOfTheDrawPile()
    {
        var fight = Fight.Hand(new CardInstance(ReboundId, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);

        Assert.Equal(ReboundId, fight.State.DrawPile[0].DefId);
        Assert.DoesNotContain(fight.State.DiscardPile, card => card.DefId == ReboundId);
    }

    [Fact]
    public void ItStillDealsItsDamage()
    {
        var fight = Fight.Hand(new CardInstance(ReboundId, false)).Energy(9).Enemy(hp: 300);

        fight.Play(0);

        Assert.Equal(300 - 9, fight.State.Enemies[0].Hp);
    }

    /// <summary>
    /// `PowerStackType.Counter`, decremented per card — so one Rebound moves one card, and
    /// the card after it discards normally.
    /// </summary>
    [Fact]
    public void ItMovesOneCardOnly()
    {
        var fight = Fight
            .Hand(new CardInstance(ReboundId, false), new CardInstance(IC.StrikeIronclad, false))
            .Energy(9)
            .Enemy(hp: 300);
        fight.State.DrawPile.Clear();

        fight.Play(0);
        fight.Play(0);

        Assert.Contains(fight.State.DiscardPile, card => card.DefId == IC.StrikeIronclad);
    }

    /// <summary>`AfterSideTurnEnd` removes it, so an unspent Rebound does not carry over.</summary>
    [Fact]
    public void AnUnspentReboundDoesNotSurviveTheTurn()
    {
        var fight = Fight.Hand().Enemy();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Rebound, 1);

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Rebound));
    }
}

/// <summary>The three Minion tokens, which Vitruvian Minion doubles.</summary>
public class MinionStrikeTests
{
    [Fact]
    public void SixDamageAndACard()
    {
        int id = GeneratedData.Cards.FindId("MinionStrike")!.Value;
        var fight = Fight.Hand(new CardInstance(id, false)).Energy(9).Enemy(hp: 300);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));

        fight.Play(0);

        Assert.Equal(300 - 6, fight.State.Enemies[0].Hp);
        Assert.Contains(fight.State.Hand, card => card.DefId == IC.StrikeIronclad);
    }
}
