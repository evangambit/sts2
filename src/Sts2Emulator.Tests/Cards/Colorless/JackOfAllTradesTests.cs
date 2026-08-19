using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/JackOfAllTrades.cs
// rolls CardsVar(1) distinct cards from the whole card list off
// Rng.CombatCardGeneration and adds them to hand; OnUpgrade raises that by 1.
//
// The emulator rolls from its colourless pool rather than every card, so these pin the
// count and the stream rather than which cards arrive.
public class JackOfAllTradesTests
{
    [Fact]
    public void AddsOneCardToHand()
    {
        var fight = Fight.Hand(Card(CL.JackOfAllTrades)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void UpgradedAddsTwo()
    {
        var fight = Fight.Hand(Card(CL.JackOfAllTrades, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void RollsFromTheCardGenerationStream()
    {
        var fight = Fight.Hand(Card(CL.JackOfAllTrades)).Energy(1).Enemy(hp: 40);
        var generationRng = new CountingRandom(2);
        fight.State.CardGenerationRng = generationRng;

        fight.Play();

        Assert.Equal(1, generationRng.CallCount);
    }
}
