using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Misery.cs: 7 damage upgrading by 2 at the target, and
// then every DEBUFF the target was carrying is copied onto every other hittable enemy, at
// the amount it had, stacking with what they already hold. The list is snapshotted BEFORE
// the attack — `originalDebuffs` is built first — so a debuff the hit removes still
// spreads. Upgrading also adds Retain.
//
// Misery had been stacked onto High Five's case since the first commit: it was an Osty
// attack at every enemy for eleven with a Vulnerable rider, which is a different card.
public class MiseryTests
{
    private const int Misery = 312;

    private static Fight Spread(bool upgraded = false)
    {
        var fight = Fight
            .Hand(new CardInstance(Misery, upgraded))
            .Energy(9)
            .Enemy(hp: 200, buffs: [new BuffState(BuffId.Weak, 2), new BuffState(BuffId.Doom, 9)])
            .Enemy(hp: 200)
            .Enemy(hp: 200, buffs: [new BuffState(BuffId.Weak, 1)]);
        fight.Play();
        return fight;
    }

    [Fact]
    public void ItHitsTheTargetForSeven()
    {
        var fight = Spread();

        Assert.Equal(193, fight.Enemy0.Hp);
        Assert.Equal(200, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedItHitsForNine()
    {
        Assert.Equal(191, Spread(upgraded: true).Enemy0.Hp);
    }

    [Fact]
    public void EveryOtherEnemyPicksUpTheTargetsDebuffs()
    {
        var fight = Spread();

        Assert.Equal(2, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
        Assert.Equal(9, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
    }

    /// <summary>It STACKS on what they already carry rather than replacing it.</summary>
    [Fact]
    public void ItStacksWithDebuffsTheyAlreadyHold()
    {
        var fight = Spread();

        Assert.Equal(3, BuffSystem.Get(fight.State.Enemies[2].Buffs, BuffId.Weak));
    }

    /// <summary>The target itself is skipped — its debuffs do not double.</summary>
    [Fact]
    public void TheTargetDoesNotDoubleItsOwn()
    {
        var fight = Spread();

        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(9, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
    }

    /// <summary>Nothing on the target is nothing to spread.</summary>
    [Fact]
    public void ACleanTargetSpreadsNothing()
    {
        var fight = Fight
            .Hand(new CardInstance(Misery, false))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);

        fight.Play();

        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
    }

    /// <summary>It is not an Osty attack — the body it used to share was High Five's.</summary>
    [Fact]
    public void ItNeedsNoOstyAndHitsOnlyTheTarget()
    {
        var fight = Fight
            .Hand(new CardInstance(Misery, false))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);

        fight.Play();

        Assert.Equal(0, fight.State.OstyHp);
        Assert.Equal(193, fight.Enemy0.Hp);
        Assert.Equal(200, fight.Enemy1.Hp);
        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }
}
