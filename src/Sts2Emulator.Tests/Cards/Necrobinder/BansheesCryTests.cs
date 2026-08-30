using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/BansheesCry.cs: DamageVar(33) `TargetingAllOpponents`,
// and a cost of NINE that comes down by EnergyVar(2) for every Ethereal card played this
// combat. Two hooks do that: `AfterCardEnteredCombat` pays the whole backlog at once for a
// card that arrives late, and `AfterCardPlayed` pays each new Ethereal play as it lands.
// Upgrading takes the printed cost to seven and leaves the damage alone.
//
// The emulator shared the single-target Bury/Defile/Reap/Sow body, so it hit ONE enemy,
// and had neither hook — a nine-cost card that never got cheaper is a card that never
// gets played.
public class BansheesCryTests
{
    private const int BansheesCry = 27;
    private const int Defy = 138; // Ethereal, costs 1
    private const int StrikeNecrobinder = 473;

    [Fact]
    public void ItHitsEveryEnemyForThirtyThree()
    {
        var fight = Fight.Hand(new CardInstance(BansheesCry, false))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);

        fight.Play();

        Assert.Equal(167, fight.Enemy0.Hp);
        Assert.Equal(167, fight.Enemy1.Hp);
    }

    /// <summary>Upgrading is all cost: 9 down to 7, damage unchanged.</summary>
    [Fact]
    public void UpgradingBuysCostNotDamage()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200);

        Assert.Equal(9, CombatEngine.EffectiveCost(new CardInstance(BansheesCry, false), fight.State));
        Assert.Equal(7, CombatEngine.EffectiveCost(new CardInstance(BansheesCry, true), fight.State));

        fight.State.Hand.Add(new CardInstance(BansheesCry, true));
        fight.Play();

        Assert.Equal(167, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachEtherealPlayTakesTwoOffTheCost()
    {
        var fight = Fight.Hand(new CardInstance(Defy, false), new CardInstance(Defy, false))
            .Energy(9)
            .Enemy(hp: 200);

        fight.Play();
        Assert.Equal(7, CombatEngine.EffectiveCost(new CardInstance(BansheesCry, false), fight.State));

        fight.Play();
        Assert.Equal(5, CombatEngine.EffectiveCost(new CardInstance(BansheesCry, false), fight.State));
    }

    /// <summary>A card that is not Ethereal does nothing to the price.</summary>
    [Fact]
    public void APlainCardDoesNotDiscountIt()
    {
        var fight = Fight.Hand(new CardInstance(StrikeNecrobinder, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(9, CombatEngine.EffectiveCost(new CardInstance(BansheesCry, false), fight.State));
    }

    /// <summary>
    /// The discount is on the copy in hand as much as on any other, because it is a
    /// property of the combat and not of the instance — a copy that arrives after the
    /// Ethereal plays is priced the same as one that watched them.
    /// </summary>
    [Fact]
    public void ACopyThatArrivesLateIsPricedTheSame()
    {
        var fight = Fight.Hand(new CardInstance(Defy, false)).Energy(9).Enemy(hp: 200);
        fight.Play();

        var arrived = new CardInstance(BansheesCry, false);
        fight.State.Hand.Add(arrived);

        Assert.Equal(7, CombatEngine.EffectiveCost(arrived, fight.State));
    }
}
