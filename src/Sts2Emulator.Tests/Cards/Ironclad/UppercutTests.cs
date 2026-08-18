using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Uppercut.cs: DamageVar(13m), then
// WeakPower and VulnerablePower both applied at DynamicVars["Power"] = 1; OnUpgrade
// raises that Power to 2 and leaves the damage at 13.
public class UppercutTests
{
    [Fact]
    public void DealsThirteenAndAppliesOneWeakAndOneVulnerable()
    {
        var fight = Fight.Hand(Card(IC.Uppercut)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(27, fight.Enemy0.Hp);
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradedAppliesTwoOfEachAndTheSameDamage()
    {
        var fight = Fight.Hand(Card(IC.Uppercut, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(27, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void TheVulnerableItAppliesDoesNotBoostItsOwnHit()
    {
        var fight = Fight.Hand(Card(IC.Uppercut)).Energy(2).Enemy(hp: 40);

        // The debuffs land after the attack resolves, so the hit is a flat 13.
        fight.Play();

        Assert.Equal(27, fight.Enemy0.Hp);
    }
}
