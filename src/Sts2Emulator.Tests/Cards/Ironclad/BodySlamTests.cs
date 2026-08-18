using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/BodySlam.cs: CalculatedDamageVar with
// multiplier (card, _) => card.Owner.Creature.Block, so damage is the player's current
// block. OnUpgrade is EnergyCost.UpgradeBy(-1) — the upgrade is cheaper, not stronger.
public class BodySlamTests
{
    [Fact]
    public void DealsDamageEqualToCurrentBlock()
    {
        var fight = Fight.Hand(Card(IC.BodySlam)).Energy(1).Enemy(hp: 40);
        fight.State.PlayerBlock = 12;

        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void SpendsNoBlockDoingIt()
    {
        var fight = Fight.Hand(Card(IC.BodySlam)).Energy(1).Enemy(hp: 40);
        fight.State.PlayerBlock = 12;

        fight.Play();

        Assert.Equal(12, fight.State.PlayerBlock);
    }

    [Fact]
    public void DealsNothingWithoutBlock()
    {
        var fight = Fight.Hand(Card(IC.BodySlam)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(40, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedCostsZeroRatherThanHittingHarder()
    {
        var fight = Fight.Hand(Card(IC.BodySlam, upgraded: true)).Energy(1).Enemy(hp: 40);
        fight.State.PlayerBlock = 12;

        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
        Assert.Equal(1, fight.State.Energy);
    }
}
