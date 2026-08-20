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

        // Blast 7 twice — not 8. Each blast also buffs the construct, so by the time
        // Expel comes round its 5x2 is announced with that Strength on every hit.
        Assert.Equal(
            [(IntentType.Buff, 0), (IntentType.Buff, 7), (IntentType.Buff, 7)],
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

        Assert.Equal((IntentType.Buff, 8), fight.Intents.First());
        fight.EndTurn();
        fight.EndTurn();

        int strength = BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength);
        Assert.Equal((IntentType.Attack, (6 + strength) * 2), fight.Intents.First());
    }
}
