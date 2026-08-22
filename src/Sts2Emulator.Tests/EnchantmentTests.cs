using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Enchantments ride on the card instance, so every path that reads a card's damage or
/// block has to include them. They used to be honoured only by the generated-card
/// approximation, which meant a hand-written case like Bash silently ignored a Sharp 2
/// the player had spent an event on.
///
/// A card carries at most ONE enchantment: <c>CardModel.Enchantment</c> is a single
/// reference and the base <c>CanEnchant</c> refuses any card that already has one. The
/// emulator used to carry Sharp, Nimble and Swift as three independent counters, which
/// modelled a card that could be all three at once -- reachable as soon as anything but
/// Self-Help Book started enchanting.
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

        var (sharp, sharpEnemy) = OneEnemy(new CardInstance(IC.Bash, false) { Enchantment = Enchantment.Sharp, EnchantAmount = 2 });
        CombatEngine.Step(sharp, 0, new Random(0));

        Assert.Equal(8, plainDamage);
        Assert.Equal(plainDamage + 2, 60 - sharpEnemy.Hp);
    }

    [Fact]
    public void Sharp_RidesOnTopOfTheUpgrade()
    {
        var (state, enemy) = OneEnemy(new CardInstance(IC.Bash, true) { Enchantment = Enchantment.Sharp, EnchantAmount = 2 });
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
        nimble.Hand = [new CardInstance(IC.DefendIronclad, false) { Enchantment = Enchantment.Nimble, EnchantAmount = 2 }];
        nimble.Energy = 3;
        nimble.PlayerBlock = 0;
        CombatEngine.Step(nimble, 0, new Random(0));

        Assert.Equal(plain.PlayerBlock + 2, nimble.PlayerBlock);
    }

    [Fact]
    public void Steady_RetainsACardTheHandWouldOtherwiseDiscard()
    {
        var plain = CombatFactory.NewCombat(seed: 0);
        plain.Hand = [new CardInstance(IC.Bash, false)];
        plain.Energy = 0;
        CombatEngine.Step(plain, plain.Hand.Count, new Random(0));
        Assert.DoesNotContain(plain.Hand, card => card.DefId == IC.Bash);

        var steady = CombatFactory.NewCombat(seed: 0);
        steady.Hand =
        [
            new CardInstance(IC.Bash, false)
            {
                Enchantment = Enchantment.Steady,
                EnchantAmount = 1,
            },
        ];
        steady.Energy = 0;
        CombatEngine.Step(steady, steady.Hand.Count, new Random(0));
        Assert.Contains(steady.Hand, card => card.DefId == IC.Bash);
    }

    [Fact]
    public void Spiral_PlaysTheCardOneExtraTime()
    {
        var (plain, plainEnemy) = OneEnemy(new CardInstance(IC.StrikeIronclad, false));
        CombatEngine.Step(plain, 0, new Random(0));
        int once = 60 - plainEnemy.Hp;

        var (spiral, spiralEnemy) = OneEnemy(
            new CardInstance(IC.StrikeIronclad, false)
            {
                Enchantment = Enchantment.Spiral,
                EnchantAmount = 1,
            }
        );
        CombatEngine.Step(spiral, 0, new Random(0));

        Assert.Equal(6, once);
        Assert.Equal(once * 2, 60 - spiralEnemy.Hp);
    }

    /// <summary>
    /// The game's <c>EnchantmentModel.CanEnchant</c>, which every event's option gating
    /// reads through. Status, Curse and Quest cards are never enchantable, an already
    /// enchanted card is refused, and each enchantment narrows it further.
    /// </summary>
    [Theory]
    [InlineData(IC.Bash, Enchantment.Sharp, true)]
    [InlineData(IC.DefendIronclad, Enchantment.Sharp, false)]
    [InlineData(IC.DefendIronclad, Enchantment.Nimble, true)]
    [InlineData(IC.Bash, Enchantment.Nimble, false)]
    [InlineData(IC.Bash, Enchantment.Corrupted, true)]
    [InlineData(IC.DefendIronclad, Enchantment.Corrupted, false)]
    // Spiral only takes a Basic Strike or Defend -- Bash is Basic but neither.
    [InlineData(IC.StrikeIronclad, Enchantment.Spiral, true)]
    [InlineData(IC.DefendIronclad, Enchantment.Spiral, true)]
    [InlineData(IC.Bash, Enchantment.Spiral, false)]
    [InlineData(IC.StrikeIronclad, Enchantment.Steady, true)]
    public void CanEnchant_MatchesTheEnchantmentsOwnRestriction(
        int cardId,
        Enchantment enchantment,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            Enchantments.CanEnchant(new CardInstance(cardId, false), enchantment)
        );
    }

    [Fact]
    public void AnAlreadyEnchantedCardTakesNoSecondEnchantment()
    {
        var card = new CardInstance(IC.StrikeIronclad, false)
        {
            Enchantment = Enchantment.Sharp,
            EnchantAmount = 2,
        };

        Assert.False(Enchantments.CanEnchant(card, Enchantment.Sharp));
        Assert.False(Enchantments.CanEnchant(card, Enchantment.Spiral));
        Assert.False(Enchantments.CanEnchant(card, Enchantment.Steady));
    }

    [Fact]
    public void StatusCurseAndQuestCardsAreNeverEnchantable()
    {
        foreach (var def in GeneratedData.Cards.All.ToArray())
        {
            if (def.Type is not (CardType.Status or CardType.Curse or CardType.Quest))
            {
                continue;
            }

            var card = new CardInstance(def.Id, false);
            foreach (Enchantment enchantment in Enum.GetValues<Enchantment>())
            {
                Assert.False(
                    Enchantments.CanEnchant(card, enchantment),
                    $"{def.Name} ({def.Type}) accepted {enchantment}"
                );
            }
        }
    }

    /// <summary>
    /// Pins the gap rather than hiding it: these four are recorded on the card and do
    /// nothing when it is played. Delete an entry from <c>InertInCombat</c> when it is
    /// modelled, and this test says which are left.
    /// </summary>
    [Fact]
    public void TheEnchantmentsThatDoNothingInCombatAreTheOnesDeclaredInert()
    {
        Assert.Equal(
            new[]
            {
                Enchantment.Swift,
                Enchantment.Sown,
                Enchantment.Corrupted,
                Enchantment.Slither,
            },
            Enchantments.InertInCombat
        );

        foreach (var enchantment in Enchantments.InertInCombat)
        {
            var (plain, plainEnemy) = OneEnemy(new CardInstance(IC.StrikeIronclad, false));
            CombatEngine.Step(plain, 0, new Random(0));

            var (enchanted, enchantedEnemy) = OneEnemy(
                new CardInstance(IC.StrikeIronclad, false)
                {
                    Enchantment = enchantment,
                    EnchantAmount = 1,
                }
            );
            CombatEngine.Step(enchanted, 0, new Random(0));

            Assert.Equal(plainEnemy.Hp, enchantedEnemy.Hp);
            Assert.Equal(plain.Energy, enchanted.Energy);
            Assert.Equal(plain.PlayerHp, enchanted.PlayerHp);
        }
    }
}
