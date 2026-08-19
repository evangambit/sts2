using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Prowess.cs applies
// PowerVar<StrengthPower>(1m) and PowerVar<DexterityPower>(1m); OnUpgrade raises both by 1.
public class ProwessTests
{
    [Fact]
    public void GainsOneStrengthAndOneDexterity()
    {
        var fight = Fight.Hand(Card(CL.Prowess)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void UpgradedGainsTwoOfEach()
    {
        var fight = Fight.Hand(Card(CL.Prowess, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Strength));
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void TheStrengthRaisesLaterAttacksAndTheDexterityLaterBlock()
    {
        var fight = Fight
            .Hand(Card(CL.Prowess), Card(IC.StrikeIronclad), Card(IC.DefendIronclad))
            .Energy(9)
            .Draw(Card(IC.Bash))
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);
        fight.Play(index: 0);

        Assert.Equal(33, fight.Enemy0.Hp);
        Assert.Equal(6, fight.State.PlayerBlock);
    }
}
