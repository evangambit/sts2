using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/
// HowlFromBeyond.cs: DamageVar(16m) to all opponents; OnUpgrade raises it by 5.
//
// The card declares NO `CanonicalKeywords` — only an `ExtraHoverTips` naming Exhaust, the
// way Havoc does — so `CardModel.GetResultPileTypeForCardPlay` sends it to the DISCARD
// pile. It never exhausts itself. What it has instead is
// `AfterAutoPostPlayPhaseEntered`: if the card is sitting in its owner's EXHAUST pile as
// the play phase ENDS, `CardCmd.AutoPlay(choiceContext, this, null)` swings it once more
// for free. That call takes no `forceExhaust` — unlike the `AutoPlayFromDrawPile` Havoc
// uses — so the replayed copy lands in the discard pile too, and the replay is once per
// trip to the exhaust pile rather than every turn.
public class HowlFromBeyondTests
{
    [Fact]
    public void DealsSixteenToEveryEnemy()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Enemy(hp: 40).Enemy(hp: 30);

        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
        Assert.Equal(14, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedDealsTwentyOne()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond, upgraded: true))
            .Energy(3)
            .Enemy(hp: 40)
            .Enemy(hp: 30);

        fight.Play();

        Assert.Equal(19, fight.Enemy0.Hp);
        Assert.Equal(9, fight.Enemy1.Hp);
    }

    /// <summary>
    /// No Exhaust keyword, so playing it spends it into the discard like any other attack.
    /// The hover tip is about the pile the card READS, not a pile it puts itself in.
    /// </summary>
    [Fact]
    public void PlayingItDiscardsItRatherThanExhaustingIt()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Enemy(hp: 90);

        fight.Play();

        Assert.Equal(IC.HowlFromBeyond, Assert.Single(fight.State.DiscardPile).DefId);
        Assert.Empty(fight.State.ExhaustPile);
    }

    /// <summary>
    /// And with nothing in the exhaust pile there is nothing for the hook to find, so the
    /// turn after a plain play is an ordinary turn.
    /// </summary>
    [Fact]
    public void PlayingItDoesNotSetUpAReplay()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Enemy(hp: 90);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(74, fight.Enemy0.Hp);
    }

    [Fact]
    public void ReplaysItselfOutOfTheExhaustPile()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Exhausted(Card(IC.HowlFromBeyond))
            .Enemy(hp: 90);

        fight.EndTurn();

        Assert.Equal(74, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The replay MOVES the card — out of the exhaust pile, and into the discard, because
    /// `AutoPlay` was given no `forceExhaust` and the card has no Exhaust keyword of its
    /// own. Bombardment's identical hook returns it to the exhaust pile instead.
    /// </summary>
    [Fact]
    public void TheReplaySpendsTheCardIntoTheDiscardPile()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Exhausted(Card(IC.HowlFromBeyond))
            .Enemy(hp: 90);

        fight.EndTurn();

        Assert.Empty(fight.State.ExhaustPile);
        Assert.Contains(fight.State.DiscardPile, card => card.DefId == IC.HowlFromBeyond);
    }

    /// <summary>So it fires once per trip to the exhaust pile, not once a turn.</summary>
    [Fact]
    public void TheReplayDoesNotRepeatNextTurn()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Exhausted(Card(IC.HowlFromBeyond))
            .Enemy(hp: 90);

        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal(74, fight.Enemy0.Hp);
    }

    /// <summary>
    /// `AfterAutoPostPlayPhaseEntered` is the END of the play phase, which
    /// `CombatManager.EndPlayerTurnPhaseOneInternal` runs before `BeforeTurnEnd`, before
    /// the hand flush and before the enemies take their turn. Against enemies that were
    /// about to swing, that is the difference between killing them and being hit first.
    /// </summary>
    [Fact]
    public void TheReplayLandsBeforeTheEnemiesAct()
    {
        var fight = Fight.Encounter(3); // SlimesWeak: three enemies, none protected.
        fight.State.ExhaustPile.Add(Card(IC.HowlFromBeyond));
        foreach (var enemy in fight.State.Enemies)
        {
            enemy.Hp = 16;
        }

        // Not a vacuous test only if they really were going to hit back.
        Assert.All(fight.Intents, intent => Assert.True(intent.Magnitude > 0));
        int hpBefore = fight.State.PlayerHp;

        var result = fight.EndTurn();

        Assert.True(result.PlayerWon);
        Assert.Equal(hpBefore, fight.State.PlayerHp);
    }

    /// <summary>
    /// Nothing puts Howl in the exhaust pile on its own, so the hook needs a card that
    /// exhausts what it plays. Havoc is `AutoPlayFromDrawPile(..., forceExhaust: true)`:
    /// the Howl on top of the draw pile swings, is exhausted, and then swings again as the
    /// play phase ends.
    /// </summary>
    [Fact]
    public void HavocExhaustsItAndTheHookThenFindsIt()
    {
        var fight = Fight
            .Hand(Card(IC.Havoc))
            .Draw(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Enemy(hp: 90);

        fight.Play();
        Assert.Equal(74, fight.Enemy0.Hp);
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == IC.HowlFromBeyond);

        fight.EndTurn();

        Assert.Equal(58, fight.Enemy0.Hp);
    }

    [Fact]
    public void DoesNotReplayWhileItIsStillInHand()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Exhausted().Enemy(hp: 90);

        fight.EndTurn();

        Assert.Equal(90, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachEnemysOwnVulnerableRaisesItsShare()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)])
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.Enemy0.Hp);
        Assert.Equal(24, fight.Enemy1.Hp);
    }
}
