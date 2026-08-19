using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/MasterOfStrategy.cs
// draws CardsVar(3); OnUpgrade raises it by 1.
public class MasterOfStrategyTests
{
    [Fact]
    public void DrawsThree()
    {
        var fight = Fight
            .Hand(Card(CL.MasterOfStrategy))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash, IC.StrikeIronclad, IC.Anger], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDrawsFour()
    {
        var fight = Fight
            .Hand(Card(CL.MasterOfStrategy, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.State.Hand.Count);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight
            .Hand(Card(CL.MasterOfStrategy))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.MasterOfStrategy], Fight.Ids(fight.State.ExhaustPile));
    }
}
