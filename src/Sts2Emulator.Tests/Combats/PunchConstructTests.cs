using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// PunchConstructNormal: one Punch Construct. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/PunchConstruct: HP a flat 60 at A8 (55 below),
/// FastPunchDamage 6 (5 below A9) as two hits, StrongPunchDamage 16 (14).
/// </summary>
public class PunchConstructTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.PunchConstruct, ascension);

    [Fact]
    public void RosterIsOneConstruct()
    {
        var fight = Encounter();

        Assert.Equal([KE.PunchConstruct], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtSixty()
    {
        var fight = Encounter();

        Assert.Equal(60, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(55, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void PunchesUseTheAscensionEightDamage()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        // Fast Punch 5x2 and Strong Punch 14 — not 12 and 16.
        Assert.Equal(
            [(IntentType.Defend, 10), (IntentType.Attack, 10), (IntentType.Attack, 14)],
            announced
        );
    }

    [Fact]
    public void PunchesAreHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 12), fight.Intents.First());
        fight.EndTurn();
        Assert.Equal((IntentType.Attack, 16), fight.Intents.First());
    }
}
