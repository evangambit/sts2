using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// DecimillipedeElite: three segments that come back.
/// </summary>
public class DecimillipedeTests
{
    /// <summary>
    /// The machine walks WRITHE -> CONSTRICT -> BULK, but the STARTER index numbers the
    /// moves 0/1/2 = WRITHE/BULK/CONSTRICT. Walking the starter numbering as though it
    /// were the cycle put every segment's second and third moves the wrong way round.
    /// </summary>
    [Fact]
    public void ASegmentWrithesThenConstrictsThenBulks()
    {
        var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, 0);
        var segment = fight.State.Enemies.First(e => e.MoveIndex == 0);

        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < 4; turn++)
        {
            segment.Hp = 999;
            fight.State.PlayerHp = 999;
            seen.Add(
                (
                    segment.CurrentIntent.Type,
                    segment.CurrentIntent.Magnitude,
                    segment.CurrentIntent.Hits
                )
            );
            fight.EndTurn();
        }

        Assert.Equal(
            [
                // WRITHE: MultiAttackIntent(WritheDamage, 2) -- 5 twice at A8, never a
                // folded 12.
                (IntentType.Attack, 5, 2),
                // CONSTRICT: ConstrictDamage, with Weak on the player.
                (IntentType.Attack, 8, 1),
                // BULK: BulkDamage, with Strength 2 on itself.
                (IntentType.Attack, 6, 1),
                (IntentType.Attack, 5, 2),
            ],
            seen
        );
    }

    /// <summary>The three start on three different moves, one draw deciding all of them.</summary>
    [Fact]
    public void TheThreeSegmentsStartOnDifferentMoves()
    {
        for (int seed = 0; seed < 6; seed++)
        {
            var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, seed);

            Assert.Equal(3, fight.State.Enemies.Count);
            Assert.Equal(3, fight.State.Enemies.Select(e => e.MoveIndex % 3).Distinct().Count());
        }
    }

    /// <summary>
    /// AfterAddedToRoom forces each segment's max HP even and distinct, wrapping inside
    /// the band -- so no two share a total.
    /// </summary>
    [Fact]
    public void EverySegmentHasAnEvenAndUniqueMaxHp()
    {
        var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, 3);

        Assert.All(fight.State.Enemies, e => Assert.Equal(0, e.MaxHp % 2));
        Assert.Equal(3, fight.State.Enemies.Select(e => e.MaxHp).Distinct().Count());
        Assert.All(fight.State.Enemies, e => Assert.InRange(e.MaxHp, 46, 52));
    }

    /// <summary>
    /// ReattachPower: a killed segment spends a turn dead and comes back healed by 25 --
    /// not to full, which is what separates it from the Fogmog eye. The emulator left it
    /// dead, so the elite could be taken apart one piece at a time.
    /// </summary>
    [Fact]
    public void AKilledSegmentComesBackHurt()
    {
        var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, 0);
        var segment = fight.State.Enemies[0];
        fight.State.PlayerHp = 999;

        // Killed by an actual hit: the death hooks run off the HP CROSSING zero during
        // the step, so a segment zeroed beforehand is simply a corpse nobody killed.
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        segment.Hp = 1;
        segment.Block = 0;
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(3, fight.State.Enemies.Count);
        Assert.Equal(RunConstants.DecimillipedeReattachHeal, segment.Hp);
        Assert.True(segment.Hp < segment.MaxHp, "reattach heals 25, it does not restore");
    }

    /// <summary>
    /// REATTACH_MOVE's FollowUpState is the machine's RandomBranchState, not the cycle, so
    /// a segment that comes back ROLLS its next move — and every branch is CannotRepeat,
    /// so it cannot roll the one it last performed. The emulator resumed the cycle where
    /// the segment fell, which is both the wrong move and a draw on the AI stream the
    /// game makes and it did not.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AReattachedSegmentRollsRatherThanResuming(int seed)
    {
        var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, seed);
        var segment = fight.State.Enemies[0];
        fight.State.PlayerHp = 999;

        // One whole turn first, so the segment has actually PERFORMED a move: the repeat
        // rule is scored against that, not against the announcement it dies holding.
        fight.EndTurn();
        fight.State.PlayerHp = 999;
        int performed = (segment.MoveIndex - 1 + 3) % 3;

        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        segment.Hp = 1;
        segment.Block = 0;
        fight.Play(0, target: 0);
        fight.EndTurn(); // the reattach turn

        fight.State.PlayerHp = 999;
        fight.EndTurn(); // and the roll it announces afterwards

        Assert.NotEqual(performed, (segment.MoveIndex - 1 + 3) % 3);
    }

    /// <summary>
    /// `if (!AreAllOtherSegmentsDead())` is the whole fight: the last one standing stays
    /// down. Without it the elite is unkillable.
    /// </summary>
    [Fact]
    public void TheLastSegmentStandingDoesNotComeBack()
    {
        var fight = Fight.EncounterWithStream(RunConstants.DecimillipedeEncounterId, 0);
        fight.State.PlayerHp = 999;
        fight.State.Enemies[1].Hp = 0;
        fight.State.Enemies[2].Hp = 0;

        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        fight.State.Enemies[0].Hp = 1;
        fight.State.Enemies[0].Block = 0;
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.All(fight.State.Enemies, e => Assert.Equal(0, e.Hp));
    }
}

