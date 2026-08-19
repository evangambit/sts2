using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/PrimalForce.cs transforms every
// transformable Attack in hand into a GiantRock, upgraded when the card is; there is no
// numeric upgrade at all.
//
// The game filters on IsTransformable, which the emulator does not model.
public class PrimalForceTests
{
    [Fact]
    public void TurnsEveryAttackInHandIntoAGiantRock()
    {
        var fight = Fight
            .Hand(Card(IC.PrimalForce), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.GiantRock, IC.GiantRock], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void LeavesSkillsAlone()
    {
        var fight = Fight
            .Hand(Card(IC.PrimalForce), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.DefendIronclad, IC.GiantRock], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedMakesTheRocksUpgraded()
    {
        var fight = Fight
            .Hand(Card(IC.PrimalForce, upgraded: true), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.All(fight.State.Hand, card => Assert.True(card.Upgraded));
    }

    [Fact]
    public void DoesNothingToAnEmptyHand()
    {
        var fight = Fight.Hand(Card(IC.PrimalForce)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }
}
