using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/BurningPact.cs: CardSelectCmd.FromHand
// exhausts a chosen card, THEN CardsVar(2) are drawn; OnUpgrade raises the draw to 3.
//
// The draw follows the choice, so cards drawn by this play are never candidates for its
// own exhaust.
public class BurningPactTests
{
    [Fact]
    public void AsksWhichCardToExhaustBeforeDrawing()
    {
        var fight = Fight
            .Hand(Card(IC.BurningPact), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Draw(Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(CardSelectionKind.ExhaustFromHandThenDraw, fight.Pending?.Kind);
        Assert.Equal(2, fight.Pending?.Candidates.Count);
        // Nothing drawn yet — the choice comes first.
        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void ExhaustsTheChosenCardAndThenDrawsTwo()
    {
        var fight = Fight
            .Hand(Card(IC.BurningPact), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Draw(Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);
        fight.Play();

        fight.Choose(0);

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.ExhaustPile));
        Assert.Equal([IC.StrikeIronclad, IC.Anger, IC.DefendIronclad], Fight.Ids(fight.State.Hand));
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void UpgradedDrawsThree()
    {
        var fight = Fight
            .Hand(Card(IC.BurningPact, upgraded: true), Card(IC.Bash))
            .Energy(1)
            .Draw(Card(IC.Anger), Card(IC.DefendIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);
        fight.Play();

        fight.Choose(0);

        Assert.Equal(3, fight.State.Hand.Count);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.ExhaustPile));
    }

    [Fact]
    public void DrawsWithoutAskingWhenTheHandIsEmpty()
    {
        var fight = Fight
            .Hand(Card(IC.BurningPact))
            .Energy(1)
            .Draw(Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Null(fight.Pending);
        Assert.Equal([IC.Anger, IC.DefendIronclad], Fight.Ids(fight.State.Hand));
    }
}