public class EntomancerTests
{
    /// <summary>
    /// Opens on BEES, then SPEAR, then PHEROMONE_SPIT. BEES is
    /// MultiAttackIntent(BeesDamage, BeesRepeat) where the damage is 3 at both levels and
    /// the REPEAT is what ascension moves -- so the folded 24 was the A9 hit count
    /// multiplied into the damage, the Exoskeleton's mistake again.
    /// </summary>
    [Theory]
    [InlineData(8, 7, 18)]
    [InlineData(9, 8, 20)]
    public void ItBeesThenSpearsThenSpits(int ascension, int beeHits, int spear)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Entomancer, ascension);
        var entomancer = fight.State.Enemies[0];

        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < 4; turn++)
        {
            entomancer.Hp = 999;
            fight.State.PlayerHp = 999;
            seen.Add(
                (
                    entomancer.CurrentIntent.Type,
                    entomancer.CurrentIntent.Magnitude,
                    entomancer.CurrentIntent.Hits
                )
            );
            fight.EndTurn();
        }

        Assert.Equal(
            [
                (IntentType.Attack, 3, beeHits),
                (IntentType.Attack, spear, 1),
                (IntentType.Buff, 1, 1),
                (IntentType.Attack, 3, beeHits),
            ],
            seen
        );
    }

    /// <summary>
    /// AfterAddedToRoom applies PersonalHivePower at 1, and PHEROMONE_SPIT reads it:
    /// below 3 it grows the hive and takes Strength 1, at 3 it takes Strength 2 instead.
    /// Starting the hive at zero bought it an extra growth before the switch.
    /// </summary>
    [Fact]
    public void ItStartsWithOneHiveStack()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Entomancer);

        Assert.Equal(1, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.PersonalHive));
    }
}

public class InfestedPrismsTests
{
    /// <summary>
    /// <c>VitalSparkPower</c> is the Skill-card twin of the Globe Head's Galvanic: the
    /// prism arrives holding VitalSparkAmount, PULSATE stacks another on top, and every
    /// Skill the player plays stamps them with TaintedPower — which adds that much to
    /// every POWERED attack against them until the enemy turn ends.
    /// </summary>
    /// <remarks>
    /// None of it was modelled. The ascension audit flagged VitalSparkAmount's 3 as an A9
    /// literal, which was a false positive of the documented kind — the 3 in the prism's
    /// block is WhirlwindRepeat, a hit count — and reading it turned up a whole power
    /// missing instead.
    /// </remarks>
    [Theory]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    public void PlayingASkillTaintsThePlayer(int ascension, int spark)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.InfestedPrisms, ascension);
        var prism = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        Assert.Equal(spark, BuffSystem.Get(prism.Buffs, BuffId.VitalSpark));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Tainted));

        fight.State.Hand = [new CardInstance(IC.DefendIronclad, false)];
        fight.State.Energy = 3;
        fight.Play();

        Assert.Equal(spark, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Tainted));

        // A tainted player takes that much more from every powered attack -- JabDamage
        // plus the taint.
        int announced = prism.CurrentIntent.AnnouncedDamage(prism.Buffs, fight.State.PlayerBuffs);
        Assert.Equal(prism.CurrentIntent.Magnitude + spark, announced);

        // TaintedPower removes itself outright at the end of the enemy turn.
        fight.EndTurn();
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Tainted));
    }

    /// <summary>PULSATE stacks another Vital Spark on top of the one it arrived with.</summary>
    [Fact]
    public void PulsateStacksAnotherSpark()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.InfestedPrisms);
        var prism = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        fight.Turns(4); // JAB, RADIATE, WHIRLWIND, PULSATE

        Assert.Equal(4, BuffSystem.Get(prism.Buffs, BuffId.VitalSpark));
    }

    /// <summary>
    /// JAB -> RADIATE -> WHIRLWIND -> PULSATE, cycling. WHIRLWIND is
    /// MultiAttackIntent(WhirlwindDamage, 3) and was folded into 18.
    /// </summary>
    [Theory]
    [InlineData(8, 15, 11, 5, 8)]
    [InlineData(9, 17, 13, 6, 10)]
    public void ItRunsItsFourMoveCycle(
        int ascension,
        int jab,
        int radiate,
        int whirlwind,
        int pulsate
    )
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.InfestedPrisms, ascension);
        var prism = fight.State.Enemies[0];

        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < 5; turn++)
        {
            prism.Hp = 999;
            fight.State.PlayerHp = 999;
            seen.Add(
                (prism.CurrentIntent.Type, prism.CurrentIntent.Magnitude, prism.CurrentIntent.Hits)
            );
            fight.EndTurn();
        }

        Assert.Equal(
            [
                (IntentType.Attack, jab, 1),
                (IntentType.Attack, radiate, 1),
                (IntentType.Attack, whirlwind, 3),
                (IntentType.Attack, pulsate, 1),
                (IntentType.Attack, jab, 1),
            ],
            seen
        );
    }

    /// <summary>
    /// Only RADIATE and PULSATE carry a DefendIntent, and for different amounts. A flat
    /// 22 after every attack gave the prism block on its jab and its whirlwind too.
    /// </summary>
    [Fact]
    public void OnlyTwoOfItsFourMovesGainBlock()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.InfestedPrisms);
        var prism = fight.State.Enemies[0];

        var blockAfter = new List<int>();
        for (int turn = 0; turn < 4; turn++)
        {
            prism.Hp = 999;
            fight.State.PlayerHp = 999;
            fight.EndTurn();
            blockAfter.Add(prism.Block);
        }

        // JAB none, RADIATE 11, WHIRLWIND none, PULSATE 22 (the TOUGH pair, live at A8).
        Assert.Equal([0, 11, 0, 22], blockAfter);
    }
}
