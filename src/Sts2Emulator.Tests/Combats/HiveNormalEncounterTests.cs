using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

internal static class HiveNormal
{
    public static Fight At(CombatFactory.ActOneEncounter encounter, int ascension = 8) =>
        Fight.Encounter(encounter, ascension);

    public static (IntentType Type, int Magnitude, int Hits) Shown(EnemyState enemy) =>
        (enemy.CurrentIntent.Type, enemy.CurrentIntent.Magnitude, enemy.CurrentIntent.Hits);

    /// <summary>
    /// What this enemy announces over the next <paramref name="turns"/> turns.
    /// </summary>
    /// <remarks>
    /// Tracked by REFERENCE, not by index: a summon is inserted in FRONT of whatever
    /// summoned it, so the Ovicopter's eggs and the Obscura's Parafright both push their
    /// own summoner down the list on the very turn it acts.
    /// </remarks>
    public static List<(IntentType, int, int)> Cycle(Fight fight, int index, int turns)
    {
        var subject = fight.State.Enemies[index];
        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < turns; turn++)
        {
            // Both sides kept alive: an enemy that outlives the PLAYER stops being asked
            // for an intent, so a cycle read past the player's death is just the last
            // announcement standing still.
            subject.Hp = 999;
            fight.State.PlayerHp = 999;
            seen.Add(Shown(subject));
            fight.EndTurn();
        }

        return seen;
    }
}

/// <summary>
/// ChompersNormal: two Chompers, the second starting on its screech.
/// </summary>
public class ChompersTests
{
    /// <summary>
    /// CLAMP is <c>MultiAttackIntent(ClampDamage, 2)</c> — 8 twice at A8, 9 twice at A9.
    /// The 18 the emulator announced was the A9 damage with the hits folded in, and the
    /// fold under-triggered every per-instance hook in the game (see the Self-Forming
    /// Clay tests, which fight these).
    /// </summary>
    [Theory]
    [InlineData(8, 8)]
    [InlineData(9, 9)]
    public void ClampIsTwoHitsAtTheRightDamage(int ascension, int expected)
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Chompers, ascension);

        Assert.Equal((IntentType.Attack, expected, 2), HiveNormal.Shown(fight.State.Enemies[0]));
    }

    [Fact]
    public void TheSecondChomperScreechesFirstAndTheyAlternate()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Chompers);

        Assert.Equal(IntentType.Debuff, fight.State.Enemies[1].CurrentIntent.Type);
        var first = HiveNormal.Cycle(fight, 0, 4).Select(i => i.Item1).ToList();
        Assert.Equal(
            [IntentType.Attack, IntentType.Debuff, IntentType.Attack, IntentType.Debuff],
            first
        );
    }
}

/// <summary>
/// HunterKillerNormal: one Hunter-Killer, gooping and then branching.
/// </summary>
public class HunterKillerTests
{
    /// <summary>
    /// TENDERIZING_GOOP once, then a RandomBranchState over BITE and PUNCTURE that both
    /// return to. Equal weights make it a coin flip; `rng.Next(3) == 0` made the bite a
    /// one-in-three. BITE is CannotRepeat and PUNCTURE may run at most twice.
    /// </summary>
    [Fact]
    public void ItGoopsThenAlternatesWithinTheRepeatCaps()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.HunterKiller);
        var seen = HiveNormal.Cycle(fight, 0, 30);

        Assert.Equal((IntentType.Debuff, 1, 1), seen[0]);

        var moves = seen.Skip(1).ToList();
        Assert.All(moves, m => Assert.Equal(IntentType.Attack, m.Item1));
        // BiteDamage 17 at A8 and PunctureDamage 7 x3 -- never the folded 24.
        Assert.All(
            moves,
            m => Assert.Contains(m, new[] { (IntentType.Attack, 17, 1), (IntentType.Attack, 7, 3) })
        );

        // No bite twice running, and never three punctures in a row.
        for (int i = 1; i < moves.Count; i++)
        {
            if (moves[i].Item3 == 1)
            {
                Assert.NotEqual(1, moves[i - 1].Item3);
            }
        }

        Assert.DoesNotContain(
            true,
            Enumerable
                .Range(2, moves.Count - 2)
                .Select(i =>
                    moves[i].Item3 == 3 && moves[i - 1].Item3 == 3 && moves[i - 2].Item3 == 3
                )
        );
    }
}

