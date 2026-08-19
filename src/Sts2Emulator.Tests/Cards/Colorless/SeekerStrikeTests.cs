using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/SeekerStrike.cs:
// DamageVar(9m), then CardsVar(3) draw-pile cards are offered and the chosen one goes to
// hand; OnUpgrade raises the damage by 3.
//
// The choice is real: the card raises a selection over the Attacks in the draw pile. The
// game samples three shuffled cards to offer; the emulator offers every Attack, which is
// the remaining difference.
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
    public void OffersTheAttacksAndTakesTheChosenOne()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Enemy(hp: 60);
        fight.Play();

        // The Defend is not on offer; candidate 1 is the Strike behind the Bash.
        Assert.Equal(2, fight.Pending?.Candidates.Count);
        fight.Choose(1);

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
        Assert.Equal([IC.DefendIronclad, IC.Bash], Fight.Ids(fight.State.DrawPile));
    }

    [Fact]
    public void AsksNothingWhenTheDrawPileHasNoAttack()
    {
        var fight = Fight
            .Hand(Card(CL.SeekerStrike))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Null(fight.Pending);
        Assert.Empty(fight.State.Hand);
        Assert.Equal([IC.DefendIronclad], Fight.Ids(fight.State.DrawPile));
    }
}
