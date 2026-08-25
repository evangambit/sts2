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
    /// SelfFormingClayPower gains its block in AfterBlockCleared, so it shows up on the
    /// next player turn rather than immediately.
    /// </summary>
    /// <remarks>
    /// SIX, not three, and the reason is worth keeping. The relic's
    /// <c>AfterDamageReceived</c> fires per INSTANCE of unblocked damage and the power is
    /// a Counter, so a two-hit attack arms it twice. These tests fight the Chompers,
    /// whose CLAMP is <c>MultiAttackIntent(ClampDamage, 2)</c> — and while that was
    /// folded into a single 18, every per-instance hook in the game under-triggered
    /// against it. The fold was not only a wrong number.
    /// </remarks>
    [Fact]
    public void SelfFormingClayArmsOncePerHitAndPaysOnTheFollowingTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);

        Assert.Equal(0, fight.State.PlayerBlock);
        fight.EndTurn();

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    /// <summary>DynamicVar("BlockNextTurn", 3m) is unpowered — Dexterity must not raise it.</summary>
    [Fact]
    public void SelfFormingClaysBlockIgnoresDexterity()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);

        fight.EndTurn();

        // Two arming instances at 3, and not a point of the five Dexterity.
        Assert.Equal(6, fight.State.PlayerBlock);
    }

    [Fact]
    public void SelfFormingClayFiresEveryTurnItIsHit()
    {
        var fight = Fight.WithRelics(RelicEffects.SelfFormingClay);
        fight.EndTurn();
        fight.EndTurn();

        // The two Chompers alternate, so one of them clamps every turn and the relic
        // re-arms every turn -- the power is removed as it pays out.
        Assert.Equal(6, fight.State.PlayerBlock);
    }
}
