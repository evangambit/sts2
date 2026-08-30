using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/LunarBlast.cs: 4 damage upgrading by 1, once per SKILL
// the player has FINISHED playing this turn — `CalculationBase 0 + Extra 1` counted over
// `CardPlaysFinished`. With no skills played it deals nothing at all, exactly as Pull From
// Below does with no Ethereal.
//
// The emulator hit once per STAR, floored at one.
public class LunarBlastTests
{
    private const int LunarBlast = 290;
    private const int DefendRegent = 133; // a Skill
    private const int StrikeRegent = 474; // an Attack

    private static Fight Fresh()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 9;
        return fight;
    }

    [Fact]
    public void WithNoSkillsPlayedItDealsNothing()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(LunarBlast, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItHitsOncePerSkillPlayed()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        fight.Play(0);
        int blocked = fight.State.PlayerBlock;

        fight.State.Hand.Add(new CardInstance(LunarBlast, false));
        fight.Play(0, target: 0);

        Assert.Equal(500 - 8, fight.Enemy0.Hp);
        Assert.True(blocked > 0, "the Defends really were skills that resolved");
    }

    /// <summary>Attacks are not skills.</summary>
    [Fact]
    public void AttacksDoNotCount()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        int afterStrike = fight.Enemy0.Hp;

        fight.State.Hand.Add(new CardInstance(LunarBlast, false));
        fight.Play(0, target: 0);

        Assert.Equal(afterStrike, fight.Enemy0.Hp);
    }

    /// <summary>The count is per TURN, so it resets.</summary>
    [Fact]
    public void TheCountResetsEachTurn()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);
        fight.EndTurn();
        fight.State.Energy = 9;

        fight.State.Hand.Add(new CardInstance(LunarBlast, false));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeHitsForFive()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(DefendRegent, false));
        fight.Play(0);

        fight.State.Hand.Add(new CardInstance(LunarBlast, true));
        fight.Play(0, target: 0);

        Assert.Equal(500 - 5, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/KnowThyPlace.cs: Weak 1 and Vulnerable 1 on the TARGET,
// and it Exhausts until upgraded. The emulator drew a card.
public class KnowThyPlaceTests
{
    private const int KnowThyPlace = 279;

    [Fact]
    public void ItWeakensAndExposesTheTarget()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(KnowThyPlace, false));

        fight.Play(0, target: 0);

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable, 0));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak, 1));
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(KnowThyPlace, false));

        fight.Play(0, target: 0);

        Assert.Single(fight.State.ExhaustPile);
    }

    /// <summary>`RemoveKeyword(Exhaust)` is the whole upgrade.</summary>
    [Fact]
    public void TheUpgradeDoesNotExhaust()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(KnowThyPlace, true));

        fight.Play(0, target: 0);

        Assert.Empty(fight.State.ExhaustPile);
        Assert.Single(fight.State.DiscardPile);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
    }
}
