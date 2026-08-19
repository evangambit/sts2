using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Impatience.cs draws CardsVar(2) only when
// no Attack is left in hand; OnUpgrade raises the draw by 1.
public class ImpatienceTests
{
    [Fact]
    public void DrawsTwoWithNoAttacksInHand()
    {
        var fight = Fight
            .Hand(Card(CL.Impatience), Card(IC.DefendIronclad))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play(index: 0);

        Assert.Equal(3, fight.State.Hand.Count);
    }

    [Fact]
    public void DrawsNothingWhileAnAttackRemains()
    {
        var fight = Fight
            .Hand(Card(CL.Impatience), Card(IC.StrikeIronclad))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play(index: 0);

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDrawsThree()
    {
        var fight = Fight
            .Hand(Card(CL.Impatience, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.State.Hand.Count);
    }
}
