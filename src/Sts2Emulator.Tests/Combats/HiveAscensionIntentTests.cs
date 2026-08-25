using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Hive monsters that announced their A9 damage at A8.
/// </summary>
/// <remarks>
/// The class E11 named and `CombatFactory` was swept for years ago: monster values come
/// through <c>AscensionHelper.GetValueIfAscension(level, high, low)</c> and the enum's
/// ordinal IS the level, so at A8 the Tough branch is live and the DEADLY branch is not.
/// <c>EnemyAI.SelectIntent</c> was never swept, and an audit of every monster's Deadly
/// pairs against it flags eighty more sites. These are the Hive ones, which is what an
/// act-2 run actually meets.
///
/// Two of them were wrong in a second way as well, and both are shapes this suite has
/// seen before: Exoskeleton's SKITTER and Hunter-Killer's PUNCTURE are
/// <c>MultiAttackIntent</c>s folded into one number (E10), which matches only while the
/// creature has no Strength — the game adds Strength to EACH hit — and the Ovicopter's
/// TENDERIZER declares its attack BEFORE its debuff, so the readout calls it an attack
/// (E12). Retyping that one moved its damage into the attack branch, so its Vulnerable
/// had to move with it or it would have been dropped in silence.
/// </remarks>
public class HiveAscensionIntentTests
{
    private static Fight At(CombatFactory.ActOneEncounter encounter, int ascension) =>
        Fight.Encounter(encounter, ascension);

    private static (IntentType Type, int Magnitude, int Hits) IntentOf(Fight fight, int defId)
    {
        int index = fight.EnemyDefIds.ToList().IndexOf(defId);
        var enemy = fight.State.Enemies[index];
        return (enemy.CurrentIntent.Type, enemy.CurrentIntent.Magnitude, enemy.CurrentIntent.Hits);
    }

    /// <summary>
    /// SkitterDamage is a flat 1 and SkitterRepeats is what ascension moves (4 at A9,
    /// 3 at A8) — so a flat 4 was the A9 HIT COUNT read as a damage number.
    /// MandiblesDamage is 9 at A9 and 8 at A8.
    /// </summary>
    [Theory]
    [InlineData(8, 3, 8)]
    [InlineData(9, 4, 9)]
    public void ExoskeletonSkittersAndBitesAtTheRightLevel(
        int ascension,
        int expectedRepeats,
        int expectedMandibles
    )
    {
        // The move is a NextInt(3) over three states, so sweep seeds until each is seen
        // rather than assuming which one the opener rolls.
        var seen = new HashSet<(IntentType, int, int)>();
        for (int seed = 0; seed < 40; seed++)
        {
            var fight = Fight.Encounter(
                CombatFactory.ActOneEncounter.Exoskeletons,
                ascension,
                seed
            );
            seen.Add(IntentOf(fight, KE.Exoskeleton));
        }

        Assert.Contains((IntentType.Attack, 1, expectedRepeats), seen);
        Assert.Contains((IntentType.Attack, expectedMandibles, 1), seen);
        // Never the folded number, at either level.
        Assert.DoesNotContain((IntentType.Attack, 4, 1), seen);
    }

