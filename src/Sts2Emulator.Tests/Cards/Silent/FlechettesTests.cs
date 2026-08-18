using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Flechettes.cs: DamageVar(5m) with
// hitCount = 1 per Skill in hand; OnUpgrade raises the damage by 2. No Skills in hand
// means no hits at all.
public class FlechettesTests
{
    [Fact]
    public void HitsOncePerSkillInHand()
    {
        var fight = Fight
            .Hand(Card(SI.Flechettes), Card(IC.DefendIronclad), Card(IC.ShrugItOff))
            .Energy(1)
            .Enemy(hp: 60);

        // Two Skills left in hand once Flechettes leaves it, so two hits of 5.
        fight.Play();

        Assert.Equal(50, fight.Enemy0.Hp);
    }

    [Fact]
    public void DealsNothingWithNoSkillsInHand()
    {
        var fight = Fight
            .Hand(Card(SI.Flechettes), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedHitsForSeven()
    {
        var fight = Fight
            .Hand(Card(SI.Flechettes, upgraded: true), Card(IC.DefendIronclad))
            .Energy(1)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(53, fight.Enemy0.Hp);
    }
}
