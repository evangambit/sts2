using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// When Weak, Frail and Vulnerable count down. All three tick in
/// AfterSideTurnEnd(side == CombatSide.Enemy) — once a round, after the enemies have
/// acted — and anything applied to a player-side creature skips one tick, because
/// PowerCmd.Apply sets SkipNextDurationTick for player-side debuffs.
///
/// Ground truth is a live A8 capture of slime-and-flyconid (seed QS2GYXRKWN): the
/// flyconid's Vulnerable Spores land on turn 2, the player still reads Vulnerable 2 on
/// turn 3, and on turn 4 — holding the last point of it — takes 12 + 12 from two
/// 8-damage attacks rather than 8 + 8.
/// </summary>
public class DebuffDurationTests
{
    [Fact]
    public void TheLastPointOfVulnerableStillAmplifiesThatTurnsAttack()
    {
        var fight = Fight.Hand().Enemy();
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 8);
        fight.PlayerBuff(BuffId.Vulnerable, 1);
        // Applied before the round rather than during it, so it is not owed a skip.
        fight.State.PlayerDebuffsAtRoundStart = BuffSystem.DurationDebuffSnapshot(
            fight.State.PlayerBuffs
        );
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - 12, fight.State.PlayerHp);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void ADebuffAppliedDuringTheEnemyTurnIsStillWholeNextTurn()
    {
        var fight = Fight.Hand().Enemy();
        // The flyconid's Vulnerable Spores: a debuff intent, applied as the enemy acts.
        fight.Enemy0.DefId = KE.Flyconid;
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Debuff, 2);

        fight.EndTurn();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void AndThenTicksOnceARoundAfterThat()
    {
        var fight = Fight.Hand().Enemy();
        fight.Enemy0.DefId = KE.Flyconid;
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Debuff, 2);

        fight.EndTurn();
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Defend, 0);
        fight.EndTurn();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Vulnerable));
    }

    /// <summary>
    /// Enemies get no grace period: a debuff the player lands on one ticks at the end of
    /// the very next enemy turn — but only after that enemy has swung with it.
    /// </summary>
    [Fact]
    public void AnEnemyStaysWeakForTheAttackItIsAboutToMake()
    {
        var fight = Fight.Hand().Enemy();
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 10);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Weak, 1);
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        // Weak is a 25% cut, so 10 lands as 7 and only then does the stack run out.
        Assert.Equal(before - 7, fight.State.PlayerHp);
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak));
    }
}
