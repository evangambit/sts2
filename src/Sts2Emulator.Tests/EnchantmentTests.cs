using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Self-Help Book's enchantments ride on the card instance, so every path that reads a
/// card's damage or block has to include them. They used to be honoured only by the
/// generated-card approximation, which meant a hand-written case like Bash silently
/// ignored a Sharp 2 the player had spent an event on.
/// </summary>
public class EnchantmentTests
{
    private static (CombatState State, EnemyState Enemy) OneEnemy(CardInstance card)
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [card];
        state.Energy = 3;
        var enemy = state.Enemies[0];
        enemy.Hp = 60;
        enemy.MaxHp = 60;
        enemy.Block = 0;
        return (state, enemy);
    }

    [Fact]
    public void Sharp_AddsToAHandWrittenAttacksDamage()
    {
        var (plain, plainEnemy) = OneEnemy(new CardInstance(IC.Bash, false));
        CombatEngine.Step(plain, 0, new Random(0));
        int plainDamage = 60 - plainEnemy.Hp;

        var (sharp, sharpEnemy) = OneEnemy(new CardInstance(IC.Bash, false) { Sharp = 2 });
        CombatEngine.Step(sharp, 0, new Random(0));

        Assert.Equal(8, plainDamage);
        Assert.Equal(plainDamage + 2, 60 - sharpEnemy.Hp);
    }

    [Fact]
    public void Sharp_RidesOnTopOfTheUpgrade()
    {
        var (state, enemy) = OneEnemy(new CardInstance(IC.Bash, true) { Sharp = 2 });
        CombatEngine.Step(state, 0, new Random(0));

        // Bash is 8 damage, +2 upgraded, +2 Sharp.
        Assert.Equal(12, 60 - enemy.Hp);
    }

    [Fact]
    public void Nimble_AddsToAHandWrittenSkillsBlock()
    {
        var plain = CombatFactory.NewCombat(seed: 0);
        plain.Hand = [new CardInstance(IC.DefendIronclad, false)];
        plain.Energy = 3;
        plain.PlayerBlock = 0;
        CombatEngine.Step(plain, 0, new Random(0));

        var nimble = CombatFactory.NewCombat(seed: 0);
        nimble.Hand = [new CardInstance(IC.DefendIronclad, false) { Nimble = 2 }];
        nimble.Energy = 3;
        nimble.PlayerBlock = 0;
        CombatEngine.Step(nimble, 0, new Random(0));

        Assert.Equal(plain.PlayerBlock + 2, nimble.PlayerBlock);
    }
}
