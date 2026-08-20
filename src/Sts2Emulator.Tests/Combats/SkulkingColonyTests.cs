using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// SkulkingColonyElite: one Skulking Colony. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/SkulkingColony — ZOOM, ZOOM_2, INERTIA (an attack
/// plus a BuffIntent) and PIERCING_STABS (PiercingStabsDamage x 2), in a fixed ring.
///
/// This replaces a CombatEngineTests case that pinned Inertia at its A9 value and said so
/// in a comment: "like every elite the combat sweep does not reach yet". The sweep reaches
/// the act-1 elites now, and the live game announces 9.
/// </summary>
public class SkulkingColonyTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.SkulkingColony, ascension);

    [Fact]
    public void RosterIsOneColony()
    {
        var fight = Encounter();

        Assert.Equal([KE.SkulkingColony], fight.EnemyDefIds);
    }

    /// <summary>
    /// Zoom, Zoom, Inertia, Piercing Stabs. Inertia grants Strength, so everything after
    /// it announces higher — which is why the later numbers are not their printed damage.
    /// </summary>
    [Fact]
    public void RunsItsRingAtTheAscensionEightDamage()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        int strength = BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength);
        Assert.Equal(
            [(IntentType.Attack, 14), (IntentType.Attack, 14), (IntentType.Attack, 9)],
            announced.Take(3)
        );
        // Piercing Stabs is two hits, each carrying the Strength Inertia just granted.
        Assert.Equal((IntentType.Attack, (7 + strength) * 2), announced[3]);
    }

    [Fact]
    public void EveryMoveIsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [(IntentType.Attack, 16), (IntentType.Attack, 16), (IntentType.Attack, 11)],
            announced
        );
    }

    /// <summary>INERTIA_MOVE's BuffIntent is InertiaStrengthGain — 2 at A8, 4 from A9.</summary>
    [Fact]
    public void InertiaGrantsItsAscensionEightStrength()
    {
        var fight = Encounter();
        fight.EndTurn();
        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength));
    }
}
