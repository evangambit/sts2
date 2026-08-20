using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// FogmogNormal: one Fogmog, which summons an Eye With Teeth. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/Fogmog: HP a flat 78 at A8 (74 below), SwipeDamage 9
/// (8 below A9), HeadbuttDamage 16 (14). Both damage values were transcribed at their A9
/// figure, so the Fogmog hit high at A8.
/// </summary>
public class FogmogTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Fogmog, ascension);

    [Fact]
    public void RosterIsOneFogmog()
    {
        var fight = Encounter();

        Assert.Equal([KE.Fogmog], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtSeventyEight()
    {
        var fight = Encounter();

        Assert.Equal(78, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(74, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void SwipeAndHeadbuttUseTheAscensionEightDamage()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(FogmogIntent(fight));
            fight.EndTurn();
        }

        // Swipe 8, not 9. The Headbutt that follows is announced with the Strength the
        // Fogmog picked up in between, so it reads 14 plus that rather than a bare 16.
        Assert.Equal([(IntentType.Buff, 0), (IntentType.Buff, 8)], announced.Take(2));
        int strength = BuffSystem.Get(
            fight.State.Enemies[fight.EnemyDefIds.ToList().IndexOf(KE.Fogmog)].Buffs,
            BuffId.Strength
        );
        Assert.Equal((IntentType.Attack, 14 + strength), announced[2]);
    }

    [Fact]
    public void BothAreHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal((IntentType.Buff, 9), FogmogIntent(fight));
        fight.EndTurn();

        int strength = BuffSystem.Get(
            fight.State.Enemies[fight.EnemyDefIds.ToList().IndexOf(KE.Fogmog)].Buffs,
            BuffId.Strength
        );
        Assert.Equal((IntentType.Attack, 16 + strength), FogmogIntent(fight));
    }

    /// <summary>ILLUSION_MOVE is a SummonIntent: the fight gains an Eye With Teeth.</summary>
    [Fact]
    public void IllusionSummonsAnEyeWithTeeth()
    {
        var fight = Encounter();
        fight.EndTurn();

        Assert.Contains(KE.EyeWithTeeth, fight.EnemyDefIds);
    }

    /// <summary>
    /// The summoned Eye is inserted ahead of the Fogmog, so "the first enemy" stops being
    /// the one this test is about the moment Illusion resolves.
    /// </summary>
    private static (IntentType Type, int Magnitude) FogmogIntent(Fight fight)
    {
        int index = fight.EnemyDefIds.ToList().IndexOf(KE.Fogmog);
        return fight.Intents.ElementAt(index);
    }
}
