using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/PerfectedStrike.cs:
// CalculationBaseVar(6m) + ExtraDamageVar(2m) per card with CardTag.Strike in
// PlayerCombatState.AllCards; OnUpgrade raises the per-Strike damage to 3.
//
// AllCards spans Hand, DrawPile, DiscardPile, ExhaustPile AND PlayPile, so the copy
// being played counts itself. The emulator matches Strikes by name rather than by tag,
// so these tests use Strike_Ironclad, where the two agree.
public class PerfectedStrikeTests
{
    [Fact]
    public void CountsItselfWhenNoOtherStrikeIsAround()
    {
        var fight = Fight.Hand(Card(IC.PerfectedStrike)).Energy(2).Draw().Enemy(hp: 40);

        // 6 + 2 x 1 (itself)
        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
    }

    [Fact]
    public void GainsTwoDamagePerStrikeInEveryPile()
    {
        var fight = Fight
            .Hand(Card(IC.PerfectedStrike), Card(IC.StrikeIronclad))
            .Energy(2)
            .Draw(Card(IC.StrikeIronclad))
            .Discard(Card(IC.StrikeIronclad))
            .Exhausted(Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        // 6 + 2 x 5 (four Strikes across the piles, plus itself)
        fight.Play();

        Assert.Equal(44, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedGainsThreePerStrike()
    {
        var fight = Fight
            .Hand(Card(IC.PerfectedStrike, upgraded: true))
            .Energy(2)
            .Draw(Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        // 6 + 3 x 3 (two Strikes in the draw pile, plus itself)
        fight.Play();

        Assert.Equal(45, fight.Enemy0.Hp);
    }

    [Fact]
    public void NonStrikeCardsDoNotCount()
    {
        var fight = Fight
            .Hand(Card(IC.PerfectedStrike))
            .Energy(2)
            .Draw(Card(IC.DefendIronclad), Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(32, fight.Enemy0.Hp);
    }
}
