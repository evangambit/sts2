using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

internal static class Boss
{
    /// <summary>What this creature announces over the next turns, both sides kept alive.</summary>
    public static List<(IntentType, int, int)> Cycle(Fight fight, EnemyState subject, int turns)
    {
        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < turns; turn++)
        {
            foreach (var enemy in fight.State.Enemies)
            {
                enemy.Hp = 9999;
                // The Insatiable's SandpitPower is a countdown that KILLS the player when
                // it empties, so a fight walked past turn four ends and the last intent
                // stands still. Topped up here for the same reason the HP is: this walks
                // the move table, and the table is not what the sandpit is about.
                if (BuffSystem.Get(enemy.Buffs, BuffId.Sandpit) > 0)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Sandpit, 99);
                }
            }

            fight.State.PlayerHp = 9999;
            seen.Add(
                (
                    subject.CurrentIntent.Type,
                    subject.CurrentIntent.Magnitude,
                    subject.CurrentIntent.Hits
                )
            );
            fight.EndTurn();

            // A monster can stop the turn to ask the player something -- the Knowledge
            // Demon's curse screen does -- and nothing advances until it is answered.
            // Always the first candidate, so the walk is deterministic.
            while (fight.Pending is not null)
            {
                fight.Choose(0);
            }
        }

        return seen;
    }
}

/// <summary>
/// KaiserCrabBoss: a Crusher on the player's left and a Rocket on their right.
/// </summary>
public class KaiserCrabTests
{
    private static Fight Crab(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.KaiserCrab, ascension);

    [Theory]
    [InlineData(8, 12, 6, 2)]
    [InlineData(9, 14, 7, 3)]
    public void TheCrusherRunsItsFiveMoveCycle(
        int ascension,
        int thrash,
        int sting,
        int adaptStrength
    )
    {
        var fight = Crab(ascension);

        Assert.Equal(
            [
                (IntentType.Attack, thrash, 1),
                // EnlargingStrikeDamage is 4 at BOTH levels.
                (IntentType.Attack, 4, 1),
                // BUG_STING: MultiAttackIntent(BugStingDamage, 2), never a folded 20.
                (IntentType.Attack, sting, 2),
                (IntentType.Buff, adaptStrength, 1),
                // GUARDED_STRIKE, same damage as the thrash plus 18 block.
                (IntentType.Attack, thrash, 1),
                (IntentType.Attack, thrash, 1),
            ],
            Boss.Cycle(fight, fight.State.Enemies[0], 6)
        );
    }

    [Theory]
    [InlineData(8, 3, 18, 2, 31)]
    [InlineData(9, 4, 20, 3, 35)]
    public void TheRocketRunsItsFiveMoveCycle(
        int ascension,
        int reticle,
        int beam,
        int charge,
        int laser
    )
    {
        var fight = Crab(ascension);

        Assert.Equal(
            [
                (IntentType.Attack, reticle, 1),
                (IntentType.Attack, beam, 1),
                (IntentType.Buff, charge, 1),
                (IntentType.Attack, laser, 1),
                // RECHARGE_MOVE is a SleepIntent: a turn spent doing nothing.
                (IntentType.Unknown, 0, 1),
                (IntentType.Attack, reticle, 1),
            ],
            Boss.Cycle(fight, fight.State.Enemies[1], 6)
        );
    }

    /// <summary>
    /// SurroundedPower goes on the PLAYER and starts facing Right, so the Crusher — which
    /// carries BackAttackLeft — is at their back and hits for 1.5x. The emulator used to
    /// bake that multiplier into the Crusher's announced damage, which is a number that
    /// can never stop being wrong.
    /// </summary>
    [Fact]
    public void TheCrusherHitsFromBehindForHalfAgain()
    {
        var fight = Crab();
        Assert.Equal(
            RunConstants.FacingRight,
            BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Surrounded)
        );

        var crusher = fight.State.Enemies[0];
        var rocket = fight.State.Enemies[1];
        // Only the Crusher acts, on its opening THRASH of 12.
        rocket.Hp = 9999;
        rocket.CurrentIntent = new Intent(IntentType.Unknown, 0);
        fight.State.PlayerHp = 500;
        fight.State.PlayerBlock = 0;
        crusher.Hp = 9999;

        fight.EndTurn();

