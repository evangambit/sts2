using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The last two shared-pool relics, both of which needed machinery rather than a body.

/// <summary>
/// Gambling Chip: on turn one, discard ANY number of cards and draw that many.
/// </summary>
/// <remarks>
/// The first selection with no upper bound. `CardSelectorPrefs(prompt, 0, 999999999)` is
/// min zero and max effectively-unlimited, where every repeated screen before it spent a
/// fixed `Amount`. The draw is deferred to whichever answer CLOSES the screen, because the
/// count is not known until then — `CardCmd.DiscardAndDraw(list, list.Count)` takes the
/// whole list at once rather than one card at a time.
/// </remarks>
public class GamblingChipTests
{
    [Fact]
    public void TheScreenIsUpAtTheStartOfTheFirstTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.GamblingChip);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.DiscardAnyThenDraw, fight.Pending!.Kind);
        Assert.Equal(fight.State.Hand.Count, fight.Pending.Candidates.Count);
    }

    /// <summary>Declining keeps the whole hand and draws nothing.</summary>
    [Fact]
    public void ItCanBeDeclinedOutright()
    {
        var fight = Fight.WithRelics(RelicEffects.GamblingChip);
        int hand = fight.State.Hand.Count;

        fight.Choose(fight.Pending!.Candidates.Count); // the skip

        Assert.Null(fight.Pending);
        Assert.Equal(hand, fight.State.Hand.Count);
    }

    /// <summary>
    /// It reopens after every pick — there is no fixed count to spend — and the draw
    /// arrives only once the screen is closed.
    /// </summary>
    [Fact]
    public void ItReopensUntilDeclinedAndThenDraws()
    {
        var fight = Fight.WithRelics(RelicEffects.GamblingChip);
        int hand = fight.State.Hand.Count;

        fight.Choose(0);
        Assert.NotNull(fight.Pending);
        Assert.Equal(hand - 1, fight.State.Hand.Count);

        fight.Choose(0);
        Assert.NotNull(fight.Pending);
        // Still nothing drawn: the draw waits for the screen to close.
        Assert.Equal(hand - 2, fight.State.Hand.Count);

        fight.Choose(fight.Pending!.Candidates.Count);

        Assert.Null(fight.Pending);
        Assert.Equal(hand, fight.State.Hand.Count);
    }

    /// <summary>Emptying the hand closes the screen on its own and draws the lot.</summary>
    [Fact]
    public void PitchingEverythingClosesItAndRedraws()
    {
        var fight = Fight.WithRelics(RelicEffects.GamblingChip);
        int hand = fight.State.Hand.Count;

        while (fight.Pending is not null && fight.State.Hand.Count > 0)
        {
            fight.Choose(0);
        }

        Assert.Null(fight.Pending);
        Assert.Equal(hand, fight.State.Hand.Count);
    }

    /// <summary>And it is a turn-one relic: no screen on the second turn.</summary>
    [Fact]
    public void ItDoesNotComeBackOnLaterTurns()
    {
        var fight = Fight.WithRelics(RelicEffects.GamblingChip);
        fight.State.PlayerHp = 999;
        fight.Choose(fight.Pending!.Candidates.Count);

        fight.EndTurn();

        Assert.Null(fight.Pending);
    }
}

/// <summary>
/// Unsettling Lamp: the FIRST card each combat to land a debuff on an enemy has its
/// debuffs doubled.
/// </summary>
/// <remarks>
/// The game latches in `BeforePowerAmountChanged` and unlatches in `AfterCardPlayed` for
/// that same card — so a card applying two debuffs gets both doubled, and the next card
/// gets neither.
/// </remarks>
public class UnsettlingLampTests
{
    // Encounter 1's two enemies both hold Artifact, which swallows a debuff whole -- so a
    // relic test about debuff MAGNITUDE has to pick a roster that can actually receive
    // one. Encounter 3 is three enemies, none protected.
    private static Fight Lamped() =>
        Fight.Encounter(
            (CombatFactory.ActOneEncounter)3,
            relicIds: [RelicEffects.UnsettlingLamp]
        );

    [Fact]
    public void TheFirstDebuffingCardIsDoubled()
    {
        var fight = Lamped();
        fight.State.Hand = [Card(SI.Neutralize), Card(SI.Neutralize)];
        fight.State.Energy = 9;

        fight.Play();
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak)); // Weak 1, doubled

        int after = fight.EnemyBuffAmount(BuffId.Weak);
        fight.Play();
        Assert.Equal(after + 1, fight.EnemyBuffAmount(BuffId.Weak)); // and the next is not
    }

    /// <summary>
    /// Both of one card's debuffs are doubled, not just the first — the latch is per CARD
    /// rather than per application.
    /// </summary>
    [Fact]
    public void EveryDebuffOnThatOneCardIsDoubled()
    {
        var fight = Lamped();
        fight.State.Hand = [Card(SI.Malaise)];
        fight.State.Energy = 3;

        fight.Play();

        // Malaise applies Strength down AND Weak at X; both should be doubled.
        Assert.True(fight.EnemyBuffAmount(BuffId.Weak) > 0);
        Assert.Equal(fight.EnemyBuffAmount(BuffId.Weak), -fight.EnemyBuffAmount(BuffId.Strength));
    }

    /// <summary>A card that debuffs nobody does not spend the doubling.</summary>
    [Fact]
    public void ACardWithNoDebuffDoesNotSpendIt()
    {
        var fight = Lamped();
        fight.State.Hand = [Card(SI.Slice), Card(SI.Neutralize)];
        fight.State.Energy = 9;

        fight.Play(); // an attack with no debuff
        fight.Play();

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak));
    }
}
