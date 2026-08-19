using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Power. MegaCrit.Sts2.Core.Models.Cards/EternalArmor.cs applies
// PowerVar<PlatingPower>(9m); OnUpgrade raises it by 3.
public class EternalArmorTests
{
    [Fact]
    public void AppliesNinePlating()
    {
        var fight = Fight.Hand(Card(CL.EternalArmor)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(9, fight.PlayerBuffAmount(BuffId.Plating));
    }

    [Fact]
    public void UpgradedAppliesTwelve()
    {
        var fight = Fight.Hand(Card(CL.EternalArmor, upgraded: true)).Energy(3).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(12, fight.PlayerBuffAmount(BuffId.Plating));
    }

    /// <summary>
    /// Plating pays out as block at the END of the player turn and then decays by 1 at
    /// the start of the next one, so by the time a full turn cycle returns, that block
    /// has already been spent or cleared. The decay is the observable that survives.
    /// </summary>
    [Fact]
    public void ThePlatingDecaysByOneEachTurn()
    {
        var fight = Fight.Hand(Card(CL.EternalArmor)).Energy(3).Enemy(hp: 40);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.Plating));
    }
}