        // 12 x 1.5 = 18, not 12 and not the old baked-in 21.
        Assert.Equal(500 - 18, fight.State.PlayerHp);
    }

    /// <summary>
    /// The player only turns when one half dies — and turning to face the survivor is
    /// what takes the 1.5x away from it.
    /// </summary>
    [Fact]
    public void KillingTheRocketTurnsThePlayerAndEndsTheBonus()
    {
        var fight = Crab();
        fight.State.PlayerHp = 9999;
        var rocket = fight.State.Enemies[1];

        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        rocket.Hp = 1;
        rocket.Block = 0;
        fight.Play(0, target: 1);

        Assert.Equal(0, rocket.Hp);
        Assert.Equal(
            RunConstants.FacingLeft,
            BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Surrounded)
        );

        // Facing Left, the bonus belongs to BackAttackRight -- which is the dead Rocket.
        var crusher = fight.State.Enemies[0];
        crusher.Hp = 9999;
        crusher.MoveIndex = 0;
        // Crab Rage's Strength 6 lands on the same death and would otherwise be read as
        // the multiplier still applying: 12 + 6 is 18, and so is 12 x 1.5. Cleared here
        // so this test measures ONE thing.
        BuffSystem.Remove(crusher.Buffs, BuffId.Strength);
        crusher.CurrentIntent = new Intent(IntentType.Attack, 12);
        fight.State.PlayerHp = 500;
        fight.State.PlayerBlock = 0;
        fight.EndTurn();

        Assert.Equal(500 - 12, fight.State.PlayerHp);
    }

    /// <summary>
    /// CrabRagePower: the survivor takes Strength 6 and 99 block when its partner dies,
    /// so halving the boss is not free.
    /// </summary>
    [Fact]
    public void KillingOneHalfEnragesTheOther()
    {
        var fight = Crab();
        fight.State.PlayerHp = 9999;
        var crusher = fight.State.Enemies[0];
        var rocket = fight.State.Enemies[1];

        Assert.Equal(0, BuffSystem.Get(crusher.Buffs, BuffId.Strength));

        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(472, Upgraded: false));
        fight.State.Energy = 5;
        rocket.Hp = 1;
        rocket.Block = 0;
        fight.Play(0, target: 1);

        Assert.Equal(RunConstants.CrabRageStrength, BuffSystem.Get(crusher.Buffs, BuffId.Strength));
        Assert.Equal(RunConstants.CrabRageBlock, crusher.Block);
    }
}

public class KnowledgeDemonTests
{
    /// <summary>
    /// CURSE -> SLAP -> KNOWLEDGE_OVERWHELMING -> PONDER, and PONDER branches back to
    /// CURSE while it has cast fewer than three. A bare `MoveIndex switch` with no wrap
    /// left it PONDERING every turn from the fourth on.
    /// </summary>
    [Fact]
    public void ItCyclesRatherThanPonderingForever()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.KnowledgeDemon);
        var demon = fight.State.Enemies[0];
        var types = Boss.Cycle(fight, demon, 12).Select(i => i.Item1).ToList();

        // PONDER declares its SingleAttackIntent first, so it READS as an attack -- the
        // heal and the Strength ride it. A live capture shows (Attack, 11).
        Assert.Equal(
            [
                IntentType.Debuff,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Debuff,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Debuff,
                IntentType.Attack,
                IntentType.Attack,
                IntentType.Attack,
            ],
            types
        );
    }

    /// <summary>
    /// Three curses land, on moves 0, 4 and 8; after that the branch sends it to SLAP
    /// instead and the fight is a three-cycle.
    /// </summary>
    [Fact]
    public void AfterThreeCursesItNeverCursesAgain()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.KnowledgeDemon);
        var demon = fight.State.Enemies[0];
        var types = Boss.Cycle(fight, demon, 24).Select(i => i.Item1).ToList();

        Assert.Equal(3, types.Count(t => t == IntentType.Debuff));
        Assert.DoesNotContain(IntentType.Debuff, types.Skip(12));
    }

    /// <summary>KNOWLEDGE_OVERWHELMING is MultiAttackIntent(damage, 3), folded into 27.</summary>
    [Theory]
    [InlineData(8, 17, 8, 11)]
    [InlineData(9, 18, 9, 13)]
    public void ItsNumbersReadTheRightAscensionBranch(
        int ascension,
        int slap,
        int overwhelming,
        int ponder
    )
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.KnowledgeDemon, ascension);
        var seen = Boss.Cycle(fight, fight.State.Enemies[0], 4);

        Assert.Equal((IntentType.Attack, slap, 1), seen[1]);
        Assert.Equal((IntentType.Attack, overwhelming, 3), seen[2]);
        Assert.Equal((IntentType.Attack, ponder, 1), seen[3]);
    }
}

public class TheInsatiableTests
{
    /// <summary>
    /// LIQUIFY once, then THRASH -> BITE -> SALIVATE -> THRASH_2, cycling. THRASH_2 is
    /// the same move in a second state, which is what gives the cycle its two thrashes;
    /// `_ => thrash` left it thrashing forever from the fifth turn on.
    /// </summary>
    [Theory]
    [InlineData(8, 8, 28, 2)]
    [InlineData(9, 9, 31, 3)]
    public void ItLiquifiesThenRunsAFourMoveCycle(int ascension, int thrash, int bite, int salivate)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.TheInsatiable, ascension);

        Assert.Equal(
            [
                (IntentType.Buff, 0, 1),
                // THRASH: MultiAttackIntent(ThrashDamage, 2), never a folded 18.
                (IntentType.Attack, thrash, 2),
                (IntentType.Attack, bite, 1),
                (IntentType.Buff, salivate, 1),
                // THRASH_2, whose FollowUpState is THRASH -- so the cycle really does
                // put two thrashes back to back, and a reading that "corrects" them to
                // alternate is the wrong one.
                (IntentType.Attack, thrash, 2),
                (IntentType.Attack, thrash, 2),
                (IntentType.Attack, bite, 1),
                (IntentType.Buff, salivate, 1),
            ],
            Boss.Cycle(fight, fight.State.Enemies[0], 8)
        );
    }
}
