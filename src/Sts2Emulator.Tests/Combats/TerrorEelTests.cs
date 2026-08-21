using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// TerrorEelElite: one Terror Eel. Read off MegaCrit.Sts2.Core.Models.Monsters/TerrorEel:
/// 150 HP at A8, CrashDamage 16 below A9, THRASH three hits of 3, and the two alternate
/// forever — until ShriekPower fires.
///
/// ShriekPower(75 at A8) watches for an unblocked hit that leaves the eel at or below its
/// threshold. It then stuns the eel for a turn and queues TERROR_MOVE, which lands
/// Vulnerable 99 on the player, and removes itself so this happens once a combat. Nothing
/// a passive capture does can reach it: the sweep had to play cards into a 150 HP elite.
/// </summary>
public class TerrorEelTests
{
    private static Fight Encounter() => Fight.Encounter(CombatFactory.ActOneEncounter.TerrorEel);

    [Fact]
    public void AlternatesCrashAndThrashWhileHealthy()
    {
        var fight = Encounter();
        var announced = new List<(IntentType Type, int Magnitude)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.Single());
            fight.EndTurn();
        }

        // CrashDamage 16, then THRASH's three hits of 3 announced as 9. THRASH also
        // grants itself Vigor 6, which the NEXT attack spends — so the second CRASH
        // announces 22 and the THRASH after it is back to 9.
        Assert.Equal(
            [
                (IntentType.Attack, 16),
                (IntentType.Attack, 9),
                (IntentType.Attack, 22),
                (IntentType.Attack, 9),
            ],
            announced
        );
    }

    [Fact]
    public void ItStartsWithItsShriekThreshold()
    {
        var fight = Encounter();

        Assert.Equal(75, fight.EnemyBuffAmount(BuffId.Shriek));
    }

    [Fact]
    public void AnUnblockedHitBelowTheThresholdStunsItImmediately()
    {
        var fight = Encounter();
        fight.Enemy0.Hp = 80;

        CardEffects.DealDamage(fight.State, 10);

        Assert.Equal(IntentType.Unknown, fight.Enemy0.CurrentIntent.Type);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Stunned));
        // Removed as it fires, so a second wound does not stun it again.
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Shriek));
    }

    [Fact]
    public void AboveTheThresholdItIsUntouched()
    {
        var fight = Encounter();
        fight.Enemy0.Hp = 120;

        CardEffects.DealDamage(fight.State, 10);

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Stunned));
        Assert.Equal(75, fight.EnemyBuffAmount(BuffId.Shriek));
    }

    [Fact]
    public void TheTurnAfterTheStunItTerrorises()
    {
        var fight = Encounter();
        fight.Enemy0.Hp = 80;
        CardEffects.DealDamage(fight.State, 10);

        // The stunned turn passes without the eel acting.
        int hpAfterStun = fight.State.PlayerHp;
        fight.EndTurn();
        Assert.Equal(hpAfterStun, fight.State.PlayerHp);

        Assert.Equal((IntentType.Debuff, 99), fight.Intents.Single());
        fight.EndTurn();
        Assert.Equal(99, fight.PlayerBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void AndThenGoesBackToCrash()
    {
        var fight = Encounter();
        fight.Enemy0.Hp = 80;
        CardEffects.DealDamage(fight.State, 10);

        fight.EndTurn(); // stunned turn
        fight.EndTurn(); // TERROR

        // TERROR_MOVE's FollowUpState is CRASH. Vulnerable makes the readout 1.5x.
        Assert.Equal((IntentType.Attack, 24), fight.Intents.Single());
    }
}
