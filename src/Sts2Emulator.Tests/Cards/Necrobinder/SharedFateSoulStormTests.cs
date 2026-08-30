using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/SharedFate.cs: two StrengthPowers, both applied at a
// NEGATIVE amount — the player loses 2 and the TARGET loses 2, upgrading to 3. Neither is
// temporary. The emulator GAINED Strength on the player and took the enemy's away only
// until the end of their turn.
public class SharedFateTests
{
    private const int SharedFate = 427;

    [Fact]
    public void BothSidesLoseStrength()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SharedFate, false));

        fight.Play(0, target: 0);

        Assert.Equal(-2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(-2, fight.EnemyBuffAmount(BuffId.Strength));
    }

    /// <summary>Only the ENEMY's loss upgrades.</summary>
    [Fact]
    public void TheUpgradeTakesThreeFromTheEnemyAndStillTwoFromYou()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SharedFate, true));

        fight.Play(0, target: 0);

        Assert.Equal(-2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(-3, fight.EnemyBuffAmount(BuffId.Strength));
    }

    /// <summary>Not temporary: the enemy does not get it back at the end of their turn.</summary>
    [Fact]
    public void TheEnemyDoesNotGetItBack()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SharedFate, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(-2, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    /// <summary>
    /// `GetTypeForAmount`: a COUNTER power at a negative amount is a Debuff whatever its
    /// declared type, so Sleight of Flesh pays out on the enemy's half of this card.
    /// </summary>
    [Fact]
    public void SleightOfFleshPaysOutOnTheEnemyHalf()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.SleightOfFlesh, 9);
        fight.State.Hand.Add(new CardInstance(SharedFate, false));

        fight.Play(0, target: 0);

        Assert.Equal(491, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SoulStorm.cs: CalculationBase 9 plus ExtraDamage 2
// (upgrading by 1) for each SOUL in the EXHAUST pile, at `cardPlay.Target`.
//
// The emulator had no base at all, counted the whole exhaust pile rather than the Souls in
// it, and threw the result at every enemy — three mistakes in one line.
public class SoulStormTests
{
    private const int SoulStorm = 447;
    private const int Soul = 446;
    private const int Strike = 473;

    private static Fight WithExhausted(params int[] defIds)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        foreach (int id in defIds)
        {
            fight.State.ExhaustPile.Add(new CardInstance(id, false));
        }

        return fight;
    }

    [Fact]
    public void WithNoSoulsItHitsForTheBaseNine()
    {
        var fight = WithExhausted();
        fight.State.Hand.Add(new CardInstance(SoulStorm, false));

        fight.Play(0, target: 0);

        Assert.Equal(491, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachSoulInExhaustAddsTwo()
    {
        var fight = WithExhausted(Soul, Soul, Soul);
        fight.State.Hand.Add(new CardInstance(SoulStorm, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - (9 + 6), fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeAddsThreePerSoul()
    {
        var fight = WithExhausted(Soul, Soul);
        fight.State.Hand.Add(new CardInstance(SoulStorm, true));

        fight.Play(0, target: 0);

        Assert.Equal(500 - (9 + 6), fight.Enemy0.Hp);
    }

    /// <summary>SOULS in the exhaust pile, not cards in it.</summary>
    [Fact]
    public void OtherExhaustedCardsDoNotCount()
    {
        var fight = WithExhausted(Strike, Strike, Strike, Soul);
        fight.State.Hand.Add(new CardInstance(SoulStorm, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - (9 + 2), fight.Enemy0.Hp);
    }

    /// <summary>`cardPlay.Target`, not the room.</summary>
    [Fact]
    public void ItHitsOnlyTheTarget()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SoulStorm, false));

        fight.Play(0, target: 0);

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(500, fight.State.Enemies[1].Hp);
    }
}
