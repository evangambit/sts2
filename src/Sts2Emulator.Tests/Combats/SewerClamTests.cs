using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// SewerClamNormal: one Sewer Clam. Read off MegaCrit.Sts2.Core.Models.Monsters/SewerClam:
/// HP is a flat 58 at A8 (56 below), and the machine starts on JET (10 damage, 11 at A9)
/// and alternates with PRESSURIZE, which applies PlatingPower.
/// </summary>
public class SewerClamTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.SewerClam, ascension);

    [Fact]
    public void RosterIsOneClam()
    {
        var fight = Encounter();

        Assert.Equal([KE.SewerClam], fight.EnemyDefIds);
    }

    /// <summary>MaxInitialHp is MinInitialHp, so the roll has exactly one outcome.</summary>
    [Fact]
    public void HpIsFixedAtFiftyEight()
    {
        var fight = Encounter();

        Assert.Equal(58, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(56, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void OpensOnJetRatherThanPressurize()
    {
        var fight = Encounter();

        Assert.Equal([(IntentType.Attack, 10)], fight.Intents);
    }

    [Fact]
    public void JetHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);

        Assert.Equal([(IntentType.Attack, 11)], fight.Intents);
    }

    [Fact]
    public void AlternatesJetAndPressurize()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [
                (IntentType.Attack, 10),
                (IntentType.Buff, 0),
                (IntentType.Attack, 10),
                (IntentType.Buff, 0),
            ],
            announced
        );
    }
}
