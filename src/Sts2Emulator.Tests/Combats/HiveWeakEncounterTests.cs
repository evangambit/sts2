using System.Linq;
using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Hive's four weak encounters — the first fights an act-2 run meets.
/// </summary>
/// <remarks>
/// Every one of the four had something wrong with it, and the move cycles were the worst
/// of it: three monsters were transcribed as `MoveIndex % n` where the game's machine
/// SETTLES rather than loops. `FollowUpState` pointing at itself is the tell, and it is
/// easy to read as "and then repeat from the top".
/// </remarks>
internal static class HiveWeak
{
    public static Fight At(CombatFactory.ActOneEncounter encounter, int ascension = 8) =>
        Fight.Encounter(encounter, ascension);

    /// <summary>This enemy's intent as (type, magnitude), which is what a readout shows.</summary>
    public static (IntentType Type, int Magnitude) Announced(EnemyState enemy) =>
        (enemy.CurrentIntent.Type, enemy.CurrentIntent.Magnitude);
}

public class BowlbugsWeakTests
{
    private static Fight At(CombatFactory.ActOneEncounter e, int a = 8) => HiveWeak.At(e, a);

    [Fact]
    public void BowlbugsWeakIsARockAndOneWorker()
    {
        var fight = At(CombatFactory.ActOneEncounter.BowlbugsWeak);

        Assert.Equal(2, fight.State.Enemies.Count);
        Assert.Equal(KE.BowlbugRock, fight.State.Enemies[0].DefId);
        // NextItem over [BowlbugEgg, BowlbugNectar] -- never the Silk, which only the
        // normal encounter can field.
        Assert.Contains(fight.State.Enemies[1].DefId, new[] { KE.BowlbugEgg, KE.BowlbugNectar });
    }

    [Fact]
    public void BowlbugsNormalIsARockAndTwoDistinctWorkers()
    {
        var fight = At(CombatFactory.ActOneEncounter.Bowlbugs);

        Assert.Equal(3, fight.State.Enemies.Count);
        Assert.Equal(KE.BowlbugRock, fight.State.Enemies[0].DefId);
        // _workerValidCounts caps each worker at one, and the loop re-derives the
        // candidates each pass -- so the two are always different.
        Assert.NotEqual(fight.State.Enemies[1].DefId, fight.State.Enemies[2].DefId);
    }

    [Theory]
    [InlineData(8, 46, 49)]
    [InlineData(7, 45, 48)]
    public void TheRocksHpBandMovesWithToughEnemies(int ascension, int min, int max)
    {
        var fight = At(CombatFactory.ActOneEncounter.BowlbugsWeak, ascension);

        Assert.InRange(fight.State.Enemies[0].MaxHp, min, max);
    }

    /// <summary>
    /// The Rock headbutts EVERY turn. It only owes a dizzy turn when its own attack was
    /// fully blocked -- ImbalancedPower.AfterDamageGiven fires on WasFullyBlocked.
    /// </summary>
    [Fact]
    public void TheRockHeadbuttsEveryTurnWhenNothingBlocksIt()
    {
        var fight = At(CombatFactory.ActOneEncounter.BowlbugsWeak);
        var rock = fight.State.Enemies[0];

        for (int turn = 0; turn < 4; turn++)
        {
            Assert.Equal(
                (IntentType.Attack, 15),
                (rock.CurrentIntent.Type, rock.CurrentIntent.Magnitude)
            );
            fight.State.PlayerBlock = 0;
            fight.EndTurn();
        }
    }

    [Fact]
    public void FullyBlockingTheHeadbuttCostsTheRockItsNextTurn()
    {
        var fight = At(CombatFactory.ActOneEncounter.BowlbugsWeak);
        var rock = fight.State.Enemies[0];
        Assert.Equal(IntentType.Attack, rock.CurrentIntent.Type);

        // Enough block to swallow the headbutt whole.
        fight.State.PlayerBlock = 99;
        fight.EndTurn();

        Assert.Equal(IntentType.Unknown, rock.CurrentIntent.Type);

        // And the dizzy turn clears it -- back to headbutting after.
        fight.State.PlayerBlock = 0;
        fight.EndTurn();
        Assert.Equal(IntentType.Attack, rock.CurrentIntent.Type);
    }

