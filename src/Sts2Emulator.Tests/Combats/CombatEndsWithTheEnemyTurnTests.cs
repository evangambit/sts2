using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// <c>CombatManager.ExecuteEnemyTurn</c> awaits <c>CheckWinCondition</c> after every enemy
/// and returns the moment <c>IsInProgress</c> goes false, so a fight the enemy phase
/// settles never begins another player turn.
///
/// <para>
/// The emulator used to run the whole of that turn -- drawing a hand, reshuffling the
/// discard pile to find one -- and only check afterwards. That is not a cosmetic
/// difference: <c>Rng.Shuffle</c> is a RUN-level stream, so a reshuffle the game never
/// makes leaves it ahead by the size of a pile and every hand dealt for the rest of the
/// run comes off the wrong position. It cost a live run its opening hand three fights
/// later (`WK1DEGZD8P`, docs/divergence-catalog.md O2).
/// </para>
/// </summary>
public class CombatEndsWithTheEnemyTurnTests
{
    private static Fight AboutToWin() =>
        Fight
            .Hand()
            .PlayerHp(200, 200)
            // Two turns' worth of cards left, so a turn that should not happen has to
            // reshuffle to deal itself a hand -- which is what makes the extra turn
            // visible on the shuffle stream rather than only in the phase.
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Discard(
                Card(IC.DefendIronclad),
                Card(IC.DefendIronclad),
                Card(IC.DefendIronclad),
                Card(IC.DefendIronclad),
                Card(IC.DefendIronclad),
                Card(IC.DefendIronclad)
            )
            .Enemy(hp: 1);

    [Fact]
    public void AFightTheEnemyPhaseEndsDoesNotDealAnotherHand()
    {
        var fight = AboutToWin();
        // Poison finishes the only enemy at the start of ITS turn, so the fight is over
        // partway through the enemy phase rather than on the player's own turn.
        BuffSystem.Apply(fight.State.Enemies[0].Buffs, BuffId.Poison, 5);

        var result = fight.EndTurn();

        Assert.True(result.Terminal);
        Assert.True(result.PlayerWon);
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void AFightTheEnemyPhaseEndsDoesNotDrawFromTheShuffleStream()
    {
        var fight = AboutToWin();
        var shuffle = new CountingRandom(0);
        fight.State.ShuffleRng = shuffle;
        BuffSystem.Apply(fight.State.Enemies[0].Buffs, BuffId.Poison, 5);

        fight.EndTurn();

        Assert.Equal(0, shuffle.CallCount);
    }

    /// <summary>
    /// The other half of the same rule: a player the enemy phase kills does not take a
    /// turn either, and a fight that carried on regardless kept ticking its turn counter
    /// past the death -- which is how a seventh-turn relic fired in a fight that ended on
    /// the fourth.
    /// </summary>
    [Fact]
    public void AFightThatKillsThePlayerEndsWithTheEnemyPhase()
    {
        var fight = Fight.Hand().PlayerHp(5, 80).Enemy(hp: 100).Enemy(hp: 100);
        foreach (var enemy in fight.State.Enemies)
        {
            enemy.CurrentIntent = new Intent(IntentType.Attack, 20);
        }

        var result = fight.EndTurn();

        Assert.True(result.Terminal);
        Assert.False(result.PlayerWon);
        Assert.Equal(0, fight.State.PlayerHp);
        Assert.Empty(fight.State.Hand);
    }
}
