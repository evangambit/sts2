using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Underdocks elite, and the Skittish power that made a live run diverge.
///
/// <para>
/// A gardener flinches behind block the first time a card lands unblocked damage on it
/// each turn. Nothing modelled that, so a replayed Underdocks run had the gardener at 0
/// block where the game had 7 -- and every later exchange in that fight drifted.
/// </para>
/// </summary>
public class PhantasmalGardenersTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.PhantasmalGardeners, ascension);

    /// <summary>A gardener carrying A8 Skittish, and a Strike to poke it with.</summary>
    private static Fight Gardener(params CardInstance[] hand) =>
        Fight
            .Hand(hand.Length > 0 ? hand : [Card(IC.StrikeIronclad)])
            .Energy(3)
            .Enemy(hp: 40, buffs: new BuffState(BuffId.Skittish, 7));

    [Fact]
    public void RosterIsFourGardeners()
    {
        var fight = Encounter();

        Assert.Equal(
            [
                KE.PhantasmalGardener,
                KE.PhantasmalGardener,
                KE.PhantasmalGardener,
                KE.PhantasmalGardener,
            ],
            fight.EnemyDefIds
        );
    }

    /// <summary>
    /// <c>SkittishAmount</c> is <c>GetValueIfAscension(ToughEnemies, 7, 6)</c>, so the
    /// A8 run a capture is taken on carries 7 and a lower one carries 6.
    /// </summary>
    [Theory]
    [InlineData(8, 7)]
    [InlineData(7, 6)]
    public void EveryGardenerCarriesSkittishFromTheStart(int ascension, int amount)
    {
        var fight = Encounter(ascension);

        Assert.All(
            fight.State.Enemies,
            enemy => Assert.Equal(amount, BuffSystem.Get(enemy.Buffs, BuffId.Skittish))
        );
    }

    [Fact]
    public void TheFirstCardToGetThroughMakesItFlinch()
    {
        var fight = Gardener();

        Assert.Equal(0, fight.Enemy0.Block);
        fight.Play(0, target: 0);

        Assert.Equal(7, fight.Enemy0.Block);
    }

    /// <summary>Once per turn: the second card that lands adds no more block.</summary>
    [Fact]
    public void ItOnlyFlinchesOncePerTurn()
    {
        var fight = Gardener(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad)).Energy(9);

        fight.Play(0, target: 0);
        int afterFirst = fight.Enemy0.Block;
        fight.Play(0, target: 0);

        Assert.Equal(7, afterFirst);
        // The second hit spends block rather than adding more: 7 - 6 damage = 1.
        Assert.Equal(1, fight.Enemy0.Block);
    }

    /// <summary>
    /// The flag clears when the player's turn ends, so the next turn's first hit
    /// flinches again. Modelling the block but not the reset would make a gardener
    /// flinch once per FIGHT.
    /// </summary>
    [Fact]
    public void ItFlinchesAgainNextTurn()
    {
        var fight = Gardener();
        fight.Play(0, target: 0);
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SkittishSpent));

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SkittishSpent));
    }

    /// <summary>
    /// The game wants <c>UnblockedDamage != 0</c>: a hit the gardener soaks entirely on
    /// its own block does not set the power off, and does not spend the turn's flinch.
    /// </summary>
    [Fact]
    public void AHitItFullyBlocksDoesNotSetItOff()
    {
        var fight = Gardener();
        fight.Enemy0.Block = 99;

        fight.Play(0, target: 0);

        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SkittishSpent));
        Assert.Equal(99 - 6, fight.Enemy0.Block);
    }
}
