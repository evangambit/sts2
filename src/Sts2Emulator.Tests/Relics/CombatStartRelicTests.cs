using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics that fire once as the combat opens, each read off
/// MegaCrit.Sts2.Core.Models.Relics: Vajra PowerVar&lt;StrengthPower&gt;(1m), Oddly Smooth
/// Stone PowerVar&lt;DexterityPower&gt;(1m), Bronze Scales PowerVar&lt;ThornsPower&gt;(3m),
/// Blood Vial HealVar(2m), Bag of Preparation CardsVar(2), Data Disk
/// PowerVar&lt;FocusPower&gt;(1m), Gorget PowerVar&lt;PlatingPower&gt;(4m), Akabeko
/// PowerVar&lt;VigorPower&gt;(8m), Red Mask PowerVar&lt;WeakPower&gt;(1m), Festive Popper
/// DamageVar(9m, ValueProp.Unpowered).
/// </summary>
public class CombatStartRelicTests
{
    [Fact]
    public void VajraGrantsOneStrength()
    {
        var fight = Fight.WithRelics(RelicEffects.Vajra);

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void OddlySmoothStoneGrantsOneDexterity()
    {
        var fight = Fight.WithRelics(RelicEffects.OddlySmoothStone);

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void BronzeScalesGrantsThreeThorns()
    {
        var fight = Fight.WithRelics(RelicEffects.BronzeScales);

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Thorns));
    }

    [Fact]
    public void BloodVialHealsTwo()
    {
        var plain = Fight.WithRelics();
        var withVial = Fight.WithRelics(RelicEffects.BloodVial);

        Assert.Equal(plain.State.PlayerHp + 2, withVial.State.PlayerHp);
    }

    [Fact]
    public void BagOfPreparationDrawsTwoExtraCards()
    {
        var plain = Fight.WithRelics();
        var withBag = Fight.WithRelics(RelicEffects.BagOfPreparation);

        Assert.Equal(plain.State.Hand.Count + 2, withBag.State.Hand.Count);
    }

    [Fact]
    public void DataDiskGrantsOneFocus()
    {
        var fight = Fight.WithRelics(RelicEffects.DataDisk);

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Focus));
    }

    [Fact]
    public void GorgetGrantsFourPlating()
    {
        var fight = Fight.WithRelics(RelicEffects.Gorget);

        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.Plating));
    }

    [Fact]
    public void AkabekoGrantsEightVigor()
    {
        var fight = Fight.WithRelics(RelicEffects.Akabeko);

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Vigor));
    }

    /// <summary>
    /// Vigor is spent by the first Attack, so Akabeko's real effect is a one-off +8 on it
    /// rather than a standing buff.
    /// </summary>
    [Fact]
    public void AkabekosVigorLandsOnTheFirstAttackOnly()
    {
        var plain = Fight.WithRelics().Energy(20);
        var withAkabeko = Fight.WithRelics(RelicEffects.Akabeko).Energy(20);
        foreach (var fight in new[] { plain, withAkabeko })
        {
            fight.State.Hand = TestDeck.Pile(IC.StrikeIronclad, IC.StrikeIronclad);
        }

        Assert.Equal(FirstEnemyDamage(plain) + 8, FirstEnemyDamage(withAkabeko));
        Assert.Equal(FirstEnemyDamage(plain), FirstEnemyDamage(withAkabeko));
    }

    /// <summary>Plays the top card and reports what the first enemy lost.</summary>
    private static int FirstEnemyDamage(Fight fight)
    {
        int before = fight.State.Enemies[0].Hp;
        fight.Play();
        return before - fight.State.Enemies[0].Hp;
    }

    /// <summary>
    /// Fought in encounter 3 rather than the default: encounter 1's enemies both hold
    /// Artifact, so every enemy there absorbs the Weak and the assertion says nothing.
    /// </summary>
    [Fact]
    public void RedMaskWeakensEveryEnemy()
    {
        var fight = Fight.Encounter(3, RelicEffects.RedMask);

        Assert.NotEmpty(fight.State.Enemies);
        Assert.All(
            fight.State.Enemies,
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Weak))
        );
    }

    [Fact]
    public void FestivePopperHitsEveryEnemyForNine()
    {
        var plain = Fight.WithRelics();
        var withPopper = Fight.WithRelics(RelicEffects.FestivePopper);

        Assert.Equal(
            plain.State.Enemies.Select(enemy => enemy.Hp - 9),
            withPopper.State.Enemies.Select(enemy => enemy.Hp)
        );
    }

    /// <summary>DamageVar(9m, ValueProp.Unpowered): a Strength relic must not raise it.</summary>
    [Fact]
    public void FestivePoppersDamageIgnoresStrength()
    {
        var plain = Fight.WithRelics(RelicEffects.Vajra);
        var withPopper = Fight.WithRelics(RelicEffects.Vajra, RelicEffects.FestivePopper);

        Assert.Equal(
            plain.State.Enemies.Select(enemy => enemy.Hp - 9),
            withPopper.State.Enemies.Select(enemy => enemy.Hp)
        );
    }

    [Fact]
    public void RelicsStackWithoutInterferingWithEachOther()
    {
        var fight = Fight.WithRelics(
            RelicEffects.Vajra,
            RelicEffects.OddlySmoothStone,
            RelicEffects.BronzeScales
        );

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Thorns));
    }
}
