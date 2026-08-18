using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost, 10/14 dmg + reapply the target's Vulnerable if it survives.
public class MoltenFistTests
{
    [Fact]
    public void DamagesAndDuplicatesTargetVulnerable()
    {
        var fight = Fight
            .Hand(Card(IC.MoltenFist))
            .Energy(1)
            .Enemy(hp: 100, buffs: [new BuffState(BuffId.Vulnerable, 2)]);

        fight.Play();

        Assert.Equal(85, fight.Enemy0.Hp);
        Assert.Equal(4, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == IC.MoltenFist);
    }

    [Fact]
    public void UpgradedUsesUpgradedDamageAndTriggersVicious()
    {
        var fight = Fight
            .Hand(Card(IC.MoltenFist, upgraded: true))
            .Draw(Card(IC.StrikeIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Vicious, 1)
            .Enemy(hp: 100, buffs: [new BuffState(BuffId.Vulnerable, 1)]);

        fight.Play();

        Assert.Equal(79, fight.Enemy0.Hp);
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }
}
