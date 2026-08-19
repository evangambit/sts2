using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardRarity.Token — the card Primal Force turns your Attacks into.
// MegaCrit.Sts2.Core.Models.Cards/GiantRock.cs: DamageVar(16m); OnUpgrade raises it by 4.
// No case in CardEffects.Apply; it runs on the generic damage-and-block path.
public class GiantRockTests
{
    [Fact]
    public void DealsSixteen()
    {
        var fight = Fight.Hand(Card(IC.GiantRock)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsTwenty()
    {
        var fight = Fight.Hand(Card(IC.GiantRock, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(20, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsWhatPrimalForceLeavesBehindAndItHitsForThatMuch()
    {
        var fight = Fight
            .Hand(Card(IC.PrimalForce), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 40);
        fight.Play(index: 0);

        // The Strike became a Giant Rock, so the 6-damage card now hits for 16.
        fight.Play(index: 0);

        Assert.Equal(24, fight.Enemy0.Hp);
    }
}
