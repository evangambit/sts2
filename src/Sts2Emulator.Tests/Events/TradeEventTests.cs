using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The three events that take something the run already owns and give something back.
///
/// They share a defect the fixtures cannot see. Each offers one option per thing it will
/// take, so WHICH thing an option index means is the whole content of the option -- and
/// the emulator answered that question positionally, by array slot, in every one of them.
/// The Relic Trader indexed <c>state.Relics</c> and skipped slot 0 to dodge Burning Blood;
/// Ranwid surrendered whatever sat in slot 1. Both would hand over an untradable relic,
/// and neither matched the relic the game had named on the option.
///
/// A live capture pins one run state, and a fresh run holds exactly one relic and no
/// potions -- so at the captured state most of these options are locked and prove nothing.
/// Everything here sets up the state that makes the option real.
/// </summary>
[CoversEvent("RanwidTheElder")]
[CoversEvent("RelicTrader")]
[CoversEvent("TheFutureOfPotions")]
public class TradeEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int Relic(string name) => RunNonCombatEffects.NamedRelic(name);

    private static int Potion(string name) => RunNonCombatEffects.NamedPotion(name);

    /// <summary>
    /// Three tradable relics on top of the untradable Burning Blood, OBTAINED rather than
    /// appended: picking a relic up strikes it from the grab bag, and a relic traded away
    /// is never returned to it (<c>RelicCmd.Remove</c> only removes it from the player).
    /// Appending straight to the list leaves it in the bag, so the trade can hand back the
    /// very relic just given up -- which the game cannot do.
    /// </summary>
    private static RunEngine WithTradableRelics(int eventId)
    {
        var engine = At(eventId);
        foreach (string name in new[] { "Akabeko", "Anchor", "BagOfMarbles" })
        {
            RunNonCombatEffects.ApplyRelicPickup(engine.State, Relic(name));
        }

        return engine;
    }

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    // ── The Relic Trader ─────────────────────────────────────────────────────

    /// <summary>
    /// The trader takes only relics the game will trade. Burning Blood is a Starter relic
    /// and must survive whatever the player picks -- indexing state.Relics by option would
    /// hand it over the moment the run held a second untradable relic.
    /// </summary>
    [Fact]
    public void TheTraderNeverTakesAnUntradableRelic()
    {
        int burningBlood = RunConstants.RelicBurningBlood;

        for (int option = 0; option < 3; option++)
        {
            var engine = WithTradableRelics(RunConstants.EventRelicTrader);
            Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));

            Assert.Contains(engine.State.Relics, relic => relic.DefId == burningBlood);
        }
    }

    [Fact]
    public void TheTraderTakesTheRelicItsOwnOptionNamed()
    {
        var probe = WithTradableRelics(RunConstants.EventRelicTrader);
        var stock = RunNonCombatEffects.RelicTraderStock(probe.State);
        Assert.Equal(3, stock.Count);

        for (int option = 0; option < stock.Count; option++)
        {
            var engine = WithTradableRelics(RunConstants.EventRelicTrader);
            int wanted = engine.State.Relics[stock[option]].DefId;
            Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));

            Assert.DoesNotContain(engine.State.Relics, relic => relic.DefId == wanted);
        }
    }

    [Fact]
    public void TradingKeepsTheRelicCountTheSame()
    {
        var engine = WithTradableRelics(RunConstants.EventRelicTrader);
        int before = engine.State.Relics.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(before, engine.State.Relics.Count);
    }

    /// <summary>
    /// Only as many options as the trader has stock, and a lone Proceed when it has none
    /// -- which is what a fresh run looks like, holding only Burning Blood.
    /// </summary>
    [Theory]
    [InlineData(0, new[] { 0 })]
    [InlineData(1, new[] { 0 })]
    [InlineData(2, new[] { 0, 1 })]
    [InlineData(3, new[] { 0, 1, 2 })]
    [InlineData(5, new[] { 0, 1, 2 })]
    public void TheTraderOffersOneOptionPerRelicItWillTake(int tradable, int[] expected)
    {
        var engine = At(RunConstants.EventRelicTrader);
        foreach (
            string name in new[]
            {
                "Akabeko",
                "Anchor",
                "BagOfMarbles",
                "Bellows",
                "BeltBuckle",
            }.Take(tradable)
        )
        {
            RunNonCombatEffects.ApplyRelicPickup(engine.State, Relic(name));
        }

        Assert.Equal(expected, Offered(engine));
    }

    [Fact]
    public void AFreshRunCanOnlyProceedPastTheTrader()
    {
        var engine = At(RunConstants.EventRelicTrader);
        Assert.Single(engine.State.Relics);

        Assert.Equal(new[] { 0 }, Offered(engine));
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Single(engine.State.Relics);
    }

    // ── Ranwid the Elder ─────────────────────────────────────────────────────

    /// <summary>
    /// Giving up a relic buys TWO back -- <c>for (i &lt; 2) Obtain(...)</c> -- which is the
    /// entire reason to take that option over the gold.
    /// </summary>
    [Fact]
    public void GivingRanwidARelicBuysTwo()
    {
        var engine = WithTradableRelics(RunConstants.EventRanwidTheElder);
        int before = engine.State.Relics.Count;

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.Equal(before - 1 + 2, engine.State.Relics.Count);
    }

    [Fact]
    public void RanwidTakesTheRelicHeNamedAndNotTheStarter()
    {
        var engine = WithTradableRelics(RunConstants.EventRanwidTheElder);
        int index = RunNonCombatEffects.RanwidTradeIndex(engine.State);
        Assert.True(index >= 0);
        int wanted = engine.State.Relics[index].DefId;

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.DoesNotContain(engine.State.Relics, relic => relic.DefId == wanted);
        Assert.Contains(
            engine.State.Relics,
            relic => relic.DefId == RunConstants.RelicBurningBlood
        );
    }

    /// <summary>
    /// The gold option is never locked and never priced: LoseGold floors at zero, so a
    /// run holding 99 hands over 99 and still gets its relic.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(500)]
    public void RanwidsGoldOptionIsAlwaysOpen(int gold)
    {
        var engine = At(RunConstants.EventRanwidTheElder);
        engine.State.Gold = gold;

        Assert.Contains(1, Offered(engine));
        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(Math.Max(0, gold - 100), engine.State.Gold);
        Assert.Equal(2, engine.State.Relics.Count);
    }

    [Fact]
    public void GivingRanwidAPotionSpendsItForOneRelic()
    {
        var engine = At(RunConstants.EventRanwidTheElder);
        engine.State.PotionSlots[0] = Potion("FoulPotion");

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.All(engine.State.PotionSlots, slot => Assert.Equal(0, slot));
        Assert.Equal(2, engine.State.Relics.Count);
    }

    /// <summary>A fresh run has no potion and nothing tradable, so only the gold is open.</summary>
    [Fact]
    public void AFreshRunCanOnlyPayRanwidInGold()
    {
        Assert.Equal(new[] { 1 }, Offered(At(RunConstants.EventRanwidTheElder)));
    }

    // ── The Future of Potions ────────────────────────────────────────────────

    private static RunEngine WithPotions(params string[] names)
    {
        var engine = At(RunConstants.EventTheFutureOfPotions);
        for (int i = 0; i < names.Length; i++)
        {
            engine.State.PotionSlots[i] = Potion(names[i]);
        }

        return engine;
    }

    [Fact]
    public void TradingAPotionOffersThreeUpgradedCards()
    {
        var engine = WithPotions("FoulPotion", "Ashwater");

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        Assert.Equal(3, engine.State.RewardCards.Count(card => card != 0));
        Assert.All(engine.State.RewardUpgraded, upgraded => Assert.True(upgraded));
    }

    /// <summary>
    /// Every card on the screen is one rarity and one type: the rarity the potion buys,
    /// and a type rolled once for that potion.
    /// </summary>
    [Fact]
    public void TheOfferedCardsShareTheRarityThePotionBuysAndOneType()
    {
        foreach (string potion in new[] { "Ashwater", "BeetleJuice", "AttackPotion" })
        {
            var engine = WithPotions(potion, "FoulPotion");
            var expected = RunRewardGenerator.CardRarityForPotion(Potion(potion));

            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

            var offered = engine
                .State.RewardCards.Where(card => card != 0)
                .Select(card => GeneratedData.Cards.Get(card))
                .ToList();
            Assert.NotEmpty(offered);
            Assert.All(offered, def => Assert.Equal(expected, def.Rarity));
            Assert.Single(offered.Select(def => def.Type).Distinct());
        }
    }

    /// <summary>
    /// A Common or Token potion cannot buy a Power, and the reason is in the pool rather
    /// than in taste: the Ironclad has no Common Power at all. Asserting on the cards that
    /// come OUT cannot see this -- with Power wrongly on the table, a Power roll filters
    /// the reward down to nothing, so no Power appears either way and the test passes
    /// while the player gets an empty screen. So the rule is checked directly, and its
    /// consequence separately.
    /// </summary>
    [Fact]
    public void ACommonPotionCannotRollAPower()
    {
        int attackPotion = Potion("AttackPotion");
        Assert.Equal(PotionRarity.Common, GeneratedData.Potions.Get(attackPotion).Rarity);

        Assert.Equal(
            new[] { CardType.Attack, CardType.Skill },
            RunRewardGenerator.FutureOfPotionsCardTypes(attackPotion)
        );
        Assert.Contains(
            CardType.Power,
            RunRewardGenerator.FutureOfPotionsCardTypes(Potion("Ashwater"))
        );
    }

    /// <summary>
    /// The consequence: a trade always fills the screen. This is what breaks if Power
    /// creeps back onto a Common potion's list, because there is nothing for it to offer.
    /// </summary>
    [Fact]
    public void ATradeAlwaysOffersAFullScreenOfCards()
    {
        foreach (string potion in new[] { "AttackPotion", "Ashwater", "BeetleJuice" })
        {
            for (int i = 0; i < 12; i++)
            {
                var engine = At(RunConstants.EventTheFutureOfPotions, $"SEED{i:D4}");
                engine.State.PotionSlots[0] = Potion(potion);
                engine.State.PotionSlots[1] = Potion("FoulPotion");

                Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

                Assert.Equal(3, engine.State.RewardCards.Count(card => card != 0));
            }
        }
    }

    [Fact]
    public void TheTradedPotionIsTheOneTheOptionNamed()
    {
        var engine = WithPotions("FoulPotion", "Ashwater");
        int second = engine.State.PotionSlots[1];

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.DoesNotContain(second, engine.State.PotionSlots);
        Assert.Contains(Potion("FoulPotion"), engine.State.PotionSlots);
    }

    [Theory]
    [InlineData(0, new[] { 0 })]
    [InlineData(1, new[] { 0 })]
    [InlineData(2, new[] { 0, 1 })]
    public void OneOptionPerPotionHeld(int held, int[] expected)
    {
        var engine = At(RunConstants.EventTheFutureOfPotions);
        for (int i = 0; i < held; i++)
        {
            engine.State.PotionSlots[i] = Potion("FoulPotion");
        }

        Assert.Equal(expected, Offered(engine));
    }
}
