using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Scrawl.cs draws
// MaxCardsInHand minus the current hand — i.e. until the hand is full; OnUpgrade adds
// CardKeyword.Retain rather than changing the draw.
public class ScrawlTests
{
    [Fact]
    public void DrawsUntilTheHandIsFull()
    {
        var fight = Fight
            .Hand(Card(CL.Scrawl), Card(IC.Bash))
            .Energy(1)
            .Draw(
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad),
                Card(IC.StrikeIronclad)
            )
            .Enemy(hp: 40);

        fight.Play(index: 0);

        Assert.Equal(10, fight.State.Hand.Count);
    }

    [Fact]
    public void DrawsWhatItCanFromAShortDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.Scrawl))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash, IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.Scrawl)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.Scrawl], Fight.Ids(fight.State.ExhaustPile));
        Assert.Empty(fight.State.DiscardPile);
    }
}
