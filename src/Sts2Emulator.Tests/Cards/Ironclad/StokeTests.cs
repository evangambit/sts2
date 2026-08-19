using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Stoke.cs exhausts the whole hand, then
// rolls up that many replacement cards with CardFactory.GetForCombat drawing from
// Rng.CombatCardGeneration, upgrading them when the card is upgraded. There is no
// numeric upgrade.
//
// The emulator rolls from its own Ironclad pool rather than the character's unlocked
// pool, so these tests pin the shape — how many, from which stream, upgraded or not —
// rather than which specific cards come back.
public class StokeTests
{
    [Fact]
    public void ExhaustsTheHandAndReplacesItCardForCard()
    {
        var fight = Fight
            .Hand(Card(IC.Stoke), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count);
        Assert.Equal(2, fight.State.ExhaustPile.Count);
        Assert.DoesNotContain(fight.State.Hand, card => card.DefId == IC.Bash);
    }

    [Fact]
    public void UpgradedRollsUpgradedCards()
    {
        var fight = Fight
            .Hand(Card(IC.Stoke, upgraded: true), Card(IC.Bash))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.All(fight.State.Hand, card => Assert.True(card.Upgraded));
    }

    [Fact]
    public void RollsFromTheCardGenerationStream()
    {
        var fight = Fight
            .Hand(Card(IC.Stoke), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);
        var generationRng = new CountingRandom(3);
        fight.State.CardGenerationRng = generationRng;

        fight.Play();

        // One draw per replacement card.
        Assert.Equal(2, generationRng.CallCount);
    }

    [Fact]
    public void DoesNothingWithAnEmptyHand()
    {
        var fight = Fight.Hand(Card(IC.Stoke)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.ExhaustPile);
    }
}
