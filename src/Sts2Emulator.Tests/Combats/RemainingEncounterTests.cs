using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// TheLostAndForgottenNormal: The Lost and The Forgotten, side by side.
/// </summary>
public class LostAndForgottenTests
{
    private static Fight Pair(int ascension = 8) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.LostAndForgotten, ascension);

    [Fact]
    public void TheEncounterIsBothOfThem()
    {
        Assert.Equal([KE.TheLost, KE.TheForgotten], Pair().EnemyDefIds);
    }

    /// <summary>
    /// The Lost alternates DEBILITATING_SMOG and EYE_LASERS, which is
    /// <c>MultiAttackIntent(EyeLasersDamage, 2)</c> — folded into a single 10 before, at
    /// the A9 damage besides. The smog takes the player's Strength and keeps it, so the
    /// lasers climb by two every other turn.
    /// </summary>
    [Theory]
    [InlineData(8, 4)]
    [InlineData(9, 5)]
    public void TheLostSmogsThenLasersTwice(int ascension, int laser)
    {
        var fight = Pair(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[0], 4);

        Assert.Equal(
            [
                (IntentType.Debuff, 2, 1),
                (IntentType.Attack, (laser + 2) * 2, 2),
                (IntentType.Debuff, 2, 1),
                (IntentType.Attack, (laser + 4) * 2, 2),
            ],
            seen
        );
    }

    /// <summary>
    /// <c>DreadDamage</c> is not a constant: it is
    /// <c>GetValueIfAscension(Deadly, 15, 13) + its own DexterityPower</c>, and MIASMA
    /// hands it two Dexterity every other turn — so the dread CLIMBS. The emulator
    /// announced a flat 15, which is right for exactly one turn at A8 by the coincidence
    /// of 13 + 2, and wrong from the second dread on.
    /// </summary>
    [Theory]
    [InlineData(8, 13)]
    [InlineData(9, 15)]
    public void TheForgottensDreadClimbsWithTheDexterityItSteals(int ascension, int dread)
    {
        var fight = Pair(ascension);
        var seen = GloryNormal.Cycle(fight, fight.State.Enemies[1], 4);

        Assert.Equal(
            [
                (IntentType.Debuff, 2, 1),
                (IntentType.Attack, dread + 2, 1),
                (IntentType.Debuff, 2, 1),
                (IntentType.Attack, dread + 4, 1),
            ],
            seen
        );
    }

    /// <summary>MIASMA also gains 8 block, which its DefendIntent stands for.</summary>
    [Fact]
    public void TheMiasmaBlocksAndStealsDexterity()
    {
        var fight = Pair();
        var forgotten = fight.State.Enemies[1];
        fight.State.PlayerHp = 9999;

        fight.EndTurn(); // MIASMA

        Assert.Equal(8, forgotten.Block);
        Assert.Equal(-2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));
        Assert.Equal(2, BuffSystem.Get(forgotten.Buffs, BuffId.Dexterity));
    }

    /// <summary>
    /// <c>PossessSpeedPower.AfterDeath</c> returns every point of Dexterity The Forgotten
    /// took. The emulator kept none of that tally, so killing it was worth nothing and the
    /// debuff outlasted the creature that applied it.
    /// </summary>
    [Fact]
    public void KillingTheForgottenGivesTheDexterityBack()
    {
        var fight = Pair();
        var forgotten = fight.State.Enemies[1];
        fight.State.PlayerHp = 9999;

        fight.Turns(3); // two miasmas, so four Dexterity is gone
        Assert.Equal(-4, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));

        forgotten.Hp = 1;
        forgotten.Block = 0;
        fight.State.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        fight.State.Energy = 3;
        fight.Play(0, target: 1);

        Assert.Equal(0, forgotten.Hp);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));
    }
}

/// <summary>
/// ScrollsOfBitingNormal: four Scrolls of Biting, where the weak variant has three.
/// </summary>
public class ScrollsTests
{
    private static Fight Four(int seed = 0) =>
        Fight.EncounterWithStream((int)CombatFactory.ActOneEncounter.Scrolls, seed);

