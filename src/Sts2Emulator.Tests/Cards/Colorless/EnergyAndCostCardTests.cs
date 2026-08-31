using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Three cards that shared one `GainEnergy(upgraded ? 2 : 1)`. They have three different
/// `EnergyVar`s, and only one of them upgrades its number at all.
/// </summary>
public class LuminesceTests
{
    private static int Id => GeneratedData.Cards.FindId("Luminesce")!.Value;

    /// <summary>`EnergyVar(2)` upgrading by 1 — two, or three.</summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void TwoEnergyOrThree(bool upgraded, int gained)
    {
        var fight = Fight.Hand(new CardInstance(Id, upgraded)).Energy(3).Enemy(hp: 300);
        int before = fight.State.Energy;

        fight.Play(0);

        Assert.Equal(before + gained, fight.State.Energy);
    }
}

// `Supercritical` and `Wisp` already have test classes (Defect/RareCardTests and
// Necrobinder/NecrobinderReadPinTests), and both assert the right numbers -- their real
// arms run in earlier dispatches. Their labels in the shared body were dead.

/// <summary>
/// `Enlightenment` sets every card in hand to cost ONE — `SetThisCombat(1, reduceOnly)`
/// upgraded, `SetThisTurnOrUntilPlayed(1, reduceOnly)` otherwise. The emulator made the
/// whole hand FREE and ignored the upgrade: a strictly better card, for a whole turn
/// instead of the right one.
/// </summary>
public class EnlightenmentTests
{
    private static int Id => GeneratedData.Cards.FindId("Enlightenment")!.Value;

    private static Fight WithHand(bool upgraded)
    {
        var fight = Fight
            .Hand(
                new CardInstance(Id, upgraded),
                new CardInstance(IC.Bash, false),
                new CardInstance(IC.StrikeIronclad, false)
            )
            .Energy(9)
            .Enemy(hp: 300);
        return fight;
    }

    /// <summary>Cost ONE, not zero — a 2-cost Bash becomes 1 rather than free.</summary>
    [Fact]
    public void ItSetsCostsToOneNotZero()
    {
        var fight = WithHand(upgraded: false);

        fight.Play(0);

        int bash = fight.State.Hand.FindIndex(card => card.DefId == IC.Bash);
        Assert.Equal(1, CombatEngine.EffectiveCost(fight.State.Hand[bash], fight.State));
    }

    /// <summary>`reduceOnly` — a Strike already at 1 is not touched, and never raised.</summary>
    [Fact]
    public void ItNeverRaisesACost()
    {
        var fight = WithHand(upgraded: false);

        fight.Play(0);

        int strike = fight.State.Hand.FindIndex(card => card.DefId == IC.StrikeIronclad);
        Assert.Equal(1, CombatEngine.EffectiveCost(fight.State.Hand[strike], fight.State));
    }

    /// <summary>
    /// Unupgraded it lasts the TURN: a card retained into the next turn costs its printed
    /// price again.
    /// </summary>
    [Fact]
    public void UnupgradedTheDiscountEndsWithTheTurn()
    {
        var fight = WithHand(upgraded: false);
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        fight.Play(0);

        fight.EndTurn();

        var bashInPile = fight.State.DiscardPile.First(card => card.DefId == IC.Bash);
        Assert.Equal(int.MinValue, bashInPile.CostThisTurn);
    }

    /// <summary>Upgraded it lasts the COMBAT, so it rides into the discard and back.</summary>
    [Fact]
    public void UpgradedTheDiscountSurvivesTheTurn()
    {
        var fight = WithHand(upgraded: true);
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        fight.Play(0);

        fight.EndTurn();

        var bashInPile = fight.State.DiscardPile.First(card => card.DefId == IC.Bash);
        Assert.Equal(1, bashInPile.CostForCombat);
    }
}
