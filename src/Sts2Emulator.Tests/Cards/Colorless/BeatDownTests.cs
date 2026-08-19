using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Skill, TargetType.RandomEnemy. MegaCrit.Sts2.Core.Models.Cards/BeatDown.cs
// auto-plays up to CardsVar(3) Attacks taken from the discard pile, each targeting a
// random enemy off Rng.CombatTargets; OnUpgrade raises that to 4.
//
// The emulator moves that many discarded cards into hand instead of playing them, and
// does not filter to Attacks. These tests pin the approximation, which is a long way
// from the card: nothing is dealt, and the cards end up playable rather than spent.
public class BeatDownTests
{
    [Fact]
    public void MovesThreeDiscardedCardsToHandWithoutPlayingThem()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown))
            .Energy(3)
            .Discard(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.Bash)
            )
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.State.Hand.Count);
        // Three Strikes would have dealt 18 had they been played.
        Assert.Equal(60, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedMovesFour()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown, upgraded: true))
            .Energy(3)
            .Discard(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.Bash)
            )
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(4, fight.State.Hand.Count);
    }

    [Fact]
    public void TakesWhatItCanFromAShortDiscardPile()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown))
            .Energy(3)
            .Discard(Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }
}
