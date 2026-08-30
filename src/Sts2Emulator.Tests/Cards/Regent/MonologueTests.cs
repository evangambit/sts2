using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Monologue.cs: a `Power` var of 1, and the upgrade adds
// Retain. `MonologuePower` gives that much Strength for every card its owner plays —
// recorded in BeforeCardPlayed and paid in AfterCardPlayed, so the card that applied it
// does not pay — and at the owner's side-turn end it removes itself and takes back
// everything it gave.
//
// The emulator had it retaining the hand.
public class MonologueTests
{
    private const int Monologue = 316;
    private const int StrikeRegent = 474;
    private const int DefendRegent = 133;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Monologue, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItPaysNothingForItsOwnPlay()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Monologue));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.RetainHand));
    }

    [Fact]
    public void EveryLaterCardPaysAStrength()
    {
        var fight = Played();

        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>
    /// The Strength is real while it lasts, and an attack lands with what was ALREADY
    /// there: the point that attack earns is paid in AfterCardPlayed, after its damage. So
    /// one Defend first means +1 on the Strike, not +2.
    /// </summary>
    [Fact]
    public void TheStrengthCountsOnTheAttackButNotItsOwnPoint()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(StrikeRegent, false));
        control.Play(0, target: 0);
        int plain = 500 - control.Enemy0.Hp;

        var fight = Played();
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);

        Assert.Equal(500 - (plain + 1), fight.Enemy0.Hp);
    }

    /// <summary>At the turn's end the power goes and every point it gave goes with it.</summary>
    [Fact]
    public void ItTakesItAllBackAtTheEndOfTheTurn()
    {
        var fight = Played();
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Monologue));
    }

    /// <summary>Strength the player had already is not taken with it.</summary>
    [Fact]
    public void ItOnlyTakesBackWhatItGave()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 5);
        fight.State.Hand.Add(new CardInstance(Monologue, false));
        fight.Play(0);
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);

        fight.EndTurn();

        Assert.Equal(5, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/MonarchsGaze.cs: a `StrengthLoss` var of 1, and the
// upgrade is a discount. `MonarchsGazePower.AfterDamageGiven` takes that much temporary
// Strength off the TARGET of every POWERED attack its owner lands.
//
// It had been one more label on the flat Strength body.
public class MonarchsGazeTests
{
    private const int MonarchsGaze = 315;
    private const int StrikeRegent = 474;

    private static Fight Played()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(MonarchsGaze, false));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void PlayingItTakesNothingYet()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.MonarchsGaze));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void EachAttackTakesOneFromWhatItHit()
    {
        var fight = Played();
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));

        fight.Play(0, target: 0);

        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength, 1));
    }

    /// <summary>Temporary: the enemy has it back once their turn is over.</summary>
    [Fact]
    public void TheEnemyGetsItBack()
    {
        var fight = Played();
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }
}
