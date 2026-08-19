using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Power. MegaCrit.Sts2.Core.Models.Cards/Pyre.cs applies PyrePower at
// EnergyVar(1) — extra energy each turn; OnUpgrade raises it by 1.
public class PyreTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(IC.Pyre)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.PyrePower));
    }

    [Fact]
    public void UpgradedAppliesTwo()
    {
        var fight = Fight.Hand(Card(IC.Pyre, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.PyrePower));
    }

    [Fact]
    public void GivesTheExtraEnergyFromTheNextTurnOnwards()
    {
        var fight = Fight.Hand(Card(IC.Pyre)).Energy(2).Enemy(hp: 40);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(fight.State.MaxEnergy + 1, fight.State.Energy);
    }
}
