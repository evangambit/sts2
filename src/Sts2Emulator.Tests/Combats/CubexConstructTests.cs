using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// CubexConstructNormal: one Cubex Construct. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/CubexConstruct: HP a flat 70 at A8 (65 below), and a
/// four-state machine — CHARGE_UP, then REPEATER_BLAST twice (BlastDamage 8, 7 below A9,
/// each an attack plus a buff), then EXPEL (ExpelDamage 6, 5 below A9, twice over) which
/// loops back to the first blast rather than to the charge.
/// </summary>
public class CubexConstructTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.CubexConstruct, ascension);

    [Fact]
    public void RosterIsOneConstruct()
    {
        var fight = Encounter();

        Assert.Equal([KE.CubexConstruct], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtSeventy()
    {
        var fight = Encounter();

        Assert.Equal(70, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(65, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void BlastAndExpelUseTheAscensionEightDamage()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        // Blast 7 — not 8 — announced as an Attack and already carrying the Strength
        // that CHARGE_UP and the previous blast handed over: 7+2 then 7+4. A live sweep
        // reads exactly 9 and 11 here. Expel's 5x2 then carries it on every hit.
        Assert.Equal(
            [(IntentType.Buff, 0), (IntentType.Attack, 9), (IntentType.Attack, 11)],
            announced.Take(3)
        );
        int strength = BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength);
        Assert.Equal((IntentType.Attack, (5 + strength) * 2), announced[3]);
    }

    [Fact]
    public void BothAreHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 10), fight.Intents.First());
        fight.EndTurn();
        fight.EndTurn();

        int strength = BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength);
        Assert.Equal((IntentType.Attack, (6 + strength) * 2), fight.Intents.First());
    }
}
