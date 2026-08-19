using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Discovery.cs offers
// distinct generated cards to CHOOSE from and adds the pick to hand free for the turn;
// OnUpgrade removes the Exhaust.
//
// The emulator rolls one card off the generation stream instead of offering a choice.
public class DiscoveryTests
{
    [Fact]
    public void AddsOneCardFreeForTheTurn()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Single(fight.State.Hand);
        Assert.True(fight.State.Hand[0].FreeThisTurn);
    }

    [Fact]
    public void RollsTheCardFromTheGenerationStream()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);
        var generationRng = new CountingRandom(6);
        fight.State.CardGenerationRng = generationRng;

        fight.Play();

        Assert.Equal(1, generationRng.CallCount);
    }

    [Fact]
    public void TheAddedCardCostsNoEnergyThisTurn()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);
        fight.Play();
        int energyBefore = fight.State.Energy;

        fight.Play(index: 0);

        Assert.Equal(energyBefore, fight.State.Energy);
    }
}
