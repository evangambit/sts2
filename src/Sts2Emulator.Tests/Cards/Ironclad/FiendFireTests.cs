using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/FiendFire.cs
// exhausts the whole hand, then hits the target once per card exhausted for
// DamageVar(7m); OnUpgrade raises the per-hit damage by 3. It picks nothing at random —
// it takes everything. The card itself exhausts on top of that, so the exhaust pile ends
// up one larger than the hit count.
public class FiendFireTests
{
    [Fact]
    public void ExhaustsTheHandAndHitsOncePerCard()
    {
        var fight = Fight
            .Hand(Card(IC.FiendFire), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(2)
            .Enemy(hp: 60);

        // Two cards exhausted, so two hits of 7.
        fight.Play();

        Assert.Equal(46, fight.Enemy0.Hp);
        // The two hand cards, plus Fiend Fire itself.
        Assert.Equal(3, fight.State.ExhaustPile.Count);
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void UpgradedHitsForTen()
    {
        var fight = Fight
            .Hand(Card(IC.FiendFire, upgraded: true), Card(IC.Bash))
            .Energy(2)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(50, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithAnEmptyHandButStillExhaustsItself()
    {
        var fight = Fight.Hand(Card(IC.FiendFire)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
        Assert.Equal([IC.FiendFire], Fight.Ids(fight.State.ExhaustPile));
    }

    [Fact]
    public void ExhaustsAttacksAsWellAsSkills()
    {
        var fight = Fight
            .Hand(Card(IC.FiendFire), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Energy(2)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(
            [IC.StrikeIronclad, IC.DefendIronclad, IC.FiendFire],
            Fight.Ids(fight.State.ExhaustPile)
        );
    }
}
