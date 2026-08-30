using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Skill, TargetType.RandomEnemy. MegaCrit.Sts2.Core.Models.Cards/BeatDown.cs
// auto-plays up to CardsVar(3) Attacks taken from the discard pile; OnUpgrade raises that
// to 4.
//
// This used to move that many discarded cards into HAND without playing them, and without
// filtering to Attacks -- so three energy bought three cards and no damage. These tests
// asserted that.
public class BeatDownTests
{
    [Fact]
    public void PlaysThreeAttacksOutOfTheDiscardPile()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown))
            .Energy(3)
            .Discard(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad)
            )
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(42, fight.Enemy0.Hp);
        Assert.DoesNotContain(IC.StrikeIronclad, Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedPlaysFour()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown, upgraded: true))
            .Energy(3)
            .Discard(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad)
            )
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(36, fight.Enemy0.Hp);
    }

    /// <summary>Only Attacks: a discard pile of Defends buys nothing.</summary>
    [Fact]
    public void TakesOnlyAttacks()
    {
        var fight = Fight
            .Hand(Card(CL.BeatDown))
            .Energy(3)
            .Discard(Card(IC.DefendIronclad), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(54, fight.Enemy0.Hp);
        Assert.Equal(2, Fight.Ids(fight.State.DiscardPile).Count(id => id == IC.DefendIronclad));
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

        Assert.Equal(54, fight.Enemy0.Hp);
    }

    /// <summary>
    /// `discard.Where(Attack && !Unplayable).StableShuffle(Rng.Shuffle).Take(n)` — which
    /// Attacks come back is a shuffle, not the first ones by index. Fourth card with this
    /// pair of faults after Anointed, Catastrophe and Seeker Strike.
    /// </summary>
    [Fact]
    public void WhichAttacksReturnVariesWithTheShuffleStream()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.Hand(Card(CL.BeatDown)).Energy(3).Enemy(hp: 400);
            fight.State.ShuffleRng = new CountingRandom(seed);
            fight.State.DiscardPile =
            [
                Card(IC.StrikeIronclad),
                Card(IC.Bludgeon),
                Card(IC.Cinder),
                Card(IC.Anger),
                Card(IC.TwinStrike),
            ];

            fight.Play();

            seen.Add(string.Join(",", Fight.Ids(fight.State.DiscardPile)));
        }

        Assert.True(seen.Count > 1, $"the pick never varied: {string.Join(" | ", seen)}");
    }

    [Fact]
    public void ItRollsOnTheShuffleStream()
    {
        var fight = Fight.Hand(Card(CL.BeatDown)).Energy(3).Enemy(hp: 400);
        var stream = new CountingRandom(9);
        fight.State.ShuffleRng = stream;
        fight.State.DiscardPile =
        [
            Card(IC.StrikeIronclad),
            Card(IC.Bludgeon),
            Card(IC.Cinder),
            Card(IC.Anger),
        ];

        fight.Play();

        Assert.True(stream.CallCount > 0, "the shuffle stream should have been drawn from");
    }
}
