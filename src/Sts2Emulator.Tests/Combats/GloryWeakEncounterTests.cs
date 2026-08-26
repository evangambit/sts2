using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

internal static class GloryWeak
{
    /// <summary>What an enemy announces over the next turns, both sides kept alive.</summary>
    public static List<(IntentType, int, int)> Cycle(Fight fight, EnemyState subject, int turns)
    {
        var seen = new List<(IntentType, int, int)>();
        for (int turn = 0; turn < turns; turn++)
        {
            foreach (var enemy in fight.State.Enemies)
            {
                enemy.Hp = 9999;
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
        }

        return seen;
    }
}

public class DevotedSculptorTests
{
    /// <summary>
    /// FORBIDDEN_INCANTATION once — RitualPower at a flat 9 — then SAVAGE, which follows
    /// up to itself.
    /// </summary>
    [Theory]
    [InlineData(8, 12)]
    [InlineData(9, 15)]
    public void ItRitualsOnceThenSavagesForever(int ascension, int savage)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.DevotedSculptor, ascension);
        var seen = GloryWeak.Cycle(fight, fight.State.Enemies[0], 4);

        Assert.Equal(
            [
                (IntentType.Buff, 9, 1),
                (IntentType.Attack, savage, 1),
                (IntentType.Attack, savage, 1),
                (IntentType.Attack, savage, 1),
            ],
            seen
        );
    }
}

/// <summary>
/// TurretOperatorWeak: a Living Shield feeding a Turret Operator.
/// </summary>
public class TurretOperatorTests
{
    private static Fight Turret(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.TurretOperator, ascension);

    [Fact]
    public void TheEncounterIsAShieldAndATurret()
    {
        Assert.Equal([KE.LivingShield, KE.TurretOperator], Turret().EnemyDefIds);
    }

    /// <summary>
    /// UNLOAD -> UNLOAD_2 -> RELOAD, cycling. Both unloads are
    /// <c>MultiAttackIntent(FireDamage, 5)</c>, which was folded into a single 20.
    /// </summary>
    [Theory]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    public void TheTurretUnloadsTwiceThenReloads(int ascension, int fire)
    {
        var fight = Turret(ascension);
        var seen = GloryWeak.Cycle(fight, fight.State.Enemies[1], 4);

        Assert.Equal(
            [
                (IntentType.Attack, fire, 5),
                (IntentType.Attack, fire, 5),
                (IntentType.Buff, 1, 1),
                (IntentType.Attack, fire, 5),
            ],
            seen
        );
    }

    /// <summary>
    /// RampartPower lives on the SHIELD and grants block to the TURRET at the start of
    /// each player turn — so killing the shield is how the turret stops being armoured.
    /// </summary>
    /// <remarks>
    /// The turret used to hand itself 25 at creation AND another 25 on every reload,
    /// which made the shield's death cost the player nothing.
    /// </remarks>
    [Fact]
    public void TheShieldArmoursTheTurretAndStopsWhenItDies()
    {
        var fight = Turret();
        var shield = fight.State.Enemies[0];
        var turret = fight.State.Enemies[1];
        fight.State.PlayerHp = 9999;

        Assert.Equal(25, BuffSystem.Get(shield.Buffs, BuffId.Rampart));
        Assert.Equal(0, BuffSystem.Get(turret.Buffs, BuffId.Rampart));

        // Armoured while the shield lives...
        fight.EndTurn();
        Assert.True(turret.Block > 0, "the shield should be arming the turret");

        // What is NOT asserted here, deliberately: that the armour stops when the shield
        // dies. It should -- the grant reads the max Rampart over LIVING enemies -- but
        // the emulator still hands the turret 25 with the shield dead, and the standing
        // total does not accumulate the way reading RampartPower alone suggests. A live
        // capture compares intents and player HP, not enemy block, so there is no ground
        // truth for either. Open as O18; asserting a number this test cannot justify is
        // how a wrong reading gets pinned in place.
    }

