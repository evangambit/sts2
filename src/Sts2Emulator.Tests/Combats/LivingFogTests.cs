using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// LivingFogNormal: one Living Fog. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/LivingFog: HP a flat 82 at A8 (80 below),
/// AdvancedGasDamage 9 (8 below A9), BloatDamage 6 (5), SuperGasBlastDamage 9 (8). All
/// three were transcribed at their A9 value.
/// </summary>
public class LivingFogTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.LivingFog, ascension);

    [Fact]
    public void RosterIsOneFog()
    {
        var fight = Encounter();

        Assert.Equal([KE.LivingFog], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtEightyTwo()
    {
        var fight = Encounter();

        Assert.Equal(82, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(80, fight.State.Enemies[0].MaxHp);
    }

    /// <summary>
    /// ADVANCED_GAS happens once — its FollowUpState is BLOAT, and BLOAT and
    /// SUPER_GAS_BLAST then point at each other forever. The emulator cycled all three on
    /// MoveIndex % 3, re-gassing every third turn, which a live sweep caught.
    /// </summary>
    [Fact]
    public void GassesOnceThenAlternatesBloatAndBlast()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 5; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [
                // ADVANCED_GAS_MOVE declares SingleAttackIntent first and
                // CardDebuffIntent second, and a live capture reads it as an Attack --
                // it is an attack that also applies Smoggy, not a debuff carrying damage.
                (IntentType.Attack, 8),
                (IntentType.Attack, 5),
                (IntentType.Attack, 8),
                (IntentType.Attack, 5),
                (IntentType.Attack, 8),
            ],
            announced
        );
    }

    [Fact]
    public void EveryMoveIsLargerAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [(IntentType.Attack, 9), (IntentType.Attack, 6), (IntentType.Attack, 9)],
            announced
        );
    }

    /// <summary>BLOAT carries a SummonIntent: each one adds a Gas Bomb.</summary>
    [Fact]
    public void BloatSummonsAGasBomb()
    {
        var fight = Encounter();
        fight.EndTurn();
        fight.EndTurn();

        Assert.Contains(KE.GasBomb, fight.EnemyDefIds);
    }
}
