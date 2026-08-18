using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, TargetType.RandomEnemy. MegaCrit.Sts2.Core.Models.Cards/
// SwordBoomerang.cs: DamageVar(3m) with RepeatVar(3), TargetingRandomOpponents;
// OnUpgrade raises the repeat by 1, not the damage.
public class SwordBoomerangTests
{
    [Fact]
    public void DealsThreeDamageThreeTimes()
    {
        var fight = Fight.Hand(Card(IC.SwordBoomerang)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedHitsAFourthTime()
    {
        var fight = Fight.Hand(Card(IC.SwordBoomerang, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    /// <summary>
    /// Each hit re-rolls its target, so with two enemies the nine damage is split rather
    /// than concentrated. Asserting a per-enemy split for one seed would only restate the
    /// emulator's own output, so this asserts the total and that both enemies are reachable.
    /// </summary>
    [Fact]
    public void SpreadsItsHitsAcrossEnemies()
    {
        var hit = new bool[2];

        for (int seed = 0; seed < 16; seed++)
        {
            var fight = Fight
                .Hand(Card(IC.SwordBoomerang))
                .Seed(seed)
                .Energy(1)
                .Enemy(hp: 40)
                .Enemy(hp: 40);

            fight.Play();

            Assert.Equal(9, 80 - fight.Enemy0.Hp - fight.Enemy1.Hp);
            hit[0] |= fight.Enemy0.Hp < 40;
            hit[1] |= fight.Enemy1.Hp < 40;
        }

        Assert.True(hit[0], "the first enemy was never hit across 16 seeds");
        Assert.True(hit[1], "the second enemy was never hit across 16 seeds");
    }
}
