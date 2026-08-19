using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Rend.cs: CalculationBaseVar(15m) plus
// ExtraDamageVar(5m) per power on the target that ShouldCountPower accepts; OnUpgrade
// raises both the base and the per-power damage by 3.
public class RendTests
{
    [Fact]
    public void DealsFifteenAgainstAnUnafflictedEnemy()
    {
        var fight = Fight.Hand(Card(CL.Rend)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(45, fight.Enemy0.Hp);
    }

    [Fact]
    public void GainsFivePerDebuffOnTheTarget()
    {
        var fight = Fight
            .Hand(Card(CL.Rend))
            .Energy(2)
            .Enemy(hp: 90, buffs: [new BuffState(BuffId.Weak, 2), new BuffState(BuffId.Frail, 1)]);

        // (15 + 5 x 2) with no Vulnerable in the mix to scale it.
        fight.Play();

        Assert.Equal(65, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedStartsFromEighteenAndGainsEight()
    {
        var fight = Fight
            .Hand(Card(CL.Rend, upgraded: true))
            .Energy(2)
            .Enemy(hp: 90, buffs: [new BuffState(BuffId.Weak, 2)]);

        // 18 + 8 x 1
        fight.Play();

        Assert.Equal(64, fight.Enemy0.Hp);
    }
}
