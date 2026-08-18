using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Bloodletting.cs: HpLossVar(3m) dealt as
// Unblockable | Unpowered | Move, then EnergyVar(2); OnUpgrade raises energy by 1.
public class BloodlettingTests
{
    [Fact]
    public void LosesThreeHpForTwoEnergy()
    {
        var fight = Fight.Hand(Card(IC.Bloodletting)).Energy(0).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(2, fight.State.Energy);
    }

    [Fact]
    public void UpgradedGivesThreeEnergy()
    {
        var fight = Fight
            .Hand(Card(IC.Bloodletting, upgraded: true))
            .Energy(0)
            .PlayerHp(64)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(3, fight.State.Energy);
    }

    [Fact]
    public void TheHpLossIsUnblockable()
    {
        var fight = Fight.Hand(Card(IC.Bloodletting)).Energy(0).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 10;

        fight.Play();

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
    }
}
