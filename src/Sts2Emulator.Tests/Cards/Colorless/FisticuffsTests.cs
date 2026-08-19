using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Fisticuffs.cs: DamageVar(7m), then block
// equal to the damage the attack actually dealt; OnUpgrade raises the damage by 2.
//
// Divergence worth knowing: the game reads the attack's own results, so Strength and the
// target's Vulnerable raise the block too. The emulator gains block equal to the card's
// printed damage, so the two agree only when no modifier is in play — which is what the
// third test records.
public class FisticuffsTests
{
    [Fact]
    public void DealsSevenAndGainsSevenBlock()
    {
        var fight = Fight.Hand(Card(CL.Fisticuffs)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(53, fight.Enemy0.Hp);
        Assert.Equal(7, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedDealsAndGainsNine()
    {
        var fight = Fight.Hand(Card(CL.Fisticuffs, upgraded: true)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(51, fight.Enemy0.Hp);
        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void StrengthRaisesTheDamageButNotTheBlockHere()
    {
        var fight = Fight
            .Hand(Card(CL.Fisticuffs))
            .Energy(1)
            .PlayerBuff(BuffId.Strength, 3)
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(50, fight.Enemy0.Hp);
        // The game would give 10 here, reading the damage it actually dealt.
        Assert.Equal(7, fight.State.PlayerBlock);
    }
}
