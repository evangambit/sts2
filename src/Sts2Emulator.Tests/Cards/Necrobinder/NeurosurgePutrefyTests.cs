using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Neurosurge.cs: a 0-cost Power. Gain 3/4 energy, draw 2,
// and apply NeurosurgePower 3 to YOURSELF — a Debuff whose AfterSideTurnStart Dooms its
// owner for its amount every turn. `DoomPower.BeforeSideTurnEnd` kills whoever holds it
// when their HP is at or below it, and the player is not exempt.
//
// The emulator granted NoBlock: a different kind of bad, and one that never kills you.
public class NeurosurgeTests
{
    private const int Neurosurge = 322;

    private static Fight Played(bool upgraded = false, int hp = 60)
    {
        var fight = Fight.Hand().Energy(0).PlayerHp(hp, 80).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Neurosurge, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesEnergyAndCardsAndDebuffsYou()
    {
        var fight = Played();

        Assert.Equal(3, fight.State.Energy);
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Neurosurge));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.NoBlock));
    }

    [Fact]
    public void TheUpgradeGivesAFourthEnergyAndNoMoreDoom()
    {
        var fight = Played(upgraded: true);

        Assert.Equal(4, fight.State.Energy);
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Neurosurge));
    }

    [Fact]
    public void ItStacksDoomOnYouEveryTurn()
    {
        var fight = Played();
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Doom));

        fight.EndTurn();
        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Doom));

        fight.EndTurn();
        Assert.Equal(6, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Doom));
    }

    /// <summary>`IsOwnerDoomed` is `CurrentHp <= Amount`, and the owner here is you.</summary>
    [Fact]
    public void EnoughDoomKillsThePlayer()
    {
        var fight = Fight.Hand().Energy(9).PlayerHp(3, 80).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Doom, 3);

        var result = fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerHp);
        Assert.True(result.Terminal);
        Assert.False(result.PlayerWon);
    }

    [Fact]
    public void DoomBelowYourHpDoesNot()
    {
        var fight = Fight.Hand().Energy(9).PlayerHp(30, 80).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Doom, 3);

        fight.EndTurn();

        Assert.True(fight.State.PlayerHp > 0);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Putrefy.cs: one "Power" var at 2, upgrading by 1, spent
// on BOTH Weak and Vulnerable — and on `cardPlay.Target`, not on the room. The emulator had
// it at 1/2 and aimed at every enemy.
public class PutrefyTests
{
    private const int Putrefy = 373;

    [Fact]
    public void ItAppliesTwoOfEach()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Putrefy, false));

        fight.Play(0, target: 0);

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    [Fact]
    public void TheUpgradeAppliesThreeOfEach()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Putrefy, true));

        fight.Play(0, target: 0);

        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Weak));
        Assert.Equal(3, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }

    /// <summary>`cardPlay.Target`: the second enemy is untouched.</summary>
    [Fact]
    public void ItHitsOnlyTheTarget()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Putrefy, false));

        fight.Play(0, target: 0);

        Assert.Equal(2, fight.EnemyBuffAmount(BuffId.Weak, 0));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak, 1));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Vulnerable, 1));
    }
}
