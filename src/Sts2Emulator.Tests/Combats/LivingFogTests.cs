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

    [Fact]
    public void EveryMoveUsesTheAscensionEightValue()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [(IntentType.Debuff, 8), (IntentType.Buff, 5), (IntentType.Attack, 8)],
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
            [(IntentType.Debuff, 9), (IntentType.Buff, 6), (IntentType.Attack, 9)],
            announced
        );
    }
}
