using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Bully.cs: CalculationBaseVar(4m) +
// ExtraDamageVar(2m) per stack of Vulnerable ON THE TARGET; OnUpgrade raises the
// per-stack damage to 3. Vulnerable then also multiplies the result by 1.5.
public class BullyTests
{
    [Fact]
    public void DealsFourAgainstAnUnafflictedEnemy()
    {
        var fight = Fight.Hand(Card(IC.Bully)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(36, fight.Enemy0.Hp);
    }

    [Fact]
    public void ScalesWithTheTargetsVulnerable()
    {
        var fight = Fight
            .Hand(Card(IC.Bully))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 2)]);

        // (4 + 2 x 2) x 1.5 for being Vulnerable
        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedScalesByThreePerStack()
    {
        var fight = Fight
            .Hand(Card(IC.Bully, upgraded: true))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 2)]);

        // (4 + 3 x 2) x 1.5
        fight.Play();

        Assert.Equal(25, fight.Enemy0.Hp);
    }

    [Fact]
    public void ReadsTheTargetsVulnerableNotAnotherEnemys()
    {
        var fight = Fight
            .Hand(Card(IC.Bully))
            .Energy(1)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 5)])
            .Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal(36, fight.Enemy1.Hp);
    }
}
