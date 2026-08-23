using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Wongo's shop: three price tags, three different things behind them.
///
/// The emulator charged a flat 100 for all three -- selling the 300-gold mystery box at a
/// third of its price -- and handed over one relic rolled from the reward pool whichever
/// tier was bought. Each tier is its own pull: the bargain bin a shop-legal COMMON, the
/// featured item a shop-legal RARE named on the option when the shop opened, and the
/// mystery box Wongo's Mystery Ticket by name.
///
/// This is an Act 2 event (<c>IsAllowed</c> wants <c>CurrentActIndex == 1</c>) so no Act 1
/// run reaches it and no live capture covers it. The numbers come from the event's own
/// DynamicVars, and everything here drives the event directly.
/// </summary>
public class WelcomeToWongosTests
{
    private static RunEngine AtWongos(int gold = 999, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Gold = gold;
        engine.State.EventId = RunConstants.EventWelcomeToWongos;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static RelicDef Bought(RunEngine engine) =>
        GeneratedData.Relics.Get(engine.State.Relics[^1].DefId);

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 200)]
    [InlineData(2, 300)]
    public void EachTierChargesItsOwnPrice(int option, int price)
    {
        var engine = AtWongos();

        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));

        Assert.Equal(999 - price, engine.State.Gold);
        Assert.Equal(2, engine.State.Relics.Count);
    }

    [Theory]
    [InlineData(0, 99)]
    [InlineData(1, 199)]
    [InlineData(2, 299)]
    public void ATierIsLockedAndRefusedAPennyShort(int option, int gold)
    {
        var engine = AtWongos(gold);

        Assert.DoesNotContain(option, Offered(engine));
        Assert.Equal(-1, engine.Step(option, -1, out _, out _, out _));
        Assert.Equal(gold, engine.State.Gold);
        Assert.Single(engine.State.Relics);
    }

    [Fact]
    public void TheBargainBinSellsACommon()
    {
        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            var engine = AtWongos(seed: seed);
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

            Assert.Equal(RelicRarity.Common, Bought(engine).Rarity);
        }
    }

    [Fact]
    public void TheFeaturedItemIsARare()
    {
        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            var engine = AtWongos(seed: seed);
            Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

            Assert.Equal(RelicRarity.Rare, Bought(engine).Rarity);
        }
    }

    [Fact]
    public void TheMysteryBoxIsAlwaysTheTicket()
    {
        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP" })
        {
            var engine = AtWongos(seed: seed);
            Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

            Assert.Equal("WongosMysteryTicket", Bought(engine).Name);
        }
    }

    /// <summary>
    /// Wongo will not sell a relic the game keeps out of shops -- both pulls carry the
    /// <c>IsAllowedInShops</c> filter, which five relics fail.
    ///
    /// Checked by draining the queue down to the banned relic rather than by sweeping
    /// seeds: only five of a hundred and twenty-six relics are banned, so a sweep can miss
    /// every one of them and call the filter proved while it is not there at all. Amethyst
    /// Aubergine is the Common one and Old Coin the Rare, so each pull is aimed straight
    /// at the relic it must refuse.
    /// </summary>
    [Theory]
    [InlineData(0, "AmethystAubergine", RelicRarity.Common)]
    [InlineData(1, "OldCoin", RelicRarity.Rare)]
    public void WongoRefusesTheRelicsTheGameKeepsOutOfShops(
        int option,
        string banned,
        RelicRarity rarity
    )
    {
        var engine = AtWongos();
        int bannedId = RunNonCombatEffects.NamedRelic(banned);
        Assert.False(GeneratedData.Relics.Get(bannedId).IsAllowedInShops);

        // Leave the banned relic as the only one of its rarity in the queue: a filter that
        // is not applied would sell it, and one that is escalates past it.
        foreach (
            int relicId in engine
                .State.RelicBag.Remaining.Where(id =>
                    GeneratedData.Relics.Get(id).Rarity == rarity && id != bannedId
                )
                .ToList()
        )
        {
            engine.State.RelicBag.Remove(relicId);
        }

        Assert.Contains(bannedId, engine.State.RelicBag.Remaining);

        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));

        Assert.NotEqual(bannedId, engine.State.Relics[^1].DefId);
        Assert.True(Bought(engine).IsAllowedInShops, $"{Bought(engine).Name} is not sold in shops");
    }

    /// <summary>
    /// The featured item is pulled when the OPTIONS are generated, not when it is bought,
    /// because the option names it. So it is decided on entry and reading it twice must
    /// not pull twice.
    /// </summary>
    [Fact]
    public void TheFeaturedItemIsDecidedWhenTheShopOpens()
    {
        var engine = AtWongos();
        int featured = RunNonCombatEffects.WongosFeaturedItem(engine.State);

        Assert.Equal(featured, RunNonCombatEffects.WongosFeaturedItem(engine.State));

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(featured, engine.State.Relics[^1].DefId);
    }

    /// <summary>
    /// Buying earns Wongo points toward a customer badge, and those live in the PROFILE
    /// across runs (SaveManager.Progress.WongoPoints) rather than in the run -- so a run
    /// cannot know the total and the badge is not modelled. Pinned so the gap is visible
    /// rather than looking like an oversight.
    /// </summary>
    [Fact]
    public void TheCustomerBadgeIsNotModelled()
    {
        var engine = AtWongos();
        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.DoesNotContain(
            engine.State.Relics,
            relic => GeneratedData.Relics.Get(relic.DefId).Name == "WongoCustomerAppreciationBadge"
        );
    }
}
