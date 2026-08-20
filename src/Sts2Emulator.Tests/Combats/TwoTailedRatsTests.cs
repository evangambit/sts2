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

    /// <summary>
    /// From a live A8 capture of seed QS2GYXRKWN: three rats at 20/18/19, one calls for
    /// backup on turn 3 and the summoned rat arrives at 21 HP AT THE FRONT of the roster,
    /// then a second summon arrives at 22 in front of that. CallForBackup takes the last
    /// free slot and the rats start in Slots[2..4], so both summons land ahead of them.
    /// </summary>
    [Fact]
    public void BackupArrivesAtTheFrontOfTheRoster()
    {
        var fight = Encounter();
        var starters = fight.State.Enemies.ToList();

        for (int turn = 0; turn < 4 && fight.State.Enemies.Count == starters.Count; turn++)
        {
            fight.EndTurn();
        }

        Assert.True(
            fight.State.Enemies.Count > starters.Count,
            "no rat called for backup in four turns"
        );
        Assert.Equal(
            starters,
            fight.State.Enemies.Skip(fight.State.Enemies.Count - starters.Count)
        );
    }

    /// <summary>
    /// A summoned rat takes an HP the roster is not already using, off the Niche stream —
    /// CombatState.CreateCreature calls SetUniqueMonsterHpValue for every enemy it makes,
    /// summons included, excluding the MaxHp of the creatures already on that side.
    /// </summary>
    [Fact]
    public void BackupTakesAnHpNoLivingRatHolds()
    {
        var fight = Encounter();

        for (int turn = 0; turn < 4 && fight.State.Enemies.Count == 3; turn++)
        {
            fight.EndTurn();
        }

        Assert.True(fight.State.Enemies.Count > 3, "no rat called for backup in four turns");
        var hps = fight.State.Enemies.Where(e => e.Hp > 0).Select(e => e.MaxHp).ToList();
        Assert.Equal(hps.Count, hps.Distinct().Count());
        Assert.All(hps, hp => Assert.InRange(hp, 18, 22));
    }

    /// <summary>
    /// Five slots, three rats: the pack can summon at most twice, and once the slots are
    /// full CanSummon() finds none free, so the last rat attacks instead of calling.
    /// </summary>
    [Fact]
    public void NeverGrowsPastItsFiveSlots()
    {
        var fight = Encounter();

        fight.Turns(12);

        Assert.True(fight.State.Enemies.Count <= 5, $"{fight.State.Enemies.Count} rats");
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
