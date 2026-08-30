using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Anointed.cs moves
// every Rare card out of the draw pile into hand, capped by the space left there;
// OnUpgrade adds CardKeyword.Retain.
public class AnointedTests
{
    [Fact]
    public void TakesEveryRareCardOutOfTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.Anointed))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad), Card(IC.Juggernaut), Card(IC.Barricade))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count);
        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.DrawPile));
    }

    [Fact]
    public void TakesNothingWithoutRares()
    {
        var fight = Fight
            .Hand(Card(CL.Anointed))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.Anointed)).Energy(1).Draw(Card(IC.Juggernaut)).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.Anointed], Fight.Ids(fight.State.ExhaustPile));
    }

    /// <summary>
    /// `TakeRandom(count, Rng.CombatCardSelection)` is `UnstableShuffle(rng).Take(count)`,
    /// so which rares come up is a SHUFFLE — not a walk from either end of the pile. With
    /// more rares than hand space the emulator took the last ones by index every time.
    /// </summary>
    [Fact]
    public void WithMoreRaresThanRoomThePickVariesWithTheStream()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var fight = Fight.Hand(Card(CL.Anointed)).Energy(3);
            fight.State.CardSelectionRng = new CountingRandom(seed);
            fight.State.DrawPile =
            [
                Card(IC.Barricade),
                Card(IC.DemonForm),
                Card(IC.Corruption),
                Card(IC.Juggernaut),
                Card(IC.Impervious),
                Card(IC.Bludgeon),
            ];
            // Room for two.
            while (fight.State.Hand.Count < 8)
            {
                fight.State.Hand.Add(Card(IC.StrikeIronclad));
            }

            fight.Play(0);
            seen.Add(
                string.Join(
                    ",",
                    fight.State.Hand.Where(c => c.DefId != IC.StrikeIronclad).Select(c => c.DefId)
                )
            );
        }

        Assert.True(seen.Count > 1, $"the pick never varied: {string.Join(" | ", seen)}");
    }

    /// <summary>It draws from the CombatCardSelection stream, which it used not to touch.</summary>
    [Fact]
    public void ItRollsOnTheCardSelectionStream()
    {
        var fight = Fight.Hand(Card(CL.Anointed)).Energy(3);
        var stream = new CountingRandom(4);
        fight.State.CardSelectionRng = stream;
        fight.State.DrawPile = [Card(IC.Barricade), Card(IC.DemonForm), Card(IC.Corruption)];

        fight.Play(0);

        Assert.True(stream.CallCount > 0, "the selection stream should have been drawn from");
    }
}
