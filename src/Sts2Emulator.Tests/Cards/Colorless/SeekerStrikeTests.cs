using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/SeekerStrike.cs:
// DamageVar(9m), then CardsVar(3) draw-pile cards are offered and the chosen one goes to
// hand; OnUpgrade raises the damage by 3.
//
// The emulator approximates the search: instead of offering three shuffled draw-pile
// cards to choose from, it pulls the first Attack out of the draw pile. These pin that
// approximation, not the choice.
public class SeekerStrikeTests
{
    [Fact]
    public void DealsNine()
    {
        var fight = Fight.Hand(Card(CL.SeekerStrike)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(51, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsTwelve()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(48, fight.Enemy0.Hp);
    }

    [Fact]
    public void PullsTheFirstAttackOutOfTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play();

        // The Defend is skipped; Bash is the first Attack in the pile.
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
        Assert.Equal([IC.DefendIronclad, IC.StrikeIronclad], Fight.Ids(fight.State.DrawPile));
    }

    [Fact]
    public void TakesNothingWhenTheDrawPileHasNoAttack()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal([IC.DefendIronclad], Fight.Ids(fight.State.DrawPile));
    }
}
