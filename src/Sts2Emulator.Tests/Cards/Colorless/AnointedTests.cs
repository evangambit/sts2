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
}
