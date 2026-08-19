using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/SecretWeapon.cs lets you CHOOSE an Attack from the draw pile and puts it in hand; OnUpgrade removes the Exhaust. The emulator takes the first Attack instead of asking.
public class SecretWeaponTests
{
    [Fact]
    public void TakesTheFirstAttackFromTheDrawPile()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void TakesNothingWhenTheDrawPileHasNoAttack()
    {
        var fight = Fight
            .Hand(Card(CL.SecretWeapon))
            .Energy(1)
            .Draw(Card(IC.DefendIronclad))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal([IC.DefendIronclad], Fight.Ids(fight.State.DrawPile));
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

        Assert.Equal([CL.SecretWeapon], Fight.Ids(fight.State.ExhaustPile));
    }
}