    /// <summary>
    /// THRASH -> BUFF -> THRASH2, and THRASH2 follows up to ITSELF, so it never buffs
    /// twice. `MoveIndex % 3` gave it a second Buff on turn four.
    /// </summary>
    [Fact]
    public void TheNectarBuffsOnceAndThenThrashesForever()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs, 8);
        var nectar = fight.State.Enemies.FirstOrDefault(e => e.DefId == KE.BowlbugNectar);
        Assert.NotNull(nectar);

        var seen = new List<(IntentType, int)>();
        for (int turn = 0; turn < 6; turn++)
        {
            seen.Add((nectar!.CurrentIntent.Type, nectar.CurrentIntent.Magnitude));
            fight.EndTurn();
        }

        Assert.Equal((IntentType.Buff, 15), seen[1]);
        Assert.All(
            seen.Where((_, i) => i != 1),
            entry => Assert.Equal((IntentType.Attack, 3), entry)
        );
    }
}

public class BowlbugsTests
{
    private static Fight At(CombatFactory.ActOneEncounter e, int a = 8) => HiveWeak.At(e, a);

    [Fact]
    public void SeeAlsoBowlbugsWeakTests()
    {
        // The Rock and the workers are shared, so the shape assertions live next door;
        // what is only true of the NORMAL encounter is its two distinct workers.
        var fight = At(CombatFactory.ActOneEncounter.Bowlbugs);

        Assert.Equal(3, fight.State.Enemies.Count);
        Assert.NotEqual(fight.State.Enemies[1].DefId, fight.State.Enemies[2].DefId);
    }
}

public class ExoskeletonsTests
{
    private static Fight At(CombatFactory.ActOneEncounter e, int a = 8) => HiveWeak.At(e, a);

    [Fact]
    public void ExoskeletonsWeakIsThreeExoskeletons()
    {
        var fight = At(CombatFactory.ActOneEncounter.Exoskeletons);

        Assert.Equal([KE.Exoskeleton, KE.Exoskeleton, KE.Exoskeleton], fight.EnemyDefIds);
    }
}

public class ExoskeletonsNormalTests
{
    [Fact]
    public void TheNormalEncounterIsFourExoskeletons()
    {
        // It had no case in the roster switch at all and threw out of CombatFactory when
        // Hive dealt it, which no coverage list built from the CODE could have seen.
        var fight = HiveWeak.At(CombatFactory.ActOneEncounter.ExoskeletonsNormal);

        Assert.Equal(
            [KE.Exoskeleton, KE.Exoskeleton, KE.Exoskeleton, KE.Exoskeleton],
            fight.EnemyDefIds
        );
    }
}

public class ThievingHopperTests
{
    private static Fight At(CombatFactory.ActOneEncounter e, int a = 8) => HiveWeak.At(e, a);

    [Fact]
    public void ThievingHopperWeakIsOneHopper()
    {
        var fight = At(CombatFactory.ActOneEncounter.ThievingHopper);

        Assert.Equal([KE.ThievingHopper], fight.EnemyDefIds);
        Assert.Equal(84, fight.State.Enemies[0].MaxHp);
    }

    /// <summary>
    /// THIEVERY -> FLUTTER -> HAT_TRICK -> NAB -> ESCAPE, and ESCAPE follows up to
    /// ITSELF. `% 5` sent a Hopper that had already left back round to steal again.
    /// </summary>
    [Fact]
    public void TheHopperRunsItsRoutineOnceAndThenLeaves()
    {
        var fight = At(CombatFactory.ActOneEncounter.ThievingHopper);
        var hopper = fight.State.Enemies[0];
        hopper.Hp = 999;

        var seen = new List<(IntentType, int)>();
        for (int turn = 0; turn < 7; turn++)
        {
            seen.Add((hopper.CurrentIntent.Type, hopper.CurrentIntent.Magnitude));
            fight.EndTurn();
        }

        // TheftDamage 17, a bare BuffIntent carrying no number, HatTrick 21, Nab 14.
        Assert.Equal((IntentType.Attack, 17), seen[0]);
        Assert.Equal((IntentType.Buff, 0), seen[1]);
        Assert.Equal((IntentType.Attack, 21), seen[2]);
        Assert.Equal((IntentType.Attack, 14), seen[3]);
        Assert.All(seen.Skip(4), entry => Assert.Equal((IntentType.Unknown, 0), entry));
    }
}

