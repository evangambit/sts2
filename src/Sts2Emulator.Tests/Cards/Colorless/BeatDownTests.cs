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
}
