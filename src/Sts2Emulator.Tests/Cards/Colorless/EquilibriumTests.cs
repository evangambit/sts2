using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill, CardKeyword.Retain. MegaCrit.Sts2.Core.Models.Cards/Equilibrium.cs:
// BlockVar(13m) then RetainHandPower at DynamicVar("Equilibrium", 1m) — the hand is kept
// through the turn; OnUpgrade raises the block by 3.
public class EquilibriumTests
{
    [Fact]
    public void GainsThirteenBlockAndRetainsTheHand()
    {
        var fight = Fight.Hand(Card(CL.Equilibrium)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(13, fight.State.PlayerBlock);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.RetainHand));
    }

    [Fact]
    public void UpgradedGainsSixteen()
    {
        var fight = Fight.Hand(Card(CL.Equilibrium, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheHandSurvivesTheTurn()
    {
        var fight = Fight
            .Hand(Card(CL.Equilibrium), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.EndTurn();

        Assert.Contains(fight.State.Hand, card => card.DefId == IC.Bash);
    }
}
