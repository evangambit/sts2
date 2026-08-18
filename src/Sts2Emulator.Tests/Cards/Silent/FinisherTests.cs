using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Finisher.cs: DamageVar(6m) with
// hitCount = CalculationBase(0) + 1 per Attack play finished this turn; OnUpgrade raises
// the damage by 2. AttackCommand's hit loop does not run at zero, so with no Attacks
// played first, Finisher deals nothing.
public class FinisherTests
{
    [Fact]
    public void DealsNothingWhenNoAttackHasBeenPlayedThisTurn()
    {
        var fight = Fight.Hand(Card(SI.Finisher)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }

    [Fact]
    public void HitsOncePerAttackPlayedThisTurn()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad), Card(SI.Finisher))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);
        fight.Play(index: 0);

        // Two Strikes at 6 each, then Finisher hitting twice for 6.
        fight.Play(index: 0);

        Assert.Equal(36, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedHitsForEight()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad), Card(SI.Finisher, upgraded: true))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        fight.Play(index: 0);

        // One Strike for 6, then a single 8-damage hit.
        Assert.Equal(46, fight.Enemy0.Hp);
    }
}
