using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/SicEm.cs: OstyDamageVar(5m) upgrading by 1, then
// SicEmPower at 3 upgrading by 1 ON THE TARGET.
//
// SicEmPower.AfterDamageGiven fires when the dealer is OSTY and the target carries the
// debuff, calling OstyCmd.Summon for its amount — which on a living pet is GainMaxHp, so
// each later Osty attack into that enemy GROWS Osty. The emulator used to give the player
// Strength: wrong target, wrong effect, wrong number.
public class SicEmTests
{
    private const int SicEm = 434;
    private const int Poke = 357;

    private static Fight WithOsty(int ostyHp = 10)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);
        CardEffects.SummonOsty(fight.State, ostyHp);
        return fight;
    }

    [Fact]
    public void ItPutsTheDebuffOnTheEnemyAndNoStrengthOnThePlayer()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(SicEm, false));

        fight.Play(0);

        Assert.Equal(3, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SicEm));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>The debuff is applied AFTER the damage, so Sic Em's own hit does not feed it.</summary>
    [Fact]
    public void ItsOwnHitDoesNotGrowOsty()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(SicEm, false));
        int before = fight.State.OstyMaxHp;

        fight.Play(0);

        Assert.Equal(before, fight.State.OstyMaxHp);
    }

    /// <summary>A later Osty attack into that enemy summons for the debuff's amount.</summary>
    [Fact]
    public void ALaterOstyAttackGrowsOsty()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(SicEm, false));
        fight.Play(0);
        int afterSicEm = fight.State.OstyMaxHp;

        fight.State.Hand.Add(new CardInstance(Poke, false));
        fight.Play(0);

        Assert.Equal(afterSicEm + 3, fight.State.OstyMaxHp);
    }

    /// <summary>A dead Osty swings at nothing, so nothing triggers.</summary>
    [Fact]
    public void WithNoOstyTheAttackDoesNothing()
    {
        var fight = Fight.Hand(new CardInstance(SicEm, false)).Energy(9).Enemy(hp: 200);
        fight.State.OstyHp = 0;
        fight.State.OstyMaxHp = 0;

        fight.Play(0);

        Assert.Equal(200, fight.Enemy0.Hp);
    }

    /// <summary>`AfterSideTurnEnd` removes it when the enemy's turn ends.</summary>
    [Fact]
    public void ItLapsesAfterTheEnemyTurn()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(SicEm, false));
        fight.Play(0);
        Assert.Equal(3, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SicEm));

        fight.State.Hand.Clear();
        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SicEm));
    }

    [Fact]
    public void UpgradedItIsFourAndSixDamage()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(SicEm, true));

        fight.Play(0);

        Assert.Equal(4, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.SicEm));
        Assert.Equal(194, fight.Enemy0.Hp);
    }
}
