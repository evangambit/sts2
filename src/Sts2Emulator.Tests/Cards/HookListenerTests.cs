using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// The last two listeners on the hooks swept in E216-E224. Both cards were labels in
// merged `case` groups doing something unrelated, and both powers did not exist.

public class OrbitTests
{
    private const int OrbitId = 335;

    /// <summary>It was falling through to a Focus body — a Defect stat, on a Regent card.</summary>
    [Fact]
    public void PlayingItAppliesThePowerAndNotFocus()
    {
        var fight = Fight.Hand(new CardInstance(OrbitId, false)).Energy(3);

        fight.Play(0);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Orbit));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Focus));
    }

    /// <summary>Every fourth energy spent pays one back.</summary>
    [Fact]
    public void EveryFourthEnergySpentPaysOneBack()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.Orbit, 1);
        fight.State.Energy = 10;

        CardEffects.ApplyAfterEnergySpent(fight.State, 3);
        Assert.Equal(10, fight.State.Energy);

        // The fourth crosses the line.
        CardEffects.ApplyAfterEnergySpent(fight.State, 1);
        Assert.Equal(11, fight.State.Energy);
    }

    /// <summary>
    /// The count is cumulative over the COMBAT, not per turn: three spent one turn and one
    /// the next still crosses four.
    /// </summary>
    [Fact]
    public void TheCountCarriesAcrossTurns()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.Orbit, 1);
        CardEffects.ApplyAfterEnergySpent(fight.State, 3);
        fight.EndTurn();

        int before = fight.State.Energy;
        CardEffects.ApplyAfterEnergySpent(fight.State, 1);

        Assert.Equal(before + 1, fight.State.Energy);
    }

    /// <summary>A single spend that crosses several multiples pays for each of them.</summary>
    [Fact]
    public void OneBigSpendPaysForEveryMultipleItCrosses()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.Orbit, 1);
        fight.State.Energy = 0;

        CardEffects.ApplyAfterEnergySpent(fight.State, 9);

        Assert.Equal(2, fight.State.Energy);
    }

    /// <summary>Energy spent BEFORE the power landed does not count towards it.</summary>
    [Fact]
    public void SpendingBeforeItLandedDoesNotCount()
    {
        var fight = Fight.Hand().Energy(10);

        CardEffects.ApplyAfterEnergySpent(fight.State, 3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Orbit, 1);
        CardEffects.ApplyAfterEnergySpent(fight.State, 1);

        Assert.Equal(10, fight.State.Energy);
    }
}

public class NecroMasteryTests
{
    private const int NecroMasteryId = 319;

    [Fact]
    public void ItSummonsOstyAndAppliesThePower()
    {
        var fight = Fight.Hand(new CardInstance(NecroMasteryId, false)).Energy(3);

        fight.Play(0);

        Assert.Equal(5, fight.State.OstyHp);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NecroMastery));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void UpgradedItSummonsEight()
    {
        var fight = Fight.Hand(new CardInstance(NecroMasteryId, true)).Energy(3);

        fight.Play(0);

        Assert.Equal(8, fight.State.OstyHp);
    }

    /// <summary>Damage to Osty becomes damage to every enemy, at `-delta * Amount`.</summary>
    [Fact]
    public void OstysHpLossHitsEveryEnemy()
    {
        var fight = Fight.Hand().Enemy(hp: 40).Enemy(hp: 40).PlayerBuff(BuffId.NecroMastery, 2);
        CardEffects.SummonOsty(fight.State, 10);

        CardEffects.DamageOsty(fight.State, 3);

        Assert.Equal(7, fight.State.OstyHp);
        Assert.Equal(34, fight.Enemy0.Hp);
        Assert.Equal(34, fight.Enemy1.Hp);
    }

    /// <summary>
    /// It reflects the loss ACTUALLY taken, not the blow: a hit bigger than Osty's
    /// remaining HP reflects only what Osty had left.
    /// </summary>
    [Fact]
    public void ItReflectsWhatOstyActuallyLost()
    {
        var fight = Fight.Hand().Enemy(hp: 40).PlayerBuff(BuffId.NecroMastery, 1);
        CardEffects.SummonOsty(fight.State, 3);

        CardEffects.DamageOsty(fight.State, 50);

        Assert.Equal(0, fight.State.OstyHp);
        Assert.Equal(37, fight.Enemy0.Hp);
    }

    /// <summary>The reflection is Unpowered, so Strength stays out of it.</summary>
    [Fact]
    public void StrengthDoesNotRaiseTheReflection()
    {
        var fight = Fight.Hand().Enemy(hp: 40).PlayerBuff(BuffId.NecroMastery, 1);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 5);
        CardEffects.SummonOsty(fight.State, 10);

        CardEffects.DamageOsty(fight.State, 4);

        Assert.Equal(36, fight.Enemy0.Hp);
    }
}

