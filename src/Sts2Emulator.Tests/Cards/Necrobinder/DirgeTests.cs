using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Dirge.cs: HasEnergyCostX, SummonVar(3) upgrading by 1.
// The X is the LOOP COUNT — `for (i < xValue) OstyCmd.Summon(Summon.BaseValue)` and then
// `Soul.Create(owner, xValue, ...)`. The upgrade raises the SUMMON AMOUNT, not the count.
//
// The emulator summoned once, for `upgraded ? 4 : 3`, and made that many Souls — reading
// the summon var as the count. At nine energy the live game summoned nine times.
public class DirgeTests
{
    private const int Dirge = 145;
    private const int Soul = 446;

    private static Fight WithEnergy(int energy) => Fight.Hand().Energy(energy).Enemy(hp: 200);

    [Fact]
    public void ItSummonsOncePerEnergySpent()
    {
        var fight = WithEnergy(4);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        // 1 from the seed, then four summons of 3.
        Assert.Equal(13, fight.State.OstyMaxHp);
        Assert.Equal(0, fight.State.Energy);
    }

    /// <summary>The upgrade raises the summon amount; the count is still the energy.</summary>
    [Fact]
    public void UpgradeRaisesTheSummonAmountNotTheCount()
    {
        var fight = WithEnergy(4);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Dirge, true));

        fight.Play(0);

        Assert.Equal(17, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ItMakesOneSoulPerEnergySpentInTheDrawPile()
    {
        var fight = WithEnergy(5);
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        Assert.Equal(5, fight.State.DrawPile.Count(card => card.DefId == Soul));
        Assert.DoesNotContain(fight.State.Hand, card => card.DefId == Soul);
    }

    [Fact]
    public void AnUpgradedDirgeMakesUpgradedSouls()
    {
        var fight = WithEnergy(2);
        fight.State.Hand.Add(new CardInstance(Dirge, true));

        fight.Play(0);

        Assert.All(
            fight.State.DrawPile.Where(card => card.DefId == Soul),
            soul => Assert.True(soul.Upgraded)
        );
    }

    /// <summary>Nothing to spend is nothing to do — not one free summon.</summary>
    [Fact]
    public void AtZeroEnergyItDoesNothing()
    {
        var fight = WithEnergy(0);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        Assert.Equal(1, fight.State.OstyMaxHp);
        Assert.DoesNotContain(fight.State.DrawPile, card => card.DefId == Soul);
    }
}
