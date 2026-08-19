using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/PactsEnd.cs:
// DamageVar(17m) to all opponents, but the whole OnPlay is wrapped in
//   CanDealDamage => CardPile.GetCards(Exhaust).Count() >= CardsVar(3)
// so below three exhausted cards the card does nothing. OnUpgrade raises damage by 6.
public class PactsEndTests
{
    [Fact]
    public void DealsNothingWithFewerThanThreeCardsExhausted()
    {
        var fight = Fight
            .Hand(Card(IC.PactsEnd))
            .Energy(1)
            .Exhausted(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsSeventeenToEveryEnemyOnceThreeAreExhausted()
    {
        var fight = Fight
            .Hand(Card(IC.PactsEnd))
            .Energy(1)
            .Exhausted(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 60)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(43, fight.Enemy0.Hp);
        Assert.Equal(23, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedDealsTwentyThree()
    {
        var fight = Fight
            .Hand(Card(IC.PactsEnd, upgraded: true))
            .Energy(1)
            .Exhausted(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(37, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithAnEmptyExhaustPile()
    {
        var fight = Fight.Hand(Card(IC.PactsEnd)).Energy(1).Exhausted().Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }
}
