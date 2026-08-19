using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack. MegaCrit.Sts2.Core.Models.Cards/HandOfGreed.cs: DamageVar(20m) and
// DynamicVar("Gold", 20m), the gold paid out only when the hit is fatal; OnUpgrade
// raises both by 5.
public class HandOfGreedTests
{
    [Fact]
    public void DealsTwentyAndPaysNoGoldWhenTheEnemyLives()
    {
        var fight = Fight.Hand(Card(CL.HandOfGreed)).Energy(2).Enemy(hp: 40);
        int goldBefore = fight.State.PlayerGold;

        fight.Play();

        Assert.Equal(20, fight.Enemy0.Hp);
        Assert.Equal(goldBefore, fight.State.PlayerGold);
    }

    [Fact]
    public void PaysTwentyGoldOnAFatalHit()
    {
        var fight = Fight.Hand(Card(CL.HandOfGreed)).Energy(2).Enemy(hp: 12);
        int goldBefore = fight.State.PlayerGold;

        fight.Play();

        Assert.Equal(0, fight.Enemy0.Hp);
        Assert.Equal(goldBefore + 20, fight.State.PlayerGold);
    }

    [Fact]
    public void UpgradedDealsTwentyFiveAndPaysTwentyFive()
    {
        var fight = Fight.Hand(Card(CL.HandOfGreed, upgraded: true)).Energy(2).Enemy(hp: 12);
        int goldBefore = fight.State.PlayerGold;

        fight.Play();

        Assert.Equal(goldBefore + 25, fight.State.PlayerGold);
    }
}
