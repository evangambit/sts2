using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Calcify.cs: PowerVar 4 upgrading by 2.
// `CalcifyPower.ModifyDamageAdditive` adds its amount only when `dealer?.Monster is Osty`
// and the owner is Osty's pet-owner — the player's own attacks get nothing. The emulator
// granted Plating, which is block on the player.
public class CalcifyTests
{
    private const int Calcify = 74;
    private const int Poke = 357; // OstyAttack, 5 damage
    private const int Strike = 473;

    private static Fight WithOsty()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        CardEffects.SummonOsty(fight.State, 20);
        return fight;
    }

    private static int DamageOf(Fight fight, int defId)
    {
        fight.State.Hand.Add(new CardInstance(defId, false));
        int before = fight.Enemy0.Hp;
        fight.Play(0, target: 0);
        return before - fight.Enemy0.Hp;
    }

    [Fact]
    public void ItAddsFourToAnOstyAttack()
    {
        int plain = DamageOf(WithOsty(), Poke);

        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Calcify, false));
        fight.Play(0);

        Assert.Equal(plain + 4, DamageOf(fight, Poke));
    }

    [Fact]
    public void TheUpgradeAddsSix()
    {
        int plain = DamageOf(WithOsty(), Poke);

        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Calcify, true));
        fight.Play(0);

        Assert.Equal(plain + 6, DamageOf(fight, Poke));
    }

    /// <summary>`dealer?.Monster is Osty`: the player's own attacks are untouched.</summary>
    [Fact]
    public void ThePlayersOwnAttacksGetNothing()
    {
        int plain = DamageOf(WithOsty(), Strike);

        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Calcify, false));
        fight.Play(0);

        Assert.Equal(plain, DamageOf(fight, Strike));
    }

    [Fact]
    public void ItGrantsNoPlating()
    {
        var fight = WithOsty();
        fight.State.Hand.Add(new CardInstance(Calcify, false));

        fight.Play(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Plating));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Debilitate.cs: 10/12 damage, then DebilitatePower 2/3 on
// the TARGET. The power doubles the two multipliers it touches — Vulnerable against it
// goes 1.5x to 2x (`amount + (amount - 1)`), Weak on its attacks goes 0.75x to 0.5x
// (`amount - (1 - amount)`) — and its amount is a DURATION, decremented at its owner's
// side-turn end. The emulator was taking temporary Strength off instead.
public class DebilitateTests
{
    private const int Debilitate = 127;
    private const int Strike = 473;

    private static Fight Fresh() => Fight.Hand().Energy(9).Enemy(hp: 500);

    [Fact]
    public void ItHitsForTenAndDebuffsForTwo()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Debilitate, false));

        fight.Play(0, target: 0);

        Assert.Equal(490, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Debilitate));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    [Fact]
    public void TheUpgradeHitsForTwelveAndDebuffsForThree()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Debilitate, true));

        fight.Play(0, target: 0);

        Assert.Equal(488, fight.Enemy0.Hp);
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Debilitate));
    }

    /// <summary>Vulnerable goes from half again to double.</summary>
    [Fact]
    public void ItDoublesVulnerable()
    {
        var control = Fresh();
        BuffSystem.Apply(control.Enemy0.Buffs, BuffId.Vulnerable, 3);
        control.State.Hand.Add(new CardInstance(Strike, false));
        control.Play(0, target: 0);
        int vulnerableOnly = 500 - control.Enemy0.Hp;

        var fight = Fresh();
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Vulnerable, 3);
        BuffSystem.Apply(fight.Enemy0.Buffs, BuffId.Debilitate, 2);
        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.Play(0, target: 0);
        int both = 500 - fight.Enemy0.Hp;

        // 1.5x becomes 2x, so the base is (damage * 1.5) -> (damage * 2).
        Assert.Equal((int)(vulnerableOnly / 1.5f * 2f), both);
    }

    /// <summary>The amount is a turn count, not a scale.</summary>
    [Fact]
    public void ItTicksDownEachTurn()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Debilitate, false));
        fight.Play(0, target: 0);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Debilitate));

        fight.EndTurn();
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Debilitate));

        fight.EndTurn();
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Debilitate));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/BlightStrike.cs: 8/10 damage, then Doom for
// `Results.Sum(r => r.TotalDamage)` — the damage actually dealt, blocked plus unblocked.
// The emulator Doomed for a flat 4.
public class BlightStrikeTests
{
    private const int BlightStrike = 44;

    [Fact]
    public void ItDoomsForTheDamageItDealt()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(BlightStrike, false));

        fight.Play(0, target: 0);

        Assert.Equal(492, fight.Enemy0.Hp);
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.Doom));
    }

    [Fact]
    public void TheUpgradeDoomsForTen()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(BlightStrike, true));

        fight.Play(0, target: 0);

        Assert.Equal(10, fight.EnemyBuffAmount(BuffId.Doom));
    }

    /// <summary>Strength raises the damage, so it raises the Doom with it.</summary>
    [Fact]
    public void StrengthRaisesTheDoomToo()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 5);
        fight.State.Hand.Add(new CardInstance(BlightStrike, false));

        fight.Play(0, target: 0);

        Assert.Equal(13, fight.EnemyBuffAmount(BuffId.Doom));
    }

    /// <summary>TotalDamage is blocked plus unblocked, so a shield does not shrink it.</summary>
    [Fact]
    public void BlockDoesNotShrinkTheDoom()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.Enemy0.Block = 100;
        fight.State.Hand.Add(new CardInstance(BlightStrike, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.Doom));
    }
}
