using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/SecretWeapon.cs lets
// you choose a Attack from the draw pile and puts it in hand; OnUpgrade removes the
// Exhaust. The choice is a real selection: only the Attacks in the pile are offered.
public class SecretWeaponTests
{
    [Fact]
    public void OffersOnlyTheAttacksInTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.StrikeIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(CardSelectionKind.DrawPileToHand, fight.Pending?.Kind);
        Assert.Equal(2, fight.Pending?.Candidates.Count);
    }

    [Fact]
    public void PutsTheChosenCardInHand()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play();

        fight.Choose(0);

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void AsksNothingWhenTheDrawPileHasNoAttack()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Null(fight.Pending);
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.StrikeIronclad))
            .Enemy(hp: 40);
        fight.Play();
        fight.Choose(0);

        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == CL.SecretWeapon);
    }
}
