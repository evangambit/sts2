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

    /// <summary>
    /// ILLUSION_MOVE -> SWIPE_MOVE is fixed; what follows is a RandomBranchState weighted
    /// 0.4 to another swipe and 0.6 to the headbutt. So the first two turns are the only
    /// ones a test can name, and Swipe is 8 at A8 — the emulator had 9, its A9 value.
    /// </summary>
    [Fact]
    public void OpensOnIllusionThenSwipe()
    {
        var fight = Encounter();

        Assert.Equal((IntentType.Buff, 0), FogmogIntent(fight));
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 8), FogmogIntent(fight));
    }

    [Fact]
    public void SwipeIsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 9), FogmogIntent(fight));
    }

    /// <summary>
    /// Every SWIPE hands the Fogmog StrengthPower(1), so what it announces climbs — but
    /// swipes and headbutts interleave, so the sequence is not monotonic. The rule that
    /// does hold: each announcement is its move's printed damage plus the Strength the
    /// Fogmog is carrying at that moment. A live six-turn trace reads 8, 9, 16, 10, 11, 18.
    /// </summary>
    [Fact]
    public void EveryAnnouncementIsItsMovePlusCurrentStrength()
    {
        var fight = Encounter();
        fight.EndTurn();

        var seen = new List<int>();
        for (int turn = 0; turn < 5; turn++)
        {
            var fogmog = fight.State.Enemies[fight.EnemyDefIds.ToList().IndexOf(KE.Fogmog)];
            int strength = BuffSystem.Get(fogmog.Buffs, BuffId.Strength);
            var intent = FogmogIntent(fight);

            Assert.Equal(IntentType.Attack, intent.Type);
            Assert.Contains(intent.Magnitude, (int[])[8 + strength, 14 + strength]);
            seen.Add(strength);
            fight.EndTurn();
        }

        // Strength only ever climbs, because only SWIPE grants it and nothing spends it.
        Assert.Equal(seen.OrderBy(value => value), seen);
        Assert.True(seen[^1] > seen[0], "swiping should have built Strength");
    }

    /// <summary>
    /// Whatever the branch rolls, HEADBUTT follows a second swipe and leads back to the
    /// first — so a run of turns has to contain one, at 14 plus the Strength by then.
    /// </summary>
    [Fact]
    public void HeadbuttComesRoundAndIsWorthFourteenPlusStrength()
    {
        var fight = Encounter();
        var seen = new List<int>();
        for (int turn = 0; turn < 6; turn++)
        {
            var fogmog = fight.State.Enemies[fight.EnemyDefIds.ToList().IndexOf(KE.Fogmog)];
            int strength = BuffSystem.Get(fogmog.Buffs, BuffId.Strength);
            var intent = FogmogIntent(fight);
            if (intent.Type == IntentType.Attack && intent.Magnitude == 14 + strength)
            {
                seen.Add(intent.Magnitude);
            }

            fight.EndTurn();
        }

        Assert.NotEmpty(seen);
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
