using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// InkletsNormal: three Inklets, the middle one opening on WHIRLWIND rather than JAB. Read
/// off MegaCrit.Sts2.Core.Models.Monsters/Inklet: HP 12-18 at A8, JabDamage 4 (3 below A9),
/// PiercingGazeDamage 11 (10), WhirlwindDamage 3 (2) as a MultiAttack of three — every one
/// of those was transcribed at its A9 value and so hit high at A8.
/// </summary>
public class InkletsTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Inklets, ascension);

    [Fact]
    public void RosterIsThreeInklets()
    {
        var fight = Encounter();

        Assert.Equal([KE.Inklet, KE.Inklet, KE.Inklet], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBand()
    {
        var fight = Encounter();

        Assert.All(fight.State.Enemies, enemy => Assert.InRange(enemy.MaxHp, 12, 18));
    }

    [Fact]
    public void JabAndGazeUseTheAscensionEightDamage()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        // Jab 3, Piercing Gaze 10, Whirlwind 2x3 — not 4, 11 and 9.
        Assert.Equal(
            [(IntentType.Attack, 3), (IntentType.Attack, 10), (IntentType.Attack, 6)],
            announced
        );
    }

    [Fact]
    public void EveryMoveHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [(IntentType.Attack, 4), (IntentType.Attack, 11), (IntentType.Attack, 9)],
            announced
        );
    }
}
