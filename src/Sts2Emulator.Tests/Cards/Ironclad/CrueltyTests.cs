using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/Cruelty.cs applies
// PowerVar<CrueltyPower>(25m) — Vulnerable's damage multiplier rises by that percentage;
// OnUpgrade raises it by another 25.
public class CrueltyTests
{
    [Fact]
    public void AppliesTwentyFive()
    {
        var fight = Fight.Hand(Card(IC.Cruelty)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(25, fight.PlayerBuffAmount(BuffId.CrueltyPower));
    }

    [Fact]
    public void UpgradedAppliesFifty()
    {
        var fight = Fight.Hand(Card(IC.Cruelty, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(50, fight.PlayerBuffAmount(BuffId.CrueltyPower));
    }

    [Fact]
    public void RaisesTheVulnerableMultiplierOnLaterAttacks()
    {
        var fight = Fight
            .Hand(Card(IC.Cruelty), Card(IC.Bash))
            .Energy(9)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 2)]);
        fight.Play(index: 0);

        // Bash's 8 at 1.5 + 0.25, chosen over a 6-damage Strike so the multiplier lands
        // on a whole number and the test pins the rate rather than a rounding rule.
        fight.Play(index: 0);

        Assert.Equal(26, fight.Enemy0.Hp);
    }

    [Fact]
    public void DoesNothingToAnEnemyWithoutVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Cruelty), Card(IC.StrikeIronclad)).Energy(9).Enemy(hp: 40);
        fight.Play(index: 0);

        fight.Play(index: 0);

        Assert.Equal(34, fight.Enemy0.Hp);
    }
}