public class LouseProgenitorTests
{
    /// <summary>WEB -> CURL -> POUNCE, cycling. Curl's block is the TOUGH pair.</summary>
    [Fact]
    public void ItWebsCurlsAndPouncesInOrder()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.LouseProgenitor);

        Assert.Equal(
            [
                (IntentType.Attack, 9, 1),
                (IntentType.Defend, 18, 1),
                (IntentType.Attack, 14, 1),
                (IntentType.Attack, 9, 1),
            ],
            HiveNormal.Cycle(fight, 0, 4)
        );
    }
}

public class MytesTests
{
    /// <summary>
    /// The machine opens on a ConditionalBranchState keyed to SlotName: the first Myte
    /// starts on TOXIC and the second on SUCK, which is phase TWO of the cycle. Sharing
    /// one `MoveIndex % 3` put the second on the first's beat from turn two.
    /// </summary>
    [Fact]
    public void TheTwoMytesRunTheSameCycleTwoMovesApart()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Mytes);

        var first = HiveNormal.Cycle(fight, 0, 3).Select(i => i.Item1).ToList();
        Assert.Equal([IntentType.Debuff, IntentType.Attack, IntentType.Attack], first);

        var fresh = HiveNormal.At(CombatFactory.ActOneEncounter.Mytes);
        var second = HiveNormal.Cycle(fresh, 1, 3);
        // SUCK, then TOXIC, then BITE.
        Assert.Equal((IntentType.Attack, 4, 1), second[0]);
        Assert.Equal((IntentType.Debuff, 2, 1), second[1]);
        Assert.Equal((IntentType.Attack, 13, 1), second[2]);
    }

    /// <summary>
    /// SUCK_MOVE declares its attack and then a BuffIntent — StrengthPower(SuckStrength)
    /// on itself. Nothing applied it: the Myte's "suck" is a plain per-move Strength, not
    /// the per-HIT SuckPower the Fossil Stalker carries, and only the stalker was ever
    /// given <c>BuffId.Suck</c>. So a Myte announced the same three numbers for the whole
    /// fight where the game's climb every third turn.
    /// </summary>
    [Theory]
    [InlineData(8, 2, 4, 13)]
    [InlineData(9, 3, 6, 15)]
    public void EachSuckMakesTheMyteBigger(int ascension, int suckStrength, int suck, int bite)
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Mytes, ascension);
        var myte = fight.State.Enemies[0];

        // TOXIC, BITE, SUCK -- so one suck has landed by the end of the third turn.
        fight.State.PlayerHp = 999;
        HiveNormal.Cycle(fight, 0, 3);

        Assert.Equal(suckStrength, BuffSystem.Get(myte.Buffs, BuffId.Strength));

        // And the announcement carries it: the second pass reads base plus the Strength
        // the first suck bought, on both of the Myte's attacks.
        var second = GloryNormal.Cycle(fight, myte, 3);
        Assert.Equal(
            [
                (IntentType.Debuff, 2, 1),
                (IntentType.Attack, bite + suckStrength, 1),
                (IntentType.Attack, suck + suckStrength, 1),
            ],
            second
        );
        Assert.Equal(suckStrength * 2, BuffSystem.Get(myte.Buffs, BuffId.Strength));
    }
}

public class OvicopterTests
{
    /// <summary>
    /// LAY_EGGS -> SMASH -> TENDERIZER -> a branch back to LAY_EGGS or to
    /// NUTRITIONAL_PASTE, both of which lead to SMASH. A THREE-cycle whose first slot is
    /// one or the other — `% 4` gave it a fourth turn it does not have.
    /// </summary>
    [Fact]
    public void ItRunsAThreeCycleNotAFour()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Ovicopter);
        var types = HiveNormal.Cycle(fight, 0, 7).Select(i => i.Item1).ToList();

        Assert.Equal(
            [
                IntentType.Buff,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Buff,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Buff,
            ],
            types
        );
    }

    /// <summary>
    /// CanLay is `living teammates <= 3`. Alone it always lays; crowded it makes paste
    /// instead, and the paste's Strength is the Deadly pair.
    /// </summary>
    [Fact]
    public void ACrowdedOvicopterMakesPasteInsteadOfLaying()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Ovicopter);
        for (int i = 0; i < 4; i++)
        {
            fight.State.Enemies.Add(
                new EnemyState
                {
                    DefId = KE.ToughEgg,
                    Hp = 5,
                    MaxHp = 5,
                }
            );
        }

        var ovicopter = fight.State.Enemies[0];
        ovicopter.MoveIndex = 2;
        ovicopter.Hp = 999;
        fight.EndTurn();

        Assert.Equal((IntentType.Buff, 3, 1), HiveNormal.Shown(ovicopter));
    }
}

