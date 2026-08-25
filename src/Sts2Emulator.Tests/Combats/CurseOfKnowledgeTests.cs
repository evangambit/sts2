using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Knowledge Demon's CURSE_OF_KNOWLEDGE, which is a player CHOICE.
/// </summary>
/// <remarks>
/// The emulator used to apply a flat Disintegration 6 and offer nothing — choosing for
/// the player, and taking the wrong curse two casts out of three. It is also why the
/// demon could not be captured live: the game raises a "Choose a card" screen and the
/// harness had nothing to answer it with.
/// </remarks>
public class KnowledgeDemonCurseTests
{
    private static Fight Demon(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.KnowledgeDemon, ascension);

    /// <summary>Walk to the demon's Nth curse and stop on the open screen.</summary>
    private static Fight AtCurse(int cast)
    {
        var fight = Demon();
        var demon = fight.State.Enemies[0];
        while (true)
        {
            demon.Hp = 9999;
            fight.State.PlayerHp = 9999;
            if (fight.State.PendingSelection is { Kind: CardSelectionKind.CurseOfKnowledge })
            {
                if (cast == 0)
                {
                    return fight;
                }

                cast--;
                // Answered with the ALTERNATIVE, so the Disintegration under test is the
                // only one that ever lands -- these powers are Counters and stack.
                fight.Choose(1);
            }

            fight.EndTurn();
        }
    }

    [Fact]
    public void TheFirstCurseOpensAScreenRatherThanApplyingOne()
    {
        var fight = Demon();
        Assert.Null(fight.State.PendingSelection);

        fight.EndTurn();

        var selection = fight.State.PendingSelection;
        Assert.NotNull(selection);
        Assert.Equal(CardSelectionKind.CurseOfKnowledge, selection!.Kind);
        // Nothing lands until the player answers.
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Disintegration));
    }

    /// <summary>
    /// `_curseOfKnowledgeSets`: Disintegration against MindRot, then Sloth, then
    /// WasteAway — a different pair each cast.
    /// </summary>
    [Theory]
    [InlineData(0, ST.MindRot)]
    [InlineData(1, ST.Sloth)]
    [InlineData(2, ST.WasteAway)]
    public void EachCastOffersItsOwnPair(int cast, int alternative)
    {
        var fight = AtCurse(cast);

        Assert.Equal(
            [ST.Disintegration, alternative],
            fight.State.PendingSelection!.GeneratedCandidates
        );
    }

    /// <summary>
    /// `_disintegrationDamageValues` is `{ 6, 7, 8 }`, written over the card's own var on
    /// each cast — so the third Disintegration hurts half again as much as the first.
    /// </summary>
    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 7)]
    [InlineData(2, 8)]
    public void DisintegrationEscalatesAcrossTheThreeCasts(int cast, int expected)
    {
        var fight = AtCurse(cast);

        fight.Choose(0);

        Assert.Equal(expected, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Disintegration));
    }

    [Fact]
    public void TakingTheAlternativeAppliesItInsteadOfDisintegration()
    {
        var fight = AtCurse(0);

        fight.Choose(1);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Disintegration));
        Assert.Equal(
            RunConstants.MindRotAmount,
            BuffSystem.Get(fight.State.PlayerBuffs, BuffId.MindRot)
        );
    }

    /// <summary>Every power here is a Counter, so a second cast stacks onto the first.</summary>
    [Fact]
    public void TakingDisintegrationTwiceStacksIt()
    {
        var fight = Demon();
        var demon = fight.State.Enemies[0];
        int taken = 0;
        while (taken < 2)
        {
            demon.Hp = 9999;
            fight.State.PlayerHp = 9999;
            if (fight.State.PendingSelection is { Kind: CardSelectionKind.CurseOfKnowledge })
            {
                fight.Choose(0);
                taken++;
                continue;
            }

            fight.EndTurn();
        }

        // First cast is 6, second is 7, and the power is a Counter.
        Assert.Equal(6 + 7, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Disintegration));
    }

    /// <summary>While the screen is up, it owns the action space.</summary>
    [Fact]
    public void TheScreenOffersExactlyItsTwoCandidates()
    {
        var fight = Demon();
        fight.EndTurn();

        Assert.Equal([0, 1], fight.Pending!.Candidates);
        // A third answer is not a choice the screen has.
        Assert.Equal(StepResult.Invalid, fight.Choose(2));
    }
}

/// <summary>
/// What the three alternatives to Disintegration actually do.
/// </summary>
public class CursePowerTests
{
    private static Fight Fresh() => Fight.Encounter(CombatFactory.ActOneEncounter.Chompers);

    /// <summary>MindRotPower.ModifyHandDraw: `Math.Max(0, count - Amount)`.</summary>
    [Fact]
    public void MindRotDrawsFewerCards()
    {
        var plain = Fresh();
        plain.EndTurn();
        int normal = plain.State.Hand.Count;

        var rotted = Fresh();
        BuffSystem.Apply(rotted.State.PlayerBuffs, BuffId.MindRot, 2);
        rotted.EndTurn();

        Assert.Equal(normal - 2, rotted.State.Hand.Count);
    }

    /// <summary>WasteAwayPower.ModifyMaxEnergy subtracts from every turn's energy.</summary>
    [Fact]
    public void WasteAwayCostsEnergyEveryTurn()
    {
        var fight = Fresh();
        int normal = fight.State.Energy;

        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.WasteAway, 1);
        fight.EndTurn();

        Assert.Equal(normal - 1, fight.State.Energy);
    }

    /// <summary>
    /// SlothPower.ShouldPlay: the turn stops accepting plays at the cap. Not
    /// unplayability — the cards are fine, the turn is spent.
    /// </summary>
    [Fact]
    public void SlothCapsHowManyCardsATurnAccepts()
    {
        var fight = Fresh();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Sloth, 2);
        fight.State.Energy = 99;
        int hand = fight.State.Hand.Count;

        // Asserted on the HAND, not on the StepResult: `CombatEngine.Invalid` is
        // `new(false, false, 0f)`, which an ordinary successful play returns too, so a
        // rejection is only visible in what did not happen.
        fight.Play(0);
        fight.Play(0);
        Assert.Equal(hand - 2, fight.State.Hand.Count);

        // The third is refused however much energy is left.
        fight.Play(0);
        Assert.Equal(hand - 2, fight.State.Hand.Count);

        fight.EndTurn();
        fight.State.Energy = 99;
        int afterDraw = fight.State.Hand.Count;

        // The cap resets with the turn.
        fight.Play(0);
        Assert.Equal(afterDraw - 1, fight.State.Hand.Count);
    }
}
