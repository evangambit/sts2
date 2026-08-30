using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Friendship.cs: `PowerCmd.Apply<StrengthPower>(...,
// -StrengthPower.BaseValue)` — the card COSTS 2 Strength, and its upgrade is
// `UpgradeValueBy(-1)` on the var it then negates, so an upgraded Friendship costs LESS.
// Then FriendshipPower 1, whose ModifyMaxEnergy is +1 every turn for the rest of the
// combat.
//
// The emulator GAINED Strength and gave a one-shot energy: wrong sign on one half, wrong
// duration on the other.
public class FriendshipTests
{
    private const int Friendship = 207;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Friendship, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItCostsTwoStrength()
    {
        Assert.Equal(-2, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>The upgrade makes it cost less, not gain more.</summary>
    [Fact]
    public void TheUpgradeCostsOnlyOne()
    {
        Assert.Equal(-1, BuffSystem.Get(Played(true).State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void ItRaisesMaxEnergyEveryTurn()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Energy;

        var fight = Played();
        fight.EndTurn();
        Assert.Equal(plain + 1, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(plain + 1, fight.State.Energy);
    }

    [Fact]
    public void ItGrantsNoOneShotEnergy()
    {
        Assert.Equal(0, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.NextTurnEnergy));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Invoke.cs: SummonVar 2 and EnergyVar 2, both upgrading
// by 1, applied as SummonNextTurnPower and EnergyNextTurnPower. Next turn brings the
// energy AND a summon; the emulator had the energy right and granted Crimson Mantle block
// for the other half.
public class InvokeTests
{
    private const int Invoke = 267;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        CardEffects.SummonOsty(fight.State, 1);
        fight.State.Hand.Add(new CardInstance(Invoke, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItSummonsAtTheStartOfNextTurn()
    {
        var fight = Played();
        int before = fight.State.OstyMaxHp;

        fight.EndTurn();

        Assert.Equal(before + 2, fight.State.OstyMaxHp);
    }

    [Fact]
    public void TheUpgradeSummonsForThree()
    {
        var fight = Played(upgraded: true);
        int before = fight.State.OstyMaxHp;

        fight.EndTurn();

        Assert.Equal(before + 3, fight.State.OstyMaxHp);
    }

    /// <summary>`PowerCmd.Remove(this)` after it fires — exactly once.</summary>
    [Fact]
    public void ItSummonsOnlyOnce()
    {
        var fight = Played();
        fight.EndTurn();
        int afterFirst = fight.State.OstyMaxHp;

        fight.EndTurn();

        Assert.Equal(afterFirst, fight.State.OstyMaxHp);
    }

    [Fact]
    public void ItStillBringsTheEnergy()
    {
        Assert.Equal(2, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.NextTurnEnergy));
    }

    [Fact]
    public void ItGrantsNoCrimsonMantle()
    {
        Assert.Equal(0, BuffSystem.Get(Played().State.PlayerBuffs, BuffId.CrimsonMantleBlock));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/EnfeeblingTouch.cs: an Ethereal 1-cost Skill whose
// "StrengthLoss" var is 8, upgrading by 3, applied to the TARGET as an
// EnfeeblingTouchPower — a TemporaryStrengthPower with `IsPositive => false`. The shape
// was right in the emulator and only the numbers were invented: 3 and 6 for 8 and 11.
public class EnfeeblingTouchTests
{
    private const int EnfeeblingTouch = 164;

    /// <summary>
    /// The debuff is stored as the amount OWED BACK, so it is positive while the Strength
    /// it took is negative — the pair is what the end-of-turn hand-back reads.
    /// </summary>
    [Fact]
    public void ItTakesEightStrength()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(EnfeeblingTouch, false));

        fight.Play(0, target: 0);

        Assert.Equal(-8, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(8, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    [Fact]
    public void TheUpgradeTakesEleven()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(EnfeeblingTouch, true));

        fight.Play(0, target: 0);

        Assert.Equal(-11, fight.EnemyBuffAmount(BuffId.Strength));
        Assert.Equal(11, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
    }

    /// <summary>Temporary: the enemy has it back once their turn is over.</summary>
    [Fact]
    public void TheEnemyGetsItBackAfterTheirTurn()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(EnfeeblingTouch, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.TemporaryStrength));
        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Strength));
    }
}