/// <summary>
/// `DieForYouPower` making Osty a damage sink, which had to exist before NecroMastery
/// could. The nuance is `CreatureCmd`'s: on a redirect it deals the result's
/// OverkillDamage to the ORIGINAL target too, so Osty has a capacity rather than being
/// a blanket immunity.
/// </summary>
public class OstyDamageSinkTests
{
    private static Fight WithOsty(int ostyHp, int enemyDamage)
    {
        var fight = Fight.Hand().Enemy(hp: 60);
        fight.State.PlayerHp = 60;
        fight.State.PlayerMaxHp = 60;
        CardEffects.SummonOsty(fight.State, ostyHp);
        fight.State.Enemies[0].CurrentIntent = new Intent(IntentType.Attack, enemyDamage);
        return fight;
    }

    [Fact]
    public void OstyTakesTheUnblockedDamageInsteadOfThePlayer()
    {
        var fight = WithOsty(ostyHp: 20, enemyDamage: 8);

        fight.EndTurn();

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.True(fight.State.OstyHp < 20);
    }

    /// <summary>
    /// Overkill passes through. Osty absorbs what it has and the excess still lands on
    /// the player -- a reading where the pet swallows the whole blow would make a 1 HP
    /// Osty a free turn.
    /// </summary>
    [Fact]
    public void DamageBeyondOstysHpStillReachesThePlayer()
    {
        var fight = WithOsty(ostyHp: 3, enemyDamage: 10);

        fight.EndTurn();

        Assert.Equal(0, fight.State.OstyHp);
        Assert.True(
            fight.State.PlayerHp < 60,
            $"the excess should have reached the player, but HP is {fight.State.PlayerHp}"
        );
    }

    /// <summary>
    /// The player's BLOCK is spent first and is the player's own -- only what got through
    /// is redirected, so block still protects Osty.
    /// </summary>
    [Fact]
    public void BlockIsSpentBeforeTheRedirect()
    {
        var fight = WithOsty(ostyHp: 20, enemyDamage: 5);
        fight.State.PlayerBlock = 50;

        fight.EndTurn();

        Assert.Equal(60, fight.State.PlayerHp);
        Assert.Equal(20, fight.State.OstyHp);
    }

    /// <summary>
    /// `UnblockedDamageHitCount` is "times the PLAYER took unblocked damage", which Tear
    /// Asunder reads — a hit Osty ate is not one of them.
    /// </summary>
    [Fact]
    public void AHitOstyAteDoesNotCountAsOneThePlayerTook()
    {
        var fight = WithOsty(ostyHp: 30, enemyDamage: 6);

        fight.EndTurn();

        Assert.Equal(0, fight.State.UnblockedDamageHitCount);
    }

    [Fact]
    public void WithNoOstyThePlayerTakesItAsBefore()
    {
        var fight = Fight.Hand().Enemy(hp: 60);
        fight.State.PlayerHp = 60;
        fight.State.PlayerMaxHp = 60;
        fight.State.Enemies[0].CurrentIntent = new Intent(IntentType.Attack, 8);

        fight.EndTurn();

        Assert.True(fight.State.PlayerHp < 60);
        Assert.Equal(1, fight.State.UnblockedDamageHitCount);
    }
}
