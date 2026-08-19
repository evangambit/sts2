using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics that fire once as the combat opens, each read off
/// MegaCrit.Sts2.Core.Models.Relics: Vajra PowerVar&lt;StrengthPower&gt;(1m), Oddly Smooth
/// Stone PowerVar&lt;DexterityPower&gt;(1m), Bronze Scales PowerVar&lt;ThornsPower&gt;(3m),
/// Blood Vial HealVar(2m), Bag of Preparation CardsVar(2).
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
