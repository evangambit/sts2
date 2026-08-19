using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack, TargetType.AllEnemies. MegaCrit.Sts2.Core.Models.Cards/
// HowlFromBeyond.cs: DamageVar(16m) to all opponents; OnUpgrade raises it by 5.
//
// AfterAutoPostPlayPhaseEntered replays it out of the exhaust pile: once it is exhausted
// it fires again at the start of every play phase, and stays exhausted.
public class HowlFromBeyondTests
{
    [Fact]
    public void DealsSixteenToEveryEnemy()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Enemy(hp: 40).Enemy(hp: 30);

        fight.Play();

        Assert.Equal(24, fight.Enemy0.Hp);
        Assert.Equal(14, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedDealsTwentyOne()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond, upgraded: true))
            .Energy(3)
            .Enemy(hp: 40)
            .Enemy(hp: 30);

        fight.Play();

        Assert.Equal(19, fight.Enemy0.Hp);
        Assert.Equal(9, fight.Enemy1.Hp);
    }

    [Fact]
    public void ReplaysItselfFromTheExhaustPileEachTurn()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Exhausted(Card(IC.HowlFromBeyond))
            .Enemy(hp: 90);

        fight.EndTurn();

        // The exhausted copy swings on its own as the next play phase opens.
        Assert.Equal(74, fight.Enemy0.Hp);
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == IC.HowlFromBeyond);
    }

    [Fact]
    public void DoesNotReplayWhileItIsStillInHand()
    {
        var fight = Fight.Hand(Card(IC.HowlFromBeyond)).Energy(3).Exhausted().Enemy(hp: 90);

        fight.EndTurn();

        Assert.Equal(90, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachEnemysOwnVulnerableRaisesItsShare()
    {
        var fight = Fight
            .Hand(Card(IC.HowlFromBeyond))
            .Energy(3)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)])
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.Enemy0.Hp);
        Assert.Equal(24, fight.Enemy1.Hp);
    }
}
