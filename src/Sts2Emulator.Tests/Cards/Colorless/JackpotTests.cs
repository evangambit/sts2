using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 3-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Jackpot.cs: DamageVar(25m), then
// CardsVar(3) zero-cost cards rolled up and added to hand, upgraded when the card is;
// OnUpgrade raises the damage by 5 and leaves the card count at 3.
//
// The emulator rolls from its own colourless pool rather than the character's unlocked
// pool and adds one extra when upgraded, so the count assertions pin what it models.
public class JackpotTests
{
    [Fact]
    public void DealsTwentyFiveAndFillsTheHand()
    {
        var fight = Fight.Hand(Card(CL.Jackpot)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(35, fight.Enemy0.Hp);
        Assert.Equal(3, fight.State.Hand.Count);
    }

    [Fact]
    public void UpgradedDealsThirty()
    {
        var fight = Fight.Hand(Card(CL.Jackpot, upgraded: true)).Energy(3).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(30, fight.Enemy0.Hp);
    }

    [Fact]
    public void RollsTheCardsFromTheGenerationStream()
    {
        var fight = Fight.Hand(Card(CL.Jackpot)).Energy(3).Enemy(hp: 60);
        var generationRng = new CountingRandom(4);
        fight.State.CardGenerationRng = generationRng;

        fight.Play();

        Assert.Equal(3, generationRng.CallCount);
    }

    /// <summary>
    /// `GetForCombat` applies `FilterForCombat` to whatever it is handed, so the zero-cost
    /// filter composes with it: no Basic, Ancient or Event cards, and nothing declaring
    /// `CanBeGeneratedInCombat => false`. The pool used to be the raw character list
    /// filtered only by cost.
    /// </summary>
    [Fact]
    public void ThePoolExcludesWhatFilterForCombatDrops()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 200; seed++)
        {
            var fight = Fight.Hand(Card(CL.Jackpot)).Energy(3).Enemy(hp: 300);
            fight.State.CardGenerationRng = new CountingRandom(seed);
            fight.Play();
            foreach (var c in fight.State.Hand)
            {
                seen.Add(c.DefId);
            }
        }

        Assert.NotEmpty(seen);
        Assert.All(
            seen,
            id =>
            {
                var d = GeneratedData.Cards.Get(id);
                Assert.NotEqual(CardRarity.Ancient, d.Rarity);
                Assert.NotEqual(CardRarity.Event, d.Rarity);
                Assert.True(d.CanBeGeneratedInCombat, $"{d.Name} refuses combat generation");
            }
        );
    }
}
