using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/BoneShards.cs: the WHOLE body sits inside
// `if (!Osty.CheckMissingWithAnim(Owner))` — OstyDamage 9 (upgrading by 3) from Osty at
// all opponents, then BlockVar 9 (upgrading by 3) to the player, then `CreatureCmd.Kill`
// on the pet. With no pet the card does nothing at all.
//
// The emulator hit ONE enemy with the player's own damage — OstyDamageVar was not the
// value it read — and gained the block unconditionally, so a missing Osty still paid out.
public class BoneShardsTests
{
    private const int BoneShards = 53;

    private static Fight WithOsty(bool upgraded = false, int ostyHp = 10)
    {
        var fight = Fight.Hand(new CardInstance(BoneShards, upgraded))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);
        CardEffects.SummonOsty(fight.State, ostyHp);
        return fight;
    }

    [Fact]
    public void ItHitsEveryEnemyForNine()
    {
        var fight = WithOsty();

        fight.Play();

        Assert.Equal(191, fight.Enemy0.Hp);
        Assert.Equal(191, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedItHitsForTwelveAndBlocksTwelve()
    {
        var fight = WithOsty(upgraded: true);

        fight.Play();

        Assert.Equal(188, fight.Enemy0.Hp);
        Assert.Equal(188, fight.Enemy1.Hp);
        Assert.Equal(12, fight.State.PlayerBlock);
    }

    [Fact]
    public void ItGainsNineBlockAndKillsTheOsty()
    {
        var fight = WithOsty();

        fight.Play();

        Assert.Equal(9, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.OstyHp);
    }

    /// <summary>
    /// The block is inside the missing-Osty guard, so no pet means no damage AND no block.
    /// </summary>
    [Fact]
    public void WithNoOstyItDoesNothing()
    {
        var fight = Fight.Hand(new CardInstance(BoneShards, false)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>Calcify raises Osty's damage, being an Osty attack — it is tagged OstyAttack.</summary>
    [Fact]
    public void CalcifyRaisesIt()
    {
        var fight = WithOsty();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Calcify, 2);

        fight.Play();

        Assert.Equal(189, fight.Enemy0.Hp);
    }
}
