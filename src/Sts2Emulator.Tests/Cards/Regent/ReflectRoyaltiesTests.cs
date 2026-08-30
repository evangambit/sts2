using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Reflect.cs: three stars for 15/20 block and one stack of
// ReflectPower. That power sends `result.BlockedDamage` back at whoever dealt a POWERED
// attack, as Unpowered damage, and decrements at its owner's side-turn START — so a Reflect
// covers the enemies' turn and is gone by the player's next one.
//
// The emulator granted Thorns 1: a flat point per hit whatever happened, where this pays
// exactly what the block stopped.
public class ReflectTests
{
    private const int Reflect = 390;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(Reflect, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItBlocksFifteenAndGrantsThePowerNotThorns()
    {
        var fight = Played();

        Assert.Equal(15, fight.State.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Reflect));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Thorns));
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeBlocksTwenty()
    {
        Assert.Equal(20, Played(upgraded: true).State.PlayerBlock);
    }

    /// <summary>
    /// Blocked damage comes back IN FULL — not one point of Thorns. Driven through
    /// `EnemyAI.ExecuteIntent` rather than EndTurn, because the Fight builder's punching
    /// bag has no intent of its own and never swings.
    /// </summary>
    [Fact]
    public void BlockedDamageGoesBackAtTheAttackerInFull()
    {
        var fight = Played();
        var attacker = new EnemyState
        {
            DefId = 16,
            Hp = 100,
            MaxHp = 100,
            CurrentIntent = new Intent(IntentType.Attack, 6),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(attacker, fight.State, new Random(0));

        // Six into fifteen block: all six stopped, all six returned.
        Assert.Equal(94, attacker.Hp);
        Assert.Equal(9, fight.State.PlayerBlock);
    }

    /// <summary>Only what the block STOPPED comes back, not the whole hit.</summary>
    [Fact]
    public void UnblockedDamageDoesNotComeBack()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Reflect, 1);
        var attacker = new EnemyState
        {
            DefId = 16,
            Hp = 100,
            MaxHp = 100,
            CurrentIntent = new Intent(IntentType.Attack, 6),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(attacker, fight.State, new Random(0));

        Assert.Equal(100, attacker.Hp);
    }

    /// <summary>It decrements at the player's turn start, so it covers one enemy turn.</summary>
    [Fact]
    public void ItIsGoneByThePlayersNextTurn()
    {
        var fight = Played();

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Reflect));
    }

    [Fact]
    public void WithoutItNothingComesBack()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        CardEffects.GainBlock(fight.State, 15);
        var attacker = new EnemyState
        {
            DefId = 16,
            Hp = 100,
            MaxHp = 100,
            CurrentIntent = new Intent(IntentType.Attack, 6),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(attacker, fight.State, new Random(0));

        Assert.Equal(100, attacker.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Royalties.cs: `GoldVar(30)` upgrading by 10, and
// `CanBeGeneratedInCombat => false`. `RoyaltiesPower.AfterCombatEnd` adds that much GOLD as
// its own reward row — the Heist's shape, claimed separately rather than folded into the
// fight's own gold. The emulator gave Strength.
public class RoyaltiesTests
{
    private const int Royalties = 403;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Royalties, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItOwesThirtyGoldAndNoStrength()
    {
        var fight = Played();

        Assert.Equal(30, fight.State.RoyaltiesGold);
        Assert.Equal(30, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Royalties));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TheUpgradeOwesForty()
    {
        Assert.Equal(40, Played(upgraded: true).State.RoyaltiesGold);
    }

    /// <summary>Two copies owe both, because the power stacks.</summary>
    [Fact]
    public void TwoCopiesOweBoth()
    {
        var fight = Played();
        fight.State.Hand.Add(new CardInstance(Royalties, false));
        fight.Play(0);

        Assert.Equal(60, fight.State.RoyaltiesGold);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Resonance.cs: three stars for +1/+2 Strength to the PLAYER
// and -1 to every living enemy. The enemy side is a flat 1 and does not upgrade; the
// emulator applied the player's half only.
public class ResonanceTests
{
    private const int Resonance = 395;

    private static Fight TwoEnemies()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Stars = 3;
        return fight;
    }

    [Fact]
    public void ThePlayerGainsAndEveryEnemyLoses()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(Resonance, false));

        fight.Play(0, target: 0);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 1));
    }

    /// <summary>Only the player's half upgrades.</summary>
    [Fact]
    public void TheUpgradeRaisesOnlyThePlayersHalf()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(Resonance, true));

        fight.Play(0, target: 0);

        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }

    /// <summary>Not temporary: the enemies do not get it back.</summary>
    [Fact]
    public void TheEnemiesDoNotGetItBack()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(Resonance, false));
        fight.Play(0, target: 0);

        fight.EndTurn();

        Assert.Equal(-1, fight.EnemyBuffAmount(BuffId.Strength, 0));
    }
}
