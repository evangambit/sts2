using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Entropy.cs applies EntropyPower at
// CardsVar(1) — one card in hand is transformed at the start of each turn. OnUpgrade
// adds CardKeyword.Innate and leaves the count at 1.
public class EntropyTests
{
    [Fact]
    public void AppliesOne()
    {
        var fight = Fight.Hand(Card(CL.Entropy)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.EntropyPower));
    }

    [Fact]
    public void UpgradeMakesItInnateRatherThanTransformingMore()
    {
        var fight = Fight.Hand(Card(CL.Entropy, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.EntropyPower));
    }

    [Fact]
    public void IsInnateOnlyOnceUpgraded()
    {
        Assert.False(Card(CL.Entropy).IsInnate());
        Assert.True(Card(CL.Entropy, upgraded: true).IsInnate());
    }

    [Fact]
    public void TheTransformSurvivesIntoTheNextTurn()
    {
        var fight = Fight
            .Hand(Card(CL.Entropy), Card(IC.Bash))
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad))
            .Enemy(hp: 40);
        fight.Play(index: 0);

        fight.EndTurn();

        // Which card it transforms is a draw off the selection stream, so this pins that
        // the power persists rather than which card changed.
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.EntropyPower));
    }
}
