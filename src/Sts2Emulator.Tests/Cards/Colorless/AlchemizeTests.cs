using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Alchemize.cs procures
// PotionFactory.CreateRandomPotionInCombat, rolled off Rng.CombatPotionGeneration;
// OnUpgrade only makes it cheaper.
public class AlchemizeTests
{
    [Fact]
    public void FillsAnEmptyPotionSlot()
    {
        var fight = Fight.Hand(Card(CL.Alchemize)).Energy(1).Enemy(hp: 40);
        fight.State.PotionSlots[0] = 0;
        fight.State.PotionSlots[1] = 0;
        fight.State.PotionSlots[2] = 0;

        fight.Play();

        Assert.NotEqual(0, fight.State.PotionSlots[0]);
        Assert.Equal(0, fight.State.PotionSlots[1]);
    }

    [Fact]
    public void RollsThePotionFromThePotionGenerationStream()
    {
        var fight = Fight.Hand(Card(CL.Alchemize)).Energy(1).Enemy(hp: 40);
        fight.State.PotionSlots[0] = 0;
        var potionRng = new CountingRandom(9);
        fight.State.PotionGenerationRng = potionRng;

        fight.Play();

        Assert.Equal(1, potionRng.CallCount);
    }

    [Fact]
    public void DoesNothingWithNoFreeSlot()
    {
        var fight = Fight.Hand(Card(CL.Alchemize)).Energy(1).Enemy(hp: 40);
        fight.State.PotionSlots[0] = 5;
        fight.State.PotionSlots[1] = 6;
        fight.State.PotionSlots[2] = 7;

        fight.Play();

        Assert.Equal(new[] { 5, 6, 7 }, fight.State.PotionSlots.ToList());
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.Alchemize)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.Alchemize], Fight.Ids(fight.State.ExhaustPile));
    }
}
