using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack, CardTag.Strike. MegaCrit.Sts2.Core.Models.Cards/StrikeIronclad.cs:
// DamageVar(6m); OnUpgrade raises it by 3.
//
// Strike has no case in CardEffects.Apply — it falls through to the generic
// damage-and-block path, which is why the coverage guard never listed it. It is also the
// most-played card in the game, so it is worth pinning that path explicitly.
public class StrikeIroncladTests
{
    [Fact]
    public void DealsSix()
    {
        var fight = Fight.Hand(Card(IC.StrikeIronclad)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsNine()
    {
        var fight = Fight.Hand(Card(IC.StrikeIronclad, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(31, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsRaisedByStrengthAndByTheTargetsVulnerable()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad))
            .Energy(1)
            .PlayerBuff(BuffId.Strength, 2)
            .Enemy(hp: 40, buffs: [new BuffState(BuffId.Vulnerable, 1)]);

        // (6 + 2) x 1.5
        fight.Play();

        Assert.Equal(28, fight.Enemy0.Hp);
    }

    [Fact]
    public void HitsOnlyTheTargetedEnemyAndGoesToTheDiscardPile()
    {
        var fight = Fight.Hand(Card(IC.StrikeIronclad)).Energy(1).Enemy(hp: 40).Enemy(hp: 40);

        fight.Play(target: 1);

        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Equal(34, fight.Enemy1.Hp);
        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.DiscardPile));
    }
}
