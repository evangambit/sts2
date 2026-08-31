using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Afterlife.cs and Bodyguard.cs: both are a bare
// `OstyCmd.Summon` for their SummonVar and nothing else — 6 upgrading by 3, and 5
// upgrading by 2. Afterlife is Exhaust, Bodyguard is a Basic that is not.
//
// Each also had a DEAD duplicate case further down CardEffects — Afterlife drawing cards,
// Bodyguard gaining block — behind the Necrobinder dispatch that returns first. These
// tests pin the summon AND the absence of the thing the dead arm would have done, which
// is the only way a dead arm ever announces itself.
public class AfterlifeTests
{
    private const int Afterlife = 8;
    private const int Bodyguard = 49;

    private static Fight Played(int defId, bool upgraded)
    {
        var fight = Fight.Hand(new CardInstance(defId, upgraded)).Energy(9).Enemy(hp: 200);
        fight.Play();
        return fight;
    }

    [Fact]
    public void AfterlifeSummonsSixAndNineUpgraded()
    {
        Assert.Equal(6, Played(Afterlife, false).State.OstyHp);
        Assert.Equal(9, Played(Afterlife, true).State.OstyHp);
    }

    /// <summary>Neither draws a card — the dead Afterlife arm did.</summary>
    [Fact]
    public void NeitherDraws()
    {
        var fight = Fight.Hand(new CardInstance(Afterlife, false)).Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Add(new CardInstance(Bodyguard, false));

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    /// <summary>Neither gains block — the dead Bodyguard arm did.</summary>
    [Fact]
    public void NeitherGainsBlock()
    {
        Assert.Equal(0, Played(Bodyguard, false).State.PlayerBlock);
        Assert.Equal(0, Played(Afterlife, false).State.PlayerBlock);
    }

    /// <summary>
    /// `OstyCmd.Summon` on a LIVING pet is GainMaxHp, so a second summon grows it rather
    /// than replacing it.
    /// </summary>
    [Fact]
    public void ASecondSummonGrowsTheSameOsty()
    {
        var fight = Fight
            .Hand(new CardInstance(Bodyguard, false), new CardInstance(Afterlife, false))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();
        fight.Play();

        Assert.Equal(11, fight.State.OstyHp);
        Assert.Equal(11, fight.State.OstyMaxHp);
    }

    /// <summary>Afterlife is Exhaust; Bodyguard is not.</summary>
    [Fact]
    public void OnlyAfterlifeExhausts()
    {
        Assert.Single(Played(Afterlife, false).State.ExhaustPile);
        Assert.Empty(Played(Bodyguard, false).State.ExhaustPile);
    }
}

/// <summary>Bodyguard's half of the pair above — see AfterlifeTests for the shared reading.</summary>
public class BodyguardTests
{
    private const int Bodyguard = 49;

    [Fact]
    public void ItSummonsFiveAndSevenUpgraded()
    {
        var fight = Fight.Hand(new CardInstance(Bodyguard, false)).Energy(9).Enemy(hp: 200);
        fight.Play();
        Assert.Equal(5, fight.State.OstyHp);

        var up = Fight.Hand(new CardInstance(Bodyguard, true)).Energy(9).Enemy(hp: 200);
        up.Play();
        Assert.Equal(7, up.State.OstyHp);
    }

    /// <summary>It is a Basic skill and does not exhaust — the dead arm gained block.</summary>
    [Fact]
    public void ItDoesNotExhaustOrBlock()
    {
        var fight = Fight.Hand(new CardInstance(Bodyguard, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Empty(fight.State.ExhaustPile);
        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