public class SlumberingBeetleTests
{
    [Fact]
    public void TheEncounterIsARockASilkAndTheBeetle()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.SlumberingBeetle);

        Assert.Equal([KE.BowlbugRock, KE.BowlbugSilk, KE.SlumberingBeetle], fight.EnemyDefIds);
    }

    /// <summary>
    /// SlumberPower is applied at 3 and ticks once per enemy turn, so a beetle nobody
    /// touches sleeps three turns and then rolls out forever.
    /// </summary>
    [Fact]
    public void AnUntouchedBeetleSleepsThreeTurns()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.SlumberingBeetle);
        var types = HiveNormal.Cycle(fight, 2, 5).Select(i => i.Item1).ToList();

        Assert.Equal(
            [
                IntentType.Unknown,
                IntentType.Unknown,
                IntentType.Unknown,
                IntentType.Attack,
                IntentType.Attack,
            ],
            types
        );
    }

    /// <summary>
    /// Its OTHER decrement: every instance of unblocked damage it takes. Hitting it is
    /// the obvious play against something asleep behind Plating, and the emulator used to
    /// count turns alone — so a beetle under attack woke late here and on time in game.
    /// </summary>
    [Fact]
    public void HittingTheBeetleWakesItEarly()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.SlumberingBeetle);
        var beetle = fight.State.Enemies[2];
        Assert.Equal(3, BuffSystem.Get(beetle.Buffs, BuffId.Slumber));

        // Two Strikes past the Plating, in one turn: two INSTANCES, two points of sleep.
        beetle.Block = 0;
        BuffSystem.Remove(beetle.Buffs, BuffId.Plating);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        fight.Play(0, target: 2);
        fight.Play(0, target: 2);

        Assert.Equal(1, BuffSystem.Get(beetle.Buffs, BuffId.Slumber));

        fight.EndTurn();

        // One left, spent by the turn tick -- so it wakes a full turn early.
        Assert.Equal(IntentType.Attack, beetle.CurrentIntent.Type);
        Assert.Equal(16, beetle.CurrentIntent.Magnitude);
    }
}

public class SpinyToadTests
{
    /// <summary>
    /// SPIKES -> EXPLOSION -> LASH, cycling. Spikes applies ThornsPower at a flat 5 with
    /// no ascension term; the two attacks are both Deadly pairs.
    /// </summary>
    [Theory]
    [InlineData(8, 23, 17)]
    [InlineData(9, 25, 19)]
    public void ItSpikesExplodesAndLashes(int ascension, int explosion, int lash)
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.SpinyToad, ascension);

        Assert.Equal(
            [
                (IntentType.Buff, 5, 1),
                (IntentType.Attack, explosion, 1),
                (IntentType.Attack, lash, 1),
                (IntentType.Buff, 5, 1),
            ],
            HiveNormal.Cycle(fight, 0, 4)
        );
    }
}

public class ObscuraTests
{
    /// <summary>
    /// ILLUSION once, then a RandomBranchState over PIERCING_GAZE, WAIL and
    /// HARDENING_STRIKE — all three CannotRepeat, so the move just performed has weight
    /// zero and the choice is between the other two. `rng.Next(3)` gave it a one-in-three
    /// chance of a move the game cannot pick.
    /// </summary>
    [Fact]
    public void ItNeverRepeatsAMoveTwiceRunning()
    {
        var fight = HiveNormal.At(CombatFactory.ActOneEncounter.Obscura);
        var seen = HiveNormal.Cycle(fight, 0, 25);

        Assert.Equal((IntentType.Buff, 0, 1), seen[0]);
        var moves = seen.Skip(1).ToList();

        // PiercingGaze 10, WAIL's Strength 3, HardeningStrike 6 -- all at A8.
        Assert.All(
            moves,
            m =>
                Assert.Contains(
                    m,
                    new[]
                    {
                        (IntentType.Attack, 10, 1),
                        (IntentType.Buff, 3, 1),
                        (IntentType.Attack, 6, 1),
                    }
                )
        );
        for (int i = 1; i < moves.Count; i++)
        {
            Assert.NotEqual(moves[i - 1], moves[i]);
        }
    }
}
