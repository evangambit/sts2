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

        var (sharp, sharpEnemy) = OneEnemy(
            new CardInstance(IC.Bash, false) { Enchantment = Enchantment.Sharp, EnchantAmount = 2 }
        );
        CombatEngine.Step(sharp, 0, new Random(0));

        Assert.Equal(8, plainDamage);
        Assert.Equal(plainDamage + 2, 60 - sharpEnemy.Hp);
    }

    [Fact]
    public void Sharp_RidesOnTopOfTheUpgrade()
    {
        var (state, enemy) = OneEnemy(
            new CardInstance(IC.Bash, true) { Enchantment = Enchantment.Sharp, EnchantAmount = 2 }
        );
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
        nimble.Hand =
        [
            new CardInstance(IC.DefendIronclad, false)
            {
                Enchantment = Enchantment.Nimble,
                EnchantAmount = 2,
            },
        ];
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
    /// Every enchantment does something in combat now. The list is kept so the next
    /// unmodelled one has somewhere to be declared, and this test says when it is empty.
    /// </summary>
    [Fact]
    public void NoEnchantmentIsInertAnyMore()
    {
        Assert.Empty(Enchantments.InertInCombat);
    }

    /// <summary>
    /// Sown gains its amount of energy the first time the card is played and then stops.
    /// </summary>
    [Fact]
    public void Sown_GainsEnergyOnceAndThenStops()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false)
            {
                Enchantment = Enchantment.Sown,
                EnchantAmount = 1,
            },
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));
        // Paid 1 for the Strike and gained 1 back.
        Assert.Equal(3, state.Energy);

        // The same copy, played again, gains nothing.
        var spent = state.DiscardPile.First(card => card.Enchantment == Enchantment.Sown);
        Assert.True(spent.EnchantSpent);
    }

    /// <summary>Swift draws its amount the first time the card is played, once.</summary>
    [Fact]
    public void Swift_DrawsOnceWhenTheCardIsPlayed()
    {
        var plain = CombatFactory.NewCombat(seed: 0);
        plain.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        plain.Energy = 3;
        CombatEngine.Step(plain, 0, new Random(0));
        int drawnPlain = plain.Hand.Count;

        var swift = CombatFactory.NewCombat(seed: 0);
        swift.Hand =
        [
            new CardInstance(IC.StrikeIronclad, false)
            {
                Enchantment = Enchantment.Swift,
                EnchantAmount = 2,
            },
        ];
        swift.Energy = 3;
        CombatEngine.Step(swift, 0, new Random(0));

        Assert.Equal(drawnPlain + 2, swift.Hand.Count);
    }

    /// <summary>
    /// Vigorous adds its amount to the first powered attack and then disables itself.
    /// </summary>
    [Fact]
    public void Vigorous_AddsToTheFirstAttackOnly()
    {
        var (plain, plainEnemy) = OneEnemy(new CardInstance(IC.Bash, false));
        CombatEngine.Step(plain, 0, new Random(0));
        int plainDamage = 60 - plainEnemy.Hp;

        var (vigorous, vigorousEnemy) = OneEnemy(
            new CardInstance(IC.Bash, false)
            {
                Enchantment = Enchantment.Vigorous,
                EnchantAmount = 8,
            }
        );
        CombatEngine.Step(vigorous, 0, new Random(0));

        Assert.Equal(plainDamage + 8, 60 - vigorousEnemy.Hp);
        Assert.True(
            vigorous.DiscardPile.First(card => card.DefId == IC.Bash).EnchantSpent,
            "Vigorous should disable itself after the attack"
        );
    }

    /// <summary>
    /// Corrupted multiplies a powered attack by 1.5 and costs 2 HP every time the card is
    /// played -- it has no once-only status.
    /// </summary>
    [Fact]
    public void Corrupted_HitsHarderAndCostsHpEveryPlay()
    {
        var (plain, plainEnemy) = OneEnemy(new CardInstance(IC.Bash, false));
        CombatEngine.Step(plain, 0, new Random(0));
        int plainDamage = 60 - plainEnemy.Hp;

        var (corrupted, corruptedEnemy) = OneEnemy(
            new CardInstance(IC.Bash, false)
            {
                Enchantment = Enchantment.Corrupted,
                EnchantAmount = 1,
            }
        );
        int hp = corrupted.PlayerHp;
        CombatEngine.Step(corrupted, 0, new Random(0));

        Assert.Equal((int)(plainDamage * 1.5m), 60 - corruptedEnemy.Hp);
        Assert.Equal(hp - 2, corrupted.PlayerHp);
    }

    /// <summary>
    /// Slither re-rolls the card's cost every time it is drawn to hand, to 0..3.
    /// </summary>
    [Fact]
    public void Slither_RerollsItsCostWhenDrawn()
    {
        var costs = new HashSet<int>();
        for (int seed = 0; seed < 20; seed++)
        {
            var state = CombatFactory.NewCombat(seed: seed);
            state.Hand = [];
            state.DrawPile =
            [
                new CardInstance(IC.Bash, false)
                {
                    Enchantment = Enchantment.Slither,
                    EnchantAmount = 1,
                },
            ];
            CardEffects.DrawCards(state, 1, new Random(seed));

            Assert.Single(state.Hand);
            int cost = CombatEngine.EffectiveCost(state.Hand[0], state);
            Assert.InRange(cost, 0, 3);
            costs.Add(cost);
        }

        Assert.True(costs.Count > 1, $"Slither always rolled {string.Join(",", costs)}");
    }
}
