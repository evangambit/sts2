using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/FeelNoPain.cs applies FeelNoPainPower at
// DynamicVar("Power", 3m); OnUpgrade raises it by 1. The power's AfterCardExhausted
// grants that much block every time a card is exhausted.
public class FeelNoPainTests
{
    [Fact]
    public void AppliesThree()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.FeelNoPain));
    }

    [Fact]
    public void UpgradedAppliesFour()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FeelNoPain));
    }

    [Fact]
    public void GivesBlockWhenACardIsExhausted()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain), Card(IC.Tremble)).Energy(9).Enemy(hp: 40);
        fight.Play(index: 0);

        // Tremble exhausts itself.
        fight.Play(index: 0);

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    [Fact]
    public void GivesBlockOncePerCardExhausted()
    {
        var fight = Fight
            .Hand(Card(IC.FeelNoPain), Card(IC.FiendFire), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        // Fiend Fire exhausts the two remaining cards and then itself.
        fight.Play(index: 0);

        Assert.Equal(9, fight.State.PlayerBlock);
    }
}
