using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardKeyword.Exhaust, MultiplayerOnly, TargetType.AllAllies.
// MegaCrit.Sts2.Core.Models.Cards/HuddleUp.cs draws CardsVar(2) for every living ally;
// OnUpgrade raises it by 1. Singleplayer has one ally — you — so it is a plain draw.
public class HuddleUpTests
{
    [Fact]
    public void DrawsTwo()
    {
        var fight = Fight
            .Hand(Card(CL.HuddleUp))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash, IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDrawsThree()
    {
        var fight = Fight
            .Hand(Card(CL.HuddleUp, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.State.Hand.Count);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.HuddleUp)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.HuddleUp], Fight.Ids(fight.State.ExhaustPile));
    }
}
