using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// The four Starter relics, one per character. Every run of that character holds one, so
// these are the most-exercised relics in the game -- and they were the last four with
// nothing at all behind them.

public class RingOfTheSnakeTests
{
    /// <summary>Silent opens on SEVEN: `ModifyHandDraw` adds 2 while TurnNumber is 1.</summary>
    [Fact]
    public void TheOpeningHandIsSeven()
    {
        var fight = Fight.WithRelics(RelicEffects.RingOfTheSnake);

        Assert.Equal(7, fight.State.Hand.Count);
    }

    [Fact]
    public void WithoutItTheOpeningHandIsFive()
    {
        var fight = Fight.WithRelics();

        Assert.Equal(5, fight.State.Hand.Count);
    }

    /// <summary>
    /// `TurnNumber > 1` returns the count unchanged, so it pays once and never again --
    /// which is why it rides the opening hand rather than the turn-start draw.
    /// </summary>
    [Fact]
    public void TurnTwoDrawsTheOrdinaryFive()
    {
        var fight = Fight.WithRelics(RelicEffects.RingOfTheSnake);
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.Equal(5, fight.State.Hand.Count);
    }
}

public class CrackedCoreTests
{
    /// <summary>One Lightning, channelled before the first turn starts.</summary>
    [Fact]
    public void ItChannelsOneLightningAtCombatStart()
    {
        var fight = Fight.WithRelics(RelicEffects.CrackedCore);

        Assert.Single(fight.State.Orbs);
        Assert.Equal(OrbType.Lightning, fight.State.Orbs[0].Type);
    }

    /// <summary>
    /// `BeforeSideTurnStart` guards on `TurnNumber &lt;= 1`, so it is a combat-start
    /// channel and not a per-turn engine.
    /// </summary>
    [Fact]
    public void ItDoesNotChannelAgainOnLaterTurns()
    {
        var fight = Fight.WithRelics(RelicEffects.CrackedCore);
        fight.State.Hand.Clear();

        fight.EndTurn();
        fight.State.Hand.Clear();
        fight.EndTurn();

        Assert.Single(fight.State.Orbs);
    }
}

public class DivineRightTests
{
    [Fact]
    public void ThreeStarsAtTheTopOfTheFight()
    {
        var fight = Fight.WithRelics(RelicEffects.DivineRight);

        Assert.Equal(3, fight.State.Stars);
    }

    /// <summary>
    /// Stars live on the `PlayerCombatState`, so this is three per FIGHT. A reading that
    /// accumulated them across the run would hand a late-act Regent a free Stardust.
    /// </summary>
    [Fact]
    public void ASecondCombatAlsoStartsAtThree()
    {
        var first = Fight.WithRelics(RelicEffects.DivineRight);
        var second = Fight.WithRelics(RelicEffects.DivineRight);

        Assert.Equal(3, first.State.Stars);
        Assert.Equal(3, second.State.Stars);
    }

    [Fact]
    public void ItDoesNotFireEveryTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.DivineRight);
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.Equal(3, fight.State.Stars);
    }
}

public class BoundPhylacteryTests
{
    /// <summary>`SummonVar(1)`: a one-HP body, not a wall.</summary>
    [Fact]
    public void OstyArrivesWithOneHpAtCombatStart()
    {
        var fight = Fight.WithRelics(RelicEffects.BoundPhylactery);

        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    /// <summary>
    /// `AfterEnergyResetLate` re-summons on every turn but the first, and `OstyCmd.Summon`
    /// on a LIVING Osty is `GainMaxHp` -- so turn two is +1 rather than a second pet.
    /// </summary>
    [Fact]
    public void ALivingOstyGrowsByOneEachLaterTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.BoundPhylactery);
        fight.State.Hand.Clear();
        // Osty is a damage sink now, and a one-HP one dies to the first thing that gets
        // through. Block keeps it alive so what is measured is the growth and not the
        // re-summon -- see AOneHpOstyDiesToAHitAndComesBackAtOne for the other path.
        fight.State.PlayerBlock = 200;