    [Theory]
    [InlineData(8, 16)]
    [InlineData(9, 17)]
    public void OvicopterSmashesAtTheRightLevel(int ascension, int expected)
    {
        var fight = At(CombatFactory.ActOneEncounter.Ovicopter, ascension);
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, expected, 1), IntentOf(fight, KE.Ovicopter));
    }

    /// <summary>
    /// TENDERIZER_MOVE is <c>SingleAttackIntent(TenderizerDamage)</c> then
    /// <c>DebuffIntent()</c>, so it reads as an ATTACK of 8 (7 below A9) — not a Debuff
    /// of 8, which is what an agent was being told.
    /// </summary>
    [Theory]
    [InlineData(8, 7)]
    [InlineData(9, 8)]
    public void OvicopterTenderizerIsAnAttackNotADebuff(int ascension, int expected)
    {
        var fight = At(CombatFactory.ActOneEncounter.Ovicopter, ascension);
        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, expected, 1), IntentOf(fight, KE.Ovicopter));
    }

    [Fact]
    public void OvicopterTenderizerStillAppliesItsVulnerable()
    {
        var fight = At(CombatFactory.ActOneEncounter.Ovicopter, Ascension.DefaultLevel);
        fight.EndTurn();
        fight.EndTurn();
        Assert.Equal(IntentType.Attack, IntentOf(fight, KE.Ovicopter).Type);

        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vulnerable));
    }

    /// <summary>
    /// PUNCTURE_MOVE is <c>MultiAttackIntent(PunctureDamage, 3)</c>. The 24 it used to
    /// announce is 8 x 3 folded, which the game never says and which stops matching the
    /// moment the creature has Strength.
    /// </summary>
    [Theory]
    [InlineData(8, 17, 7)]
    [InlineData(9, 19, 8)]
    public void HunterKillerBitesAndPuncturesAtTheRightLevel(
        int ascension,
        int expectedBite,
        int expectedPuncture
    )
    {
        // The choice is a NextInt(3) taken fresh each turn, so walk turns rather than
        // seeds: a new fight re-rolls from the same position and only ever shows one.
        var seen = new HashSet<(IntentType, int, int)>();
        var fight = At(CombatFactory.ActOneEncounter.HunterKiller, ascension);
        for (int turn = 0; turn < 30; turn++)
        {
            fight.EndTurn();
            if (fight.State.Enemies[0].Hp <= 0)
            {
                break;
            }

            seen.Add(IntentOf(fight, KE.HunterKiller));
        }

        Assert.Contains((IntentType.Attack, expectedBite, 1), seen);
        Assert.Contains((IntentType.Attack, expectedPuncture, 3), seen);
        Assert.DoesNotContain((IntentType.Attack, 24, 1), seen);
    }

    [Theory]
    [InlineData(8, 9, 14)]
    [InlineData(9, 10, 16)]
    public void LouseProgenitorWebsAndPouncesAtTheRightLevel(
        int ascension,
        int expectedWeb,
        int expectedPounce
    )
    {
        var fight = At(CombatFactory.ActOneEncounter.LouseProgenitor, ascension);

        Assert.Equal((IntentType.Attack, expectedWeb, 1), IntentOf(fight, KE.LouseProgenitor));
        fight.EndTurn();
        fight.EndTurn();
        Assert.Equal((IntentType.Attack, expectedPounce, 1), IntentOf(fight, KE.LouseProgenitor));
    }

    /// <summary>
    /// CurlBlock is <c>GetValueIfAscension(ToughEnemies, 18, 14)</c> — and Tough IS live
    /// at A8, so 18 is right here where the Deadly values next to it were not. Worth a
    /// test of its own: a sweep that "fixes" every literal would break this one.
    /// </summary>
    [Fact]
    public void LouseProgenitorCurlsForEighteenAtAscensionEight()
    {
        var fight = At(CombatFactory.ActOneEncounter.LouseProgenitor, Ascension.DefaultLevel);
        fight.EndTurn();

        Assert.Equal((IntentType.Defend, 18, 1), IntentOf(fight, KE.LouseProgenitor));
    }

    [Theory]
    [InlineData(8, 13, 4)]
    [InlineData(9, 15, 6)]
    public void MyteBitesAndSucksAtTheRightLevel(int ascension, int expectedBite, int expectedSuck)
    {
        var fight = At(CombatFactory.ActOneEncounter.Mytes, ascension);
        fight.EndTurn();
        Assert.Equal((IntentType.Attack, expectedBite, 1), IntentOf(fight, KE.Myte));

        fight.EndTurn();
        Assert.Equal((IntentType.Attack, expectedSuck, 1), IntentOf(fight, KE.Myte));
    }

    [Theory]
    [InlineData(8, 15)]
    [InlineData(9, 16)]
    public void BowlbugNectarBuffsAtTheRightLevel(int ascension, int expected)
    {
        var fight = At(CombatFactory.ActOneEncounter.Bowlbugs, ascension);
        fight.EndTurn();

        Assert.Equal((IntentType.Buff, expected, 1), IntentOf(fight, KE.BowlbugNectar));
    }
}
