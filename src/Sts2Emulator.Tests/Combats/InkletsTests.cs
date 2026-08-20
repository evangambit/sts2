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

    /// <summary>
    /// JAB leads into a branch of {PIERCING_GAZE, WHIRLWIND} and both lead back to JAB, so
    /// an Inklet alternates JAB with a rolled move. Only the FIRST move is fixed — and the
    /// middle Inklet's is WHIRLWIND, which the emulator had as PIERCING_GAZE until a live
    /// sweep read the opening as 3, 6, 3.
    /// </summary>
    [Fact]
    public void TheMiddleInkletOpensOnWhirlwindAndTheOthersOnJab()
    {
        var fight = Encounter();

        Assert.Equal(
            [(IntentType.Attack, 3), (IntentType.Attack, 6), (IntentType.Attack, 3)],
            fight.Intents
        );
    }

    [Fact]
    public void TheOpeningIsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);

        Assert.Equal(
            [(IntentType.Attack, 4), (IntentType.Attack, 9), (IntentType.Attack, 4)],
            fight.Intents
        );
    }

    /// <summary>
    /// Whatever the branch rolls, no Inklet ever announces two non-JAB moves running: the
    /// rolled move's FollowUpState is JAB, unconditionally.
    /// </summary>
    [Fact]
    public void EveryRolledMoveIsFollowedByJab()
    {
        var fight = Encounter();
        List<List<int>> perEnemy =
        [
            [],
            [],
            [],
        ];

        for (int turn = 0; turn < 6; turn++)
        {
            var intents = fight.Intents.ToList();
            for (int i = 0; i < perEnemy.Count && i < intents.Count; i++)
            {
                perEnemy[i].Add(intents[i].Magnitude);
            }

            fight.EndTurn();
        }

        foreach (var announced in perEnemy)
        {
            for (int i = 1; i < announced.Count; i++)
            {
                if (announced[i - 1] != 3)
                {
                    Assert.Equal(3, announced[i]);
                }
            }
        }
    }
}
