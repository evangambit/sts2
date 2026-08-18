using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/AshenStrike.cs:
// CalculationBaseVar(6m) + ExtraDamageVar(3m) per card in the exhaust pile;
// OnUpgrade raises the per-card damage to 4.
public class AshenStrikeTests
{
    [Fact]
    public void DealsSixWithAnEmptyExhaustPile()
    {
        var fight = Fight.Hand(Card(IC.AshenStrike)).Energy(1).Exhausted().Enemy(hp: 40);

        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
    }

    [Fact]
    public void GainsThreePerExhaustedCard()
    {
        var fight = Fight
            .Hand(Card(IC.AshenStrike))
            .Energy(1)
            .Exhausted(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        // 6 + 3 x 2
        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedGainsFourPerExhaustedCard()
    {
        var fight = Fight
            .Hand(Card(IC.AshenStrike, upgraded: true))
            .Energy(1)
            .Exhausted(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        // 6 + 4 x 2
        fight.Play();

        Assert.Equal(26, fight.Enemy0.Hp);
    }
}