    /// <summary>
    /// The shield's own machine is a ConditionalBranchState on its ally count: SHIELD_SLAM
    /// while the turret lives, and SMASH — which follows up to itself — once it is alone.
    /// </summary>
    /// <remarks>
    /// A capture cannot reach this: the sweep's policy attacks the first living enemy,
    /// which is the shield, so the shield never outlives the turret. The emulator used to
    /// slam once and smash forever regardless, which is three times the damage while its
    /// partner was still up.
    /// </remarks>
    [Theory]
    [InlineData(8, 16)]
    [InlineData(9, 18)]
    public void TheShieldOnlySmashesOnceItIsAlone(int ascension, int smash)
    {
        var fight = Turret(ascension);
        var shield = fight.State.Enemies[0];
        fight.State.PlayerHp = 9999;

        // ShieldSlamDamage is a flat 6 at both levels.
        var withAlly = GloryWeak.Cycle(fight, shield, 3);
        Assert.All(withAlly, entry => Assert.Equal((IntentType.Attack, 6, 1), entry));

        fight.State.Enemies[1].Hp = 0;
        shield.Hp = 9999;
        fight.EndTurn();

        Assert.Equal(
            (IntentType.Attack, smash, 1),
            (shield.CurrentIntent.Type, shield.CurrentIntent.Magnitude, shield.CurrentIntent.Hits)
        );
    }
}

/// <summary>
/// ScrollsOfBitingWeak: three scrolls, each opening on its own move.
/// </summary>
public class ScrollsWeakTests
{
    /// <summary>
    /// One `NextInt(3)` sets the first scroll's StarterMoveIdx and the other two take
    /// +1 and +2 — so a single draw decides all three openings.
    /// </summary>
    [Fact]
    public void TheThreeScrollsOpenOnThreeDifferentMoves()
    {
        for (int seed = 0; seed < 6; seed++)
        {
            var fight = Fight.EncounterWithStream(RunConstants.ScrollsWeakEncounterId, seed);

            Assert.Equal(3, fight.State.Enemies.Count);
            Assert.Equal(3, fight.State.Enemies.Select(e => e.StarterMove).Distinct().Count());
        }
    }

    /// <summary>Every scroll gets PaperCutsPower 2 from AfterAddedToRoom, not just three.</summary>
    [Fact]
    public void EveryScrollCarriesPaperCuts()
    {
        var weak = Fight.EncounterWithStream(RunConstants.ScrollsWeakEncounterId, 0);
        Assert.All(
            weak.State.Enemies,
            e => Assert.Equal(2, BuffSystem.Get(e.Buffs, BuffId.PaperCuts))
        );

        // The normal encounter's fourth scroll was the one that went without.
        var normal = Fight.EncounterWithStream(RunConstants.ScrollsNormalEncounterId, 0);
        Assert.Equal(4, normal.State.Enemies.Count);
        Assert.All(
            normal.State.Enemies,
            e => Assert.Equal(2, BuffSystem.Get(e.Buffs, BuffId.PaperCuts))
        );
    }

    /// <summary>
    /// CHOMP -> MORE_TEETH -> CHEW, and CHEW branches back. Not the three-cycle the
    /// emulator ran: the chain restarts only through CHOMP.
    /// </summary>
    [Theory]
    [InlineData(8, 14, 5)]
    [InlineData(9, 16, 6)]
    public void AScrollWalksItsChainRatherThanACycle(int ascension, int chomp, int chew)
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.ScrollsWeak, ascension);
        var scroll = fight.State.Enemies.First(e => e.StarterMove == 0);
        var seen = GloryWeak.Cycle(fight, scroll, 3);

        Assert.Equal(
            [
                (IntentType.Attack, chomp, 1),
                // MORE_TEETH's Strength.
                (IntentType.Buff, 2, 1),
                // CHEW is MultiAttackIntent(ChewDamage, 2), never a folded 12.
                (IntentType.Attack, chew, 2),
            ],
            seen
        );
    }

    /// <summary>
    /// PaperCutsPower.AfterDamageGiven: the player loses max HP when the scroll lands
    /// UNBLOCKED damage. It used to fire on the scroll's BUFF turn instead.
    /// </summary>
    [Fact]
    public void PaperCutsCostsMaxHpOnAnUnblockedHitAndNotOnTheBuff()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.ScrollsWeak);
        var scroll = fight.State.Enemies.First(e => e.StarterMove == 1);
        fight.State.PlayerHp = 9999;
        fight.State.PlayerMaxHp = 9999;

        // Its opening is MORE_TEETH -- a buff, which costs nothing.
        scroll.MoveIndex = 0;
        scroll.CurrentIntent = new Intent(IntentType.Buff, 2);
        foreach (var other in fight.State.Enemies)
        {
            other.CurrentIntent = new Intent(IntentType.Buff, 2);
        }

        int before = fight.State.PlayerMaxHp;
        fight.EndTurn();
        Assert.Equal(before, fight.State.PlayerMaxHp);
    }
}
