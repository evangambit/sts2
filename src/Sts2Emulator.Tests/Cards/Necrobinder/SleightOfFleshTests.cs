using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/SleightOfFlesh.cs: a 2-cost Power applying
// SleightOfFleshPower at 9, upgrading by 4.
//
// SleightOfFleshPower.AfterPowerAmountChanged: when a non-zero, non-temporary DEBUFF's
// amount changes on an ENEMY and the applier is this power's owner, that enemy takes the
// power's amount in Unpowered damage. A debuff engine, not a stat — and the emulator had
// the card sharing High Five's Osty-attack body, which is a different card entirely.
public class SleightOfFleshTests
{
    private const int SleightOfFlesh = 438;
    private const int Uppercut = 529; // 13 damage, then Weak 1 and Vulnerable 1
    private const int FightMe = 189; // gives the ENEMY Strength — a buff, not a debuff
    private const int Poke = 357;

    private static Fight Armed(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SleightOfFlesh, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAppliesNineAndThirteenUpgraded()
    {
        Assert.Equal(9, BuffSystem.Get(Armed().State.PlayerBuffs, BuffId.SleightOfFlesh));
        Assert.Equal(13, BuffSystem.Get(Armed(true).State.PlayerBuffs, BuffId.SleightOfFlesh));
    }

    /// <summary>Once per debuff LANDED, so Uppercut's Weak and Vulnerable pay twice.</summary>
    [Fact]
    public void EachDebuffLandedDamagesTheEnemy()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Uppercut, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 13 - 9 - 9, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradedAmountIsWhatIsDealt()
    {
        var fight = Armed(upgraded: true);
        fight.State.Hand.Add(new CardInstance(Uppercut, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 13 - 13 - 13, fight.Enemy0.Hp);
    }

    /// <summary>A BUFF on an enemy is not a debuff: Fight Me's gift of Strength pays nothing.</summary>
    [Fact]
    public void ABuffGivenToAnEnemyDoesNotTrigger()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(FightMe, false));
        control.Play(0, target: 0);
        int plain = 500 - control.Enemy0.Hp;

        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(FightMe, false));
        fight.Play(0, target: 0);

        Assert.Equal(500 - plain, fight.Enemy0.Hp);
    }

    [Fact]
    public void AnAttackThatLandsNoDebuffPaysNothing()
    {
        var fight = Armed();
        fight.State.Hand.Add(new CardInstance(Poke, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
    }

    /// <summary>Without the power the same play does its printed damage and no more.</summary>
    [Fact]
    public void WithoutThePowerNothingIsAdded()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Uppercut, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 13, fight.Enemy0.Hp);
    }
}
