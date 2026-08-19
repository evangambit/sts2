using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/CrimsonMantle.cs applies
// PowerVar<CrimsonMantlePower>(8m) and then calls IncrementSelfDamage() on it, so each
// copy played raises the HP you pay at the start of your turn; OnUpgrade raises the
// block by 2.
public class CrimsonMantleTests
{
    [Fact]
    public void AppliesEightBlockAndOneSelfDamage()
    {
        var fight = Fight.Hand(Card(IC.CrimsonMantle)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.CrimsonMantleBlock));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.CrimsonMantleSelfDamage));
    }

    [Fact]
    public void UpgradedAppliesTenBlock()
    {
        var fight = Fight.Hand(Card(IC.CrimsonMantle, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(10, fight.PlayerBuffAmount(BuffId.CrimsonMantleBlock));
    }

    [Fact]
    public void PlayingASecondCopyRaisesTheHpItCosts()
    {
        var fight = Fight
            .Hand(Card(IC.CrimsonMantle), Card(IC.CrimsonMantle))
            .Energy(9)
            .Enemy(hp: 40);

        fight.Play(index: 0);
        fight.Play(index: 0);

        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.CrimsonMantleSelfDamage));
        Assert.Equal(16, fight.PlayerBuffAmount(BuffId.CrimsonMantleBlock));
    }

    [Fact]
    public void PaysHpAndGivesBlockAtTheStartOfTheNextTurn()
    {
        var fight = Fight.Hand(Card(IC.CrimsonMantle)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(63, fight.State.PlayerHp);
        Assert.Equal(8, fight.State.PlayerBlock);
    }
}
