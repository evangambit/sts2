using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// TwoTailedRatsNormal: three Two-Tailed Rats. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/TwoTailedRat: HP 18-22 at A8, ScratchDamage 9
/// (8 below A9), DiseaseBiteDamage 7 (6). A rat can also call for backup, which is why the
/// intent is not a pure cycle.
/// </summary>
public class TwoTailedRatsTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.TwoTailedRats, ascension);

    [Fact]
    public void RosterIsThreeRats()
    {
        var fight = Encounter();

        Assert.Equal([KE.TwoTailedRat, KE.TwoTailedRat, KE.TwoTailedRat], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBand()
    {
        var fight = Encounter();

        Assert.All(fight.State.Enemies, enemy => Assert.InRange(enemy.MaxHp, 18, 22));
    }

    [Fact]
    public void ScratchAndBiteUseTheAscensionEightDamage()
    {
        var fight = Encounter();
        var attacks = new List<int>();

        for (int turn = 0; turn < 6; turn++)
        {
            attacks.AddRange(
                fight
                    .Intents.Where(intent => intent.Type == IntentType.Attack)
                    .Select(intent => intent.Magnitude)
            );
            fight.EndTurn();
        }

        Assert.NotEmpty(attacks);
        // 8 and 6, never their A9 values of 9 and 7.
        Assert.All(attacks, damage => Assert.Contains(damage, (int[])[8, 6]));
    }

    [Fact]
    public void ScratchAndBiteAreHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        var attacks = new List<int>();

        for (int turn = 0; turn < 6; turn++)
        {
            attacks.AddRange(
                fight
                    .Intents.Where(intent => intent.Type == IntentType.Attack)
                    .Select(intent => intent.Magnitude)
            );
            fight.EndTurn();
        }

        Assert.NotEmpty(attacks);
        Assert.All(attacks, damage => Assert.Contains(damage, (int[])[9, 7]));
    }
}
