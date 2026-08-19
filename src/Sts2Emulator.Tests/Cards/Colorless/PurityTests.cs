using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust and Retain.
// MegaCrit.Sts2.Core.Models.Cards/Purity.cs exhausts up to CardsVar(3) cards CHOSEN from
// hand; OnUpgrade raises the limit by 2.
//
// The emulator exhausts the first three instead of asking. The pending-selection
// mechanism handles one card at a time, so a multi-card choice would need it extended;
// until then these pin the approximation.
public class PurityTests
{
    [Fact]
    public void ExhaustsThreeCardsFromHand()
    {
        var fight = Fight
            .Hand(
                Card(CL.Purity),
                Card(IC.Bash),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Anger)
            )
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Anger], Fight.Ids(fight.State.Hand));
        Assert.Equal(4, fight.State.ExhaustPile.Count);
    }

    [Fact]
    public void UpgradedExhaustsUpToFive()
    {
        var fight = Fight
            .Hand(
                Card(CL.Purity, upgraded: true),
                Card(IC.Bash),
                Card(IC.StrikeIronclad),
                Card(IC.DefendIronclad),
                Card(IC.Anger),
                Card(IC.Bash)
            )
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void ExhaustsWhatItCanFromAShortHand()
    {
        var fight = Fight.Hand(Card(CL.Purity), Card(IC.Bash)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal([IC.Bash, CL.Purity], Fight.Ids(fight.State.ExhaustPile));
    }
}