public class TunnelerTests
{
    private static Fight At(CombatFactory.ActOneEncounter e, int a = 8) => HiveWeak.At(e, a);

    [Fact]
    public void TunnelerWeakIsOneTunneler()
    {
        var fight = At(CombatFactory.ActOneEncounter.Tunneler);

        Assert.Equal([KE.Tunneler], fight.EnemyDefIds);
        Assert.Equal(92, fight.State.Enemies[0].MaxHp);
    }

    /// <summary>
    /// BITE -> BURROW -> BELOW, and BELOW follows up to ITSELF. `% 3` walked it back to
    /// the bite every fourth turn, at a third of the damage.
    /// </summary>
    [Fact]
    public void TheTunnelerBurrowsOnceAndThenHitsFromBelowForever()
    {
        var fight = At(CombatFactory.ActOneEncounter.Tunneler);
        var tunneler = fight.State.Enemies[0];
        tunneler.Hp = 999;

        var seen = new List<(IntentType, int)>();
        for (int turn = 0; turn < 6; turn++)
        {
            seen.Add((tunneler.CurrentIntent.Type, tunneler.CurrentIntent.Magnitude));
            fight.EndTurn();
        }

        // BiteDamage 13, then BURROW, then BelowDamage 23 for good.
        Assert.Equal((IntentType.Attack, 13), seen[0]);
        // BURROW_MOVE declares its BuffIntent FIRST and its DefendIntent second, so the
        // readout is a bare buff with no number. This asserted (Defend, 37) until a live
        // capture showed (Buff, none) -- E12's rule, and the block is still 37, it is
        // just not what the intent SAYS.
        Assert.Equal((IntentType.Buff, 0), seen[1]);
        Assert.All(seen.Skip(2), entry => Assert.Equal((IntentType.Attack, 23), entry));
    }

    /// <summary>
    /// <c>BurrowedPower</c>: the block it gains does not clear between turns.
    /// </summary>
    /// <remarks>
    /// `ShouldClearBlock` returns false for its owner, so the 37 stands until the player
    /// chews through it. A live capture shows the Tunneler on exactly 92 HP for three
    /// turns running while it is dug in.
    /// </remarks>
    [Fact]
    public void TheBurrowsBlockPersistsAcrossTurns()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Tunneler);
        var tunneler = fight.State.Enemies[0];
        tunneler.Hp = 999;
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // BITE
        fight.EndTurn(); // BURROW

        Assert.Equal(1, BuffSystem.Get(tunneler.Buffs, BuffId.Burrowed));
        Assert.Equal(37, tunneler.Block);

        fight.EndTurn();
        Assert.Equal(37, tunneler.Block);
    }

    /// <summary>
    /// Breaking the burrow is the ONLY way out of BELOW_MOVE, which follows up to itself.
    /// </summary>
    /// <remarks>
    /// `AfterBlockBroken` stuns the Tunneler into DIZZY_MOVE and then back to BITE_MOVE,
    /// and `AfterRemoved` takes the rest of the block with it. Without this the emulator's
    /// Tunneler hit from below for the rest of the fight however hard it was struck.
    /// </remarks>
    [Fact]
    public void BreakingTheBurrowStunsItBackToTheBite()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Tunneler);
        var tunneler = fight.State.Enemies[0];
        tunneler.Hp = 999;
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // BITE
        fight.EndTurn(); // BURROW
        Assert.Equal(37, tunneler.Block);

        // Break it.
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 9;
        tunneler.Block = 1;
        fight.Play(0, target: 0);

        Assert.Equal(0, BuffSystem.Get(tunneler.Buffs, BuffId.Burrowed));
        Assert.Equal(0, tunneler.Block);

        // A turn spent dizzy, and then the table starts over at the bite.
        fight.EndTurn();
        Assert.Equal(
            (IntentType.Attack, 13),
            (tunneler.CurrentIntent.Type, tunneler.CurrentIntent.Magnitude)
        );
    }
}