        fight.EndTurn();
        Assert.Equal(2, fight.State.OstyMaxHp);

        fight.State.Hand.Clear();
        fight.State.PlayerBlock = 200;
        fight.EndTurn();
        Assert.Equal(3, fight.State.OstyMaxHp);
    }

    /// <summary>
    /// The relic's real job, now that the pet can actually die: one HP is one hit, and the
    /// next turn puts a fresh body up rather than growing the old one.
    /// </summary>
    [Fact]
    public void AOneHpOstyDiesToAHitAndComesBackAtOne()
    {
        var fight = Fight.WithRelics(RelicEffects.BoundPhylactery);
        fight.State.Hand.Clear();
        fight.State.PlayerBlock = 0;

        fight.EndTurn();

        Assert.Equal(1, fight.State.OstyHp);
        Assert.Equal(1, fight.State.OstyMaxHp);
    }

    /// <summary>A dead Osty comes back the next turn, which is the relic's real job.</summary>
    [Fact]
    public void ADeadOstyIsResummoned()
    {
        var fight = Fight.WithRelics(RelicEffects.BoundPhylactery);
        fight.State.OstyHp = 0;
        fight.State.OstyMaxHp = 0;
        fight.State.Hand.Clear();

        fight.EndTurn();

        Assert.Equal(1, fight.State.OstyHp);
    }

    [Fact]
    public void WithoutItNoPetAppears()
    {
        var fight = Fight.WithRelics();

        Assert.Equal(0, fight.State.OstyHp);
    }
}

public class BlackHoleStarsTests
{
    /// <summary>
    /// `BlackHolePower.AfterStarsGained` is the only listener on the stars hook, and it
    /// is why stars needed a chokepoint at all. The card was sharing a `case` body that
    /// granted Strength.
    /// </summary>
    [Fact]
    public void GainingStarsDamagesEveryEnemy()
    {
        var fight = Fight.Hand().Enemy(hp: 40).Enemy(hp: 40).PlayerBuff(BuffId.BlackHole, 3);

        CardEffects.GainStars(fight.State, 1);

        Assert.Equal(37, fight.Enemy0.Hp);
        Assert.Equal(37, fight.Enemy1.Hp);
    }

    /// <summary>Per GAIN, not per star: two stars in one gain is one volley.</summary>
    [Fact]
    public void ItFiresOncePerGainNotOncePerStar()
    {
        var fight = Fight.Hand().Enemy(hp: 40).PlayerBuff(BuffId.BlackHole, 3);

        CardEffects.GainStars(fight.State, 5);

        Assert.Equal(37, fight.Enemy0.Hp);
        Assert.Equal(5, fight.State.Stars);
    }

    /// <summary>`LoseStars` is a different command and dispatches nothing.</summary>
    [Fact]
    public void SpendingStarsFiresNothing()
    {
        var fight = Fight.Hand().Enemy(hp: 40).PlayerBuff(BuffId.BlackHole, 3);
        fight.State.Stars = 5;

        fight.State.Stars = 0;

        Assert.Equal(40, fight.Enemy0.Hp);
    }

    /// <summary>
    /// And the card reaches the body: it was one label in a merged `case` that granted
    /// Strength, so playing it did something unrelated and the power did not exist.
    /// </summary>
    [Fact]
    public void PlayingTheCardAppliesThePower()
    {
        const int blackHole = 41;
        var fight = Fight.Hand(new CardInstance(blackHole, false)).Energy(3).Enemy(hp: 40);

        fight.Play(0);

        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.BlackHole));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>The power is Unpowered: Strength stays out of it.</summary>
    [Fact]
    public void StrengthDoesNotRaiseIt()
    {
        var fight = Fight.Hand().Enemy(hp: 40).PlayerBuff(BuffId.BlackHole, 3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 5);

        CardEffects.GainStars(fight.State, 1);

        Assert.Equal(37, fight.Enemy0.Hp);
    }
}
