using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Cards whose damage is a calculation rather than a printed number reach the same
/// <c>DamageCmd.Attack</c> as any other attack — a <c>CalculatedDamageVar</c> instead of a
/// <c>DamageVar</c>, and nothing else different. So every enchantment and relic damage
/// bonus applies to them.
///
/// Seventeen of them were handing their computed total straight to <c>DealDamage</c>,
/// which skipped all of it. A Sharp Body Slam hit for its block and not a point more.
/// </summary>
public class CalculatedDamageTests
{
    private static CardInstance Sharp(int defId, bool upgraded = false) =>
        new CardInstance(defId, upgraded) with
        {
            Enchantment = Enchantment.Sharp,
            EnchantAmount = 5,
        };

    [Fact]
    public void SharpRaisesBodySlam()
    {
        var fight = Fight.Hand(Sharp(IC.BodySlam)).Energy(3).Enemy(hp: 80);
        fight.State.PlayerBlock = 10;

        fight.Play(0);

        // 10 of block, plus the enchantment's 5.
        Assert.Equal(80 - 15, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithoutTheEnchantmentBodySlamIsJustTheBlock()
    {
        var fight = Fight.Hand(new CardInstance(IC.BodySlam, false)).Energy(3).Enemy(hp: 80);
        fight.State.PlayerBlock = 10;

        fight.Play(0);

        Assert.Equal(70, fight.Enemy0.Hp);
    }

    [Fact]
    public void SharpRaisesAshenStrike()
    {
        var fight = Fight.Hand(Sharp(IC.AshenStrike)).Energy(3).Enemy(hp: 80);
        fight.State.ExhaustPile.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.State.ExhaustPile.Add(new CardInstance(IC.StrikeIronclad, false));

        fight.Play(0);

        // 6 base + 2 exhausted x 3, plus the enchantment's 5.
        Assert.Equal(80 - (6 + 6 + 5), fight.Enemy0.Hp);
    }

    /// <summary>
    /// Corrupted is multiplicative and applies to the whole computed total, not to some
    /// printed base the card does not have.
    /// </summary>
    [Fact]
    public void CorruptedMultipliesTheWholeCalculation()
    {
        var card = new CardInstance(IC.BodySlam, false) with
        {
            Enchantment = Enchantment.Corrupted,
            EnchantAmount = 1,
        };
        var fight = Fight.Hand(card).Energy(3).Enemy(hp: 80);
        fight.State.PlayerBlock = 20;

        fight.Play(0);

        Assert.Equal(80 - 30, fight.Enemy0.Hp);
    }

    /// <summary>Strength is separate and still lands — it is applied downstream in DealDamageToEnemy.</summary>
    [Fact]
    public void StrengthStillAppliesOnTopOfTheEnchantment()
    {
        var fight = Fight.Hand(Sharp(IC.BodySlam)).Energy(3).Enemy(hp: 80);
        fight.State.PlayerBlock = 10;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 3);

        fight.Play(0);

        Assert.Equal(80 - (10 + 5 + 3), fight.Enemy0.Hp);
    }

    /// <summary>
    /// Corrupted's own 2 damage is `Unblockable | Unpowered`, so it does NOT come out of
    /// block — it went through `DealDamageToPlayer`, which spends block first, and a
    /// blocking player paid the enchantment's price twice: once in HP they should have
    /// lost and again in a Body Slam that then hit for less.
    /// </summary>
    [Fact]
    public void CorruptedsSelfDamageDoesNotTouchBlock()
    {
        var card = new CardInstance(IC.BodySlam, false) with
        {
            Enchantment = Enchantment.Corrupted,
            EnchantAmount = 1,
        };
        var fight = Fight.Hand(card).Energy(3).Enemy(hp: 80);
        fight.State.PlayerBlock = 20;
        fight.State.PlayerHp = 60;

        fight.Play(0);

        Assert.Equal(20, fight.State.PlayerBlock);
        Assert.Equal(58, fight.State.PlayerHp);
    }
}
