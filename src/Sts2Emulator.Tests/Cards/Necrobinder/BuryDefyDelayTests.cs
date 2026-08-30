using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Bury.cs: DamageVar(52) upgrading by 11, `Targeting
// (cardPlay.Target)` — one enemy, and nothing else. It shares the single-target body in
// CardEffects with Defile, Reap and Sow, which is correct for all four; Banshee's Cry was
// stacked onto that same body and is NOT, which is why it now has its own.
public class BuryTests
{
    private const int Bury = 71;

    [Fact]
    public void ItHitsOneEnemyForFiftyTwo()
    {
        var fight = Fight.Hand(new CardInstance(Bury, false)).Energy(9).Enemy(hp: 200).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(148, fight.Enemy0.Hp);
        Assert.Equal(200, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedItHitsForSixtyThree()
    {
        var fight = Fight.Hand(new CardInstance(Bury, true)).Energy(9).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(137, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Defy.cs: BlockVar(6) upgrading by 3, then WeakPower 1 on
// `cardPlay.Target` — the Weak does not upgrade. The card itself is Ethereal.
public class DefyTests
{
    private const int Defy = 138;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(Defy, upgraded)).Energy(9).Enemy(hp: 200).Enemy(hp: 200);
        fight.Play();
        return fight;
    }

    [Fact]
    public void ItGainsSixBlockAndNineUpgraded()
    {
        Assert.Equal(6, Played().State.PlayerBlock);
        Assert.Equal(9, Played(upgraded: true).State.PlayerBlock);
    }

    /// <summary>One Weak, on the target only, and it does not upgrade.</summary>
    [Fact]
    public void ItWeakensOnlyTheTarget()
    {
        var fight = Played(upgraded: true);

        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
    }

    [Fact]
    public void ItIsEthereal()
    {
        Assert.True(new CardInstance(Defy, false).IsEthereal());
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Delay.cs: BlockVar(11) upgrading by 2 and
// EnergyNextTurnPower for EnergyVar(1) upgrading by 1 — both halves upgrade.
public class DelayTests
{
    private const int Delay = 139;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(Delay, upgraded)).Energy(9).Enemy(hp: 200);
        fight.Play();
        return fight;
    }

    [Fact]
    public void ItGainsElevenBlockAndThirteenUpgraded()
    {
        Assert.Equal(11, Played().State.PlayerBlock);
        Assert.Equal(13, Played(upgraded: true).State.PlayerBlock);
    }

    [Fact]
    public void ItBanksOneEnergyAndTwoUpgraded()
    {
        Assert.Equal(1, Played().PlayerBuffAmount(BuffId.NextTurnEnergy));
        Assert.Equal(2, Played(upgraded: true).PlayerBuffAmount(BuffId.NextTurnEnergy));
    }
}
