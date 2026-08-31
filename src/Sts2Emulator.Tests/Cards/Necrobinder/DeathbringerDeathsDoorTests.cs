using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Deathbringer.cs: DoomPower 21 (upgrading by 5) and
// WeakPower 1 (which does not upgrade), both applied to `CombatState.HittableEnemies`.
// The emulator put both on one enemy — the card is TargetType.AllEnemies.
public class DeathbringerTests
{
    private const int Deathbringer = 124;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight
            .Hand(new CardInstance(Deathbringer, upgraded))
            .Energy(9)
            .Enemy(hp: 200)
            .Enemy(hp: 200);
        fight.Play();
        return fight;
    }

    [Fact]
    public void ItDoomsEveryEnemyForTwentyOne()
    {
        var fight = Played();

        Assert.Equal(21, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
        Assert.Equal(21, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
    }

    [Fact]
    public void UpgradedTheDoomIsTwentySixAndTheWeakIsStillOne()
    {
        var fight = Played(upgraded: true);

        Assert.Equal(26, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
        Assert.Equal(26, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(1, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
    }

    [Fact]
    public void ItWeakensEveryEnemy()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(1, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/DeathsDoor.cs: one `CreatureCmd.GainBlock` of BlockVar 6
// (upgrading by 1), plus RepeatVar(2) MORE gains if `WasDoomAppliedThisTurn` — a history
// query for a PowerReceivedEntry of DoomPower whose Applier is the player.
//
// The emulator gated on the player being at half HP or below and did 3 gains (4 upgraded).
// Wrong condition, wrong count: the card asks what you have DONE this turn, not how badly
// it is going.
public class DeathsDoorTests
{
    private const int DeathsDoor = 126;
    private const int Deathbringer = 124;

    private static Fight Fresh(bool upgraded = false)
    {
        return Fight.Hand(new CardInstance(DeathsDoor, upgraded)).Energy(9).Enemy(hp: 500);
    }

    [Fact]
    public void WithoutDoomItIsOneGainOfSix()
    {
        var fight = Fresh();

        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    /// <summary>Low HP is not the trigger — that was the emulator's invention.</summary>
    [Fact]
    public void LowHpDoesNotTriggerIt()
    {
        var fight = Fresh().PlayerHp(5, 80);

        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    [Fact]
    public void AfterApplyingDoomItIsThreeGains()
    {
        var fight = Fresh();
        fight.State.Hand.Insert(0, new CardInstance(Deathbringer, false));

        fight.Play();
        fight.Play();

        Assert.Equal(18, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedEachGainIsSeven()
    {
        var fight = Fresh(upgraded: true);
        fight.State.Hand.Insert(0, new CardInstance(Deathbringer, false));

        fight.Play();
        fight.Play();

        Assert.Equal(21, fight.State.PlayerBlock);
    }

    /// <summary>"This turn" — Doom applied last turn does not pay again.</summary>
    [Fact]
    public void DoomFromLastTurnDoesNotCount()
    {
        var fight = Fresh();
        fight.State.Hand.Insert(0, new CardInstance(Deathbringer, false));
        fight.Play();

        fight.EndTurn();
        fight.State.PlayerBlock = 0;
        fight.State.Hand.Insert(0, new CardInstance(DeathsDoor, false));
        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
    }
}
