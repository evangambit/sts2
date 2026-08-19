using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/Brand.cs: HpLossVar(1m) as
// Unblockable | Unpowered | Move, then CardSelectCmd.FromHand exhausts a card you pick,
// then PowerVar<StrengthPower>(1m); OnUpgrade raises the Strength by 1.
public class BrandTests
{
    [Fact]
    public void LosesOneHpGainsStrengthAndAsksWhatToExhaust()
    {
        var fight = Fight
            .Hand(Card(IC.Brand), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .PlayerHp(64)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(63, fight.State.PlayerHp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(CardSelectionKind.ExhaustFromHand, fight.Pending?.Kind);
    }

    [Fact]
    public void ExhaustsTheCardTheCallerChose()
    {
        var fight = Fight
            .Hand(Card(IC.Brand), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);
        fight.Play();

        fight.Choose(1);

        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.ExhaustPile));
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedGainsTwoStrength()
    {
        var fight = Fight.Hand(Card(IC.Brand, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void AsksNothingWithAnEmptyHand()
    {
        var fight = Fight.Hand(Card(IC.Brand)).Energy(1).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Null(fight.Pending);
        Assert.Equal(63, fight.State.PlayerHp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
    }
}
