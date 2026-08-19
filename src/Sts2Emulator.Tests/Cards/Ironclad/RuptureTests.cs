using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Rupture.cs applies RupturePower at
// PowerVar<StrengthPower>(1m); OnUpgrade raises it by 1. The power's AfterDamageReceived
// grants that much Strength whenever the player takes unblocked damage on their own
// side's turn — which is what makes it a self-damage payoff rather than a defensive one.
public class RuptureTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(IC.Rupture)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.RupturePower));
    }

    [Fact]
    public void UpgradedAppliesTwo()
    {
        var fight = Fight.Hand(Card(IC.Rupture, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.RupturePower));
    }

    [Fact]
    public void GivesStrengthWhenACardCostsYouHp()
    {
        var fight = Fight
            .Hand(Card(IC.Rupture), Card(IC.Bloodletting))
            .Energy(9)
            .PlayerHp(64)
            .Enemy(hp: 40);
        fight.Play(index: 0);

        // Bloodletting pays 3 HP for its energy.
        fight.Play(index: 0);

        Assert.Equal(61, fight.State.PlayerHp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void GivesNoStrengthWithoutHpLoss()
    {
        var fight = Fight
            .Hand(Card(IC.Rupture), Card(IC.StrikeIronclad))
            .Energy(9)
            .PlayerHp(64)
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }
}
