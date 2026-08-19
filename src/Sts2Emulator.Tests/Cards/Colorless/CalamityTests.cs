using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Power. MegaCrit.Sts2.Core.Models.Cards/Calamity.cs applies CalamityPower(1);
// OnUpgrade only makes it cheaper (EnergyCost.UpgradeBy(-1)).
public class CalamityTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(CL.Calamity)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.CalamityPower));
    }

    [Fact]
    public void UpgradedCostsTwoRatherThanApplyingMore()
    {
        var fight = Fight.Hand(Card(CL.Calamity, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.CalamityPower));
        Assert.Equal(1, fight.State.Energy);
    }

    [Fact]
    public void AddsACardToHandAfterAnAttackIsPlayed()
    {
        var fight = Fight.Hand(Card(CL.Calamity), Card(IC.StrikeIronclad)).Energy(9).Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);

        Assert.NotEmpty(fight.State.Hand);
    }
}
