using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Discovery.cs rolls
// three distinct cards with CardFactory.GetDistinctForCombat off Rng.CombatCardGeneration
// and lets you choose one, free for the turn; OnUpgrade removes the Exhaust.
//
// The choice is real. Two differences remain: the options come from the emulator's
// colourless pool rather than the character's unlocked pool, and the game's canSkip is
// not modelled — every action in a selection is a candidate, so skipping would need one
// of its own.
public class DiscoveryTests
{
    [Fact]
    public void OffersThreeDistinctCards()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(CardSelectionKind.GeneratedCardToHand, fight.Pending?.Kind);
        Assert.Equal(3, fight.Pending?.Candidates.Count);
        Assert.Equal(3, fight.Pending!.GeneratedCandidates.Distinct().Count());
    }

    [Fact]
    public void TheChosenCardJoinsTheHandFreeForTheTurn()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);
        fight.Play();
        int wanted = fight.Pending!.GeneratedCandidates[1];

        fight.Choose(1);

        Assert.Equal([wanted], Fight.Ids(fight.State.Hand));
        Assert.True(fight.State.Hand[0].FreeThisTurn);
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void TheOptionsComeOffTheCardGenerationStream()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);
        var generationRng = new CountingRandom(6);
        fight.State.CardGenerationRng = generationRng;

        fight.Play();

        // At least one draw per option offered, more when a roll repeats.
        Assert.True(generationRng.CallCount >= 3);
    }

    [Fact]
    public void TheChosenCardCostsNoEnergyThisTurn()
    {
        var fight = Fight.Hand(Card(CL.Discovery)).Energy(1).Enemy(hp: 40);
        fight.Play();
        fight.Choose(0);
        int energyBefore = fight.State.Energy;

        fight.Play(index: 0);

        Assert.Equal(energyBefore, fight.State.Energy);
    }
}