    /// <summary>
    /// The first three take consecutive starter indices off one roll; the FOURTH is pinned
    /// at 2 and costs no draw — <c>scrollOfBiting4.StarterMoveIdx = 2</c>, a literal, where
    /// the others are `(num2 + n) % 3`.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void TheFourthScrollIsPinnedWhileTheOthersFollowTheRoll(int seed)
    {
        var fight = Four(seed);
        var starters = fight.State.Enemies.Select(e => e.StarterMove).ToList();

        Assert.Equal(4, starters.Count);
        Assert.All(fight.State.Enemies, e => Assert.Equal(KE.ScrollOfBiting, e.DefId));
        Assert.Equal((starters[0] + 1) % 3, starters[1]);
        Assert.Equal((starters[0] + 2) % 3, starters[2]);
        Assert.Equal(2, starters[3]);
    }

    /// <summary>
    /// <c>AfterAddedToRoom</c> gives PaperCutsPower to EVERY scroll. The emulator gave it
    /// to the first three, which left the normal encounter's fourth without it — a defect
    /// only the four-scroll variant could show.
    /// </summary>
    [Fact]
    public void EveryScrollCarriesPaperCuts()
    {
        Assert.All(
            Four().State.Enemies,
            e => Assert.Equal(2, BuffSystem.Get(e.Buffs, BuffId.PaperCuts))
        );
    }
}

/// <summary>
/// The four punching bags: <c>TheArchitectEventEncounter</c> and the Battleworn Dummy's
/// three settings. Each is one creature whose whole machine is a NOTHING move that follows
/// up to itself, so what a test can say about them is that they never do anything — which
/// is worth saying, because an enemy that silently acquired an intent would be a live bug
/// in a room the player is meant to be safe in.
/// </summary>
internal static class PunchingBag
{
    public static void StandsThereAndNeverActs(
        CombatFactory.ActOneEncounter encounter,
        int defId,
        int hp
    )
    {
        var fight = Fight.Encounter(encounter);
        var bag = Assert.Single(fight.State.Enemies);

        Assert.Equal(defId, bag.DefId);
        Assert.Equal(hp, bag.MaxHp);
        Assert.Equal(hp, bag.Hp);

        for (int turn = 0; turn < 5; turn++)
        {
            fight.EndTurn();
            Assert.Equal(IntentType.Unknown, bag.CurrentIntent.Type);
            Assert.Equal(0, bag.Block);
            Assert.Empty(bag.Buffs);
        }

        // And it is still whole: nothing about ending five turns beside it hurt it, and
        // nothing it did hurt the player -- which is the entire contract of a dummy.
        Assert.Equal(hp, bag.Hp);
    }
}

/// <summary>
/// TheArchitectEventEncounter. `RunManager` enters it directly at an act boundary; it is
/// in no act's event pool, which is why placing it took reading the entry rather than the
/// pools.
/// </summary>
public class ArchitectTests
{
    [Fact]
    public void TheArchitectNeverActs() =>
        PunchingBag.StandsThereAndNeverActs(
            CombatFactory.ActOneEncounter.Architect,
            KE.Architect,
            9999
        );
}

public class BattlewornDummy1Tests
{
    [Fact]
    public void TheFirstSettingNeverActs() =>
        PunchingBag.StandsThereAndNeverActs(
            CombatFactory.ActOneEncounter.BattlewornDummy1,
            KE.BattleFriendV1,
            75
        );
}

public class BattlewornDummy2Tests
{
    [Fact]
    public void TheSecondSettingNeverActs() =>
        PunchingBag.StandsThereAndNeverActs(
            CombatFactory.ActOneEncounter.BattlewornDummy2,
            KE.BattleFriendV2,
            150
        );
}

public class BattlewornDummy3Tests
{
    [Fact]
    public void TheThirdSettingNeverActs() =>
        PunchingBag.StandsThereAndNeverActs(
            CombatFactory.ActOneEncounter.BattlewornDummy3,
            KE.BattleFriendV3,
            300
        );
}
