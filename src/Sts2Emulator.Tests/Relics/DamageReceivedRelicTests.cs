using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The two relics that answer unblocked damage, read off
/// MegaCrit.Sts2.Core.Models.Relics: Centennial Puzzle CardsVar(3) once per combat, and
/// Self-Forming Clay DynamicVar("BlockNextTurn", 3m) every time.
/// </summary>
public class DamageReceivedRelicTests
{
    [Fact]
    public void CentennialPuzzleDrawsThreeTheFirstTimeDamageGetsThrough()
    {
        var plain = Fight.WithRelics();
        var withPuzzle = Fight.WithRelics(RelicEffects.CentennialPuzzle);

        int hpBefore = withPuzzle.State.PlayerHp;
        withPuzzle.EndTurn();
        plain.EndTurn();

        Assert.True(withPuzzle.State.PlayerHp < hpBefore, "the enemy turn should land a hit");
        Assert.Equal(plain.State.Hand.Count + 3, withPuzzle.State.Hand.Count);
    }

    /// <summary>
    /// The relic is spent for the rest of the combat, so the second enemy turn draws
    /// nothing extra. The hand is discarded and redealt between turns, which is why this
    /// compares against a plain fight rather than against the eight cards of turn one.
    /// </summary>
    [Fact]
    public void CentennialPuzzleOnlyDrawsOncePerCombat()
    {
        var plain = Fight.WithRelics();
        var withPuzzle = Fight.WithRelics(RelicEffects.CentennialPuzzle);

        withPuzzle.EndTurn();
        plain.EndTurn();
        Assert.Equal(plain.State.Hand.Count + 3, withPuzzle.State.Hand.Count);

        withPuzzle.EndTurn();
        plain.EndTurn();

        Assert.Equal(plain.State.Hand.Count, withPuzzle.State.Hand.Count);
    }

    /// <summary>
    /// Blocked damage is not damage as far as the hook is concerned: the relic waits for a
    /// hit that actually costs HP.
    /// </summary>
    [Fact]
    public void CentennialPuzzleIgnoresFullyBlockedHits()
    {
        var plain = Fight.WithRelics();
        var withPuzzle = Fight.WithRelics(RelicEffects.CentennialPuzzle);
        foreach (var fight in new[] { plain, withPuzzle })
        {
            fight.State.PlayerBlock = 200;
        }

        int hpBefore = withPuzzle.State.PlayerHp;
        withPuzzle.EndTurn();
        plain.EndTurn();

        Assert.Equal(hpBefore, withPuzzle.State.PlayerHp);
        Assert.Equal(plain.State.Hand.Count, withPuzzle.State.Hand.Count);
    }

    /// <summary>
    /// Hook.AfterDamageReceived does not ask who dealt the damage, so a card that hits its
    /// own owner arms the relic exactly as an enemy attack does.
    /// </summary>
    [Fact]
    public void CentennialPuzzleAnswersDamageDealtByACard()
    {
        var plain = Fight.WithRelics();
        var withPuzzle = Fight.WithRelics(RelicEffects.CentennialPuzzle);

        foreach (var fight in new[] { plain, withPuzzle })
        {
            CardEffects.DealDamageToPlayer(fight.State, 5);
        }

        Assert.Equal(plain.State.Hand.Count + 3, withPuzzle.State.Hand.Count);
    }

    /// <summary>
    /// SelfFormingClayPower gains its block in AfterBlockCleared, so the three block shows
    /// up on the next player turn rather than immediately.
    /// </summary>
    [Fact]
    public void SelfFormingClayGivesThreeBlockOnTheFollowingTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);

        Assert.Equal(0, fight.State.PlayerBlock);
        fight.EndTurn();

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    /// <summary>DynamicVar("BlockNextTurn", 3m) is unpowered — Dexterity must not raise it.</summary>
    [Fact]
    public void SelfFormingClaysBlockIgnoresDexterity()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);

        fight.EndTurn();

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    [Fact]
    public void SelfFormingClayFiresEveryTurnItIsHit()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);
        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal(3, fight.State.PlayerBlock);
    }
}
