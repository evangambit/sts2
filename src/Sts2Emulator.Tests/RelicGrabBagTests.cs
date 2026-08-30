using Sts2Emulator.Core;
using Sts2Emulator.Core.Rng;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The run's relic queues.
///
/// The emulator used to roll a relic uniformly from a flat pool on the UpFront stream,
/// filtered to relics the player did not already own. The game shuffles the pools into
/// per-rarity queues once, at run start, and pulls from the front -- so the mechanism, the
/// stream and the rarity distribution were all wrong, at every site that hands over a
/// relic: elites, events, Neow, chests and shops.
///
/// The ground truth for the whole thing is one number. Unrest Site's "Kill the Trees"
/// yields ORICHALCUM on seed ABCDEF at A8 in the live game, and that relic falls out of a
/// 126-relic pool bucketed by rarity, shuffled off a stream 230 draws deep, with a rarity
/// rolled separately -- so reproducing it is not something a near-miss does.
/// </summary>
public class RelicGrabBagTests
{
    private static RunEngine Run(string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        return engine;
    }

    private static string Name(int relicId) => GeneratedData.Relics.Get(relicId).Name;

    private static RelicRarity Rarity(int relicId) => GeneratedData.Relics.Get(relicId).Rarity;

    // ── Populate ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The player's bag drops every rarity outside the four reward rarities, so a Starter,
    /// Event or Ancient relic can never come out of a reward.
    /// </summary>
    [Fact]
    public void ThePlayersBagHoldsOnlyRewardRarities()
    {
        var state = Run().State;

        Assert.NotEmpty(state.RelicBag.Remaining);
        Assert.All(
            state.RelicBag.Remaining,
            relicId =>
                Assert.Contains(
                    Rarity(relicId),
                    new[]
                    {
                        RelicRarity.Common,
                        RelicRarity.Uncommon,
                        RelicRarity.Rare,
                        RelicRarity.Shop,
                    }
                )
        );
    }

    [Fact]
    public void ThePlayersBagIsTheSharedPoolPlusTheCharactersFilteredByRarity()
    {
        var state = Run().State;
        var expected = GeneratedData
            .RelicPools.Shared.ToArray()
            .Concat(GeneratedData.RelicPools.Ironclad.ToArray())
            .Where(relicId =>
                Rarity(relicId)
                    is RelicRarity.Common
                        or RelicRarity.Uncommon
                        or RelicRarity.Rare
                        or RelicRarity.Shop
            )
            // Massive Scroll is multiplayer-only; it is struck on the first pull, not at
            // populate time, so it is still in the bag here.
            .OrderBy(id => id)
            .ToList();

        Assert.Equal(expected, state.RelicBag.Remaining.OrderBy(id => id).ToList());
    }

    /// <summary>
    /// The shared bag keeps its pool as given, Event and Ancient rarities included, which
    /// is why the two bags consume different numbers of draws from the same stream.
    /// </summary>
    [Fact]
    public void TheSharedBagKeepsEveryRarity()
    {
        var state = Run().State;

        Assert.Equal(
            GeneratedData.RelicPools.Shared.Length,
            state.SharedRelicBag.Remaining.Count()
        );
        Assert.Contains(
            state.SharedRelicBag.Remaining,
            relicId => Rarity(relicId) == RelicRarity.Ancient
        );
        Assert.DoesNotContain(
            state.RelicBag.Remaining,
            relicId => Rarity(relicId) == RelicRarity.Ancient
        );
    }

    /// <summary>
    /// Populating both bags costs exactly the 230 UpFront draws that used to sit inside
    /// the opaque 232-draw prefix ahead of map generation -- 112 for the shared bag, 118
    /// for the player's. If this number moves, the map moves with it.
    /// </summary>
    [Fact]
    public void PopulatingBothBagsCostsTwoHundredAndThirtyDraws()
    {
        var rng = new GameRng(0u);

        var shared = new RelicGrabBag(refreshAllowed: true);
        shared.Populate(GeneratedData.RelicPools.Shared.ToArray(), rng, filterRarities: false);
        Assert.Equal(112, rng.CallCount);

        var player = new RelicGrabBag();
        player.Populate(
            [
                .. GeneratedData.RelicPools.Shared.ToArray(),
                .. GeneratedData.RelicPools.Ironclad.ToArray(),
            ],
            rng,
            filterRarities: true
        );
        Assert.Equal(230, rng.CallCount);
    }

    // ── Pulling ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The one live number this whole mechanism is answerable to.
    /// </summary>
    [Fact]
    public void KillingTheTreesYieldsTheRelicTheGameGave()
    {
        var engine = Run();
        engine.State.EventId = RunConstants.EventUnrestSite;
        engine.State.Phase = RunPhase.Event;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal("Orichalcum", Name(engine.State.Relics[^1].DefId));
    }

    [Fact]
    public void APulledRelicLeavesBothBagsAndNeverComesBack()
    {
        var state = Run().State;
        int relicId = RunRewardGenerator.NextRelic(state);

        Assert.DoesNotContain(relicId, state.RelicBag.Remaining);
        Assert.DoesNotContain(relicId, state.SharedRelicBag.Remaining);

        var seen = new HashSet<int> { relicId };
        for (int i = 0; i < 40; i++)
        {
            int next = RunRewardGenerator.NextRelic(state);
            Assert.True(seen.Add(next), $"{Name(next)} came out twice");
        }
    }

    /// <summary>
    /// A relic handed over by name -- the Sword of Stone, a Neow pick -- is out of
    /// circulation too, because RelicCmd.Obtain removes it from both bags.
    /// </summary>
    [Fact]
    public void ANamedRelicIsStruckFromTheBagsWhenObtained()
    {
        var state = Run().State;
        int sword = RunNonCombatEffects.NamedRelic("SwordOfStone");
        int akabeko = RunNonCombatEffects.NamedRelic("Akabeko");
        Assert.Contains(akabeko, state.RelicBag.Remaining);

        RunNonCombatEffects.ApplyRelicPickup(state, akabeko);
        Assert.DoesNotContain(akabeko, state.RelicBag.Remaining);

        // The Sword of Stone is an Event relic, so it was never in the player's bag.
        Assert.DoesNotContain(sword, state.RelicBag.Remaining);
        RunNonCombatEffects.ApplyRelicPickup(state, sword);
    }

    /// <summary>
    /// Circlet is the only stackable relic in the game, so obtaining it must not remove
    /// anything -- it is the fallback and a run can end up with several.
    /// </summary>
    [Fact]
    public void CircletIsStackableAndIsNotStruckFromTheBags()
    {
        var state = Run().State;
        int circlet = RunNonCombatEffects.CircletRelic;
        int before = state.SharedRelicBag.Remaining.Count();

        RunNonCombatEffects.ApplyRelicPickup(state, circlet);

        Assert.Equal(before, state.SharedRelicBag.Remaining.Count());
    }

    // ── Rarity ───────────────────────────────────────────────────────────────

    /// <summary>
    /// RelicFactory.RollRarity splits a single NextFloat at 0.5 and 0.83, so a long run of
    /// rewards is about half Common, a third Uncommon and a sixth Rare. The old uniform
    /// roll over a flat pool produced nothing like that -- the pool holds more Rares
    /// (38) than Commons (26), so it favoured exactly the wrong end.
    /// </summary>
    [Fact]
    public void RarityFollowsTheGamesSplitAndNotThePoolsShape()
    {
        var rng = new GameRng(12345u);
        var rolled = Enumerable.Range(0, 4000).Select(_ => RelicGrabBag.RollRarity(rng)).ToList();

        Assert.InRange(rolled.Count(r => r == RelicRarity.Common) / 4000.0, 0.47, 0.53);
        Assert.InRange(rolled.Count(r => r == RelicRarity.Uncommon) / 4000.0, 0.30, 0.36);
        Assert.InRange(rolled.Count(r => r == RelicRarity.Rare) / 4000.0, 0.14, 0.20);
        Assert.DoesNotContain(RelicRarity.Shop, rolled);
    }

    [Fact]
    public void RollingRarityCostsOneDraw()
    {
        var rng = new GameRng(1u);
        RelicGrabBag.RollRarity(rng);

        Assert.Equal(1, rng.CallCount);
    }

    // ── Escalation and filtering ─────────────────────────────────────────────

    /// <summary>
    /// An exhausted rarity escalates upward -- Shop to Common to Uncommon to Rare -- and
    /// only then gives up. It does not fall back to a re-roll.
    /// </summary>
    [Fact]
    public void AnExhaustedRarityEscalatesUpward()
    {
        var bag = new RelicGrabBag();
        var rng = new GameRng(7u);
        int akabeko = RunNonCombatEffects.NamedRelic("Akabeko");
        int rare = GeneratedData
            .RelicPools.Shared.ToArray()
            .First(id => Rarity(id) == RelicRarity.Rare);
        bag.Populate([akabeko, rare], rng, filterRarities: true);

        // Drain Common, then ask for Common again: the answer is the Rare.
        Assert.Equal(akabeko, bag.Pull(RelicRarity.Common, true, _ => true));
        Assert.Equal(rare, bag.Pull(RelicRarity.Common, true, _ => true));
        Assert.Null(bag.Pull(RelicRarity.Common, true, _ => true));
    }

    [Fact]
    public void AnEmptyBagFallsBackToCirclet()
    {
        var state = Run().State;
        while (state.RelicBag.Remaining.Any())
        {
            state.RelicBag.Remove(state.RelicBag.Remaining.First());
        }

        Assert.Equal(RunNonCombatEffects.CircletRelic, RunRewardGenerator.NextRelic(state));
    }

    /// <summary>
    /// Massive Scroll is multiplayer-only, so a solo run must never be offered it -- and
    /// the game strikes it on the first pull rather than at populate time.
    /// </summary>
    [Fact]
    public void MassiveScrollIsNeverHandedToASoloRun()
    {
        var state = Run().State;
        int scroll = RunNonCombatEffects.NamedRelic("MassiveScroll");

        for (int i = 0; i < 60 && state.RelicBag.Remaining.Any(); i++)
        {
            Assert.NotEqual(scroll, RunRewardGenerator.NextRelic(state));
        }

        Assert.DoesNotContain(scroll, state.RelicBag.Remaining);
    }

    /// <summary>
    /// The chest relics stop being eligible once a run passes floor 41
    /// (<c>IsBeforeAct3TreasureChest</c>), and once struck they do not come back.
    /// </summary>
    [Fact]
    public void ChestRelicsAreGoneAfterFloorFortyOne()
    {
        var early = Run().State;
        Assert.Contains(early.RelicBag.Remaining, id => Name(id) == "FrozenEgg");
        RunRewardGenerator.NextRelic(early);
        Assert.Contains(early.RelicBag.Remaining, id => Name(id) == "FrozenEgg");

        var late = Run().State;
        late.Floor = 41;
        RunRewardGenerator.NextRelic(late);

        Assert.DoesNotContain(late.RelicBag.Remaining, id => Name(id) == "FrozenEgg");
        Assert.DoesNotContain(late.RelicBag.Remaining, id => Name(id) == "Shovel");
    }

    /// <summary>
    /// EVERY relic that declares the gate, not the fourteen someone transcribed. The list
    /// used to live in `RelicGrabBag` and had drifted three short — Meal Ticket, Old Coin
    /// and White Beast Statue kept being offered past floor 41 — so the flag is extracted
    /// now and this asserts against the extracted set rather than a second copy of it.
    /// </summary>
    [Fact]
    public void EveryRelicDeclaringTheGateIsGoneAfterFloorFortyOne()
    {
        var gated = new List<int>();
        foreach (var def in GeneratedData.Relics.All)
        {
            if (def.StopsAfterAct3Chest)
            {
                gated.Add(def.Id);
            }
        }

        Assert.Equal(17, gated.Count);

        var late = Run().State;
        late.Floor = 41;
        RunRewardGenerator.NextRelic(late);

        Assert.Empty(late.RelicBag.Remaining.Intersect(gated));
    }

    /// <summary>The three the hand-kept list was missing, named so the regression is visible.</summary>
    [Theory]
    [InlineData("MealTicket")]
    [InlineData("OldCoin")]
    [InlineData("WhiteBeastStatue")]
    public void TheThreeThatWereMissingAreGatedToo(string name)
    {
        int id = GeneratedData.Relics.FindId(name) ?? throw new Xunit.Sdk.XunitException(name);
        Assert.True(GeneratedData.Relics.Get(id).StopsAfterAct3Chest);

        var late = Run().State;
        late.Floor = 41;
        RunRewardGenerator.NextRelic(late);

        Assert.DoesNotContain(late.RelicBag.Remaining, id => Name(id) == name);
    }

    // ── Shops ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shops ask for Shop rarity by name and read the queue from the BACK, so a shop and
    /// a reward drawing from the same bag never race for the same relic until it is
    /// nearly empty.
    /// </summary>
    [Fact]
    public void ShopsPullShopRarityFromTheBack()
    {
        var state = Run().State;
        var shopQueue = state
            .RelicBag.Remaining.Where(id => Rarity(id) == RelicRarity.Shop)
            .ToList();

        int pulled = RunRewardGenerator.NextShopRelic(state);

        Assert.Equal(RelicRarity.Shop, Rarity(pulled));
        Assert.Equal(shopQueue[^1], pulled);
    }

    [Fact]
    public void AShopPullDoesNotRollARarity()
    {
        var state = Run().State;
        int before = state.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.NextShopRelic(state);

        Assert.Equal(before, state.PlayerRng.Rewards.CallCount);
    }

    // ── The stream ───────────────────────────────────────────────────────────

    /// <summary>
    /// Rewards roll their rarity off the player's rewards stream, not off UpFront -- which
    /// is what the old NextRelic used, and using it there moved every later UpFront draw.
    /// </summary>
    [Fact]
    public void PullingReadsTheRewardsStreamAndLeavesUpFrontAlone()
    {
        var state = Run().State;
        int upFront = state.Rng.UpFront.CallCount;
        int rewards = state.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.NextRelic(state);

        Assert.Equal(upFront, state.Rng.UpFront.CallCount);
        Assert.Equal(rewards + 1, state.PlayerRng.Rewards.CallCount);
    }

    [Fact]
    public void TheSameSeedBuildsTheSameQueue()
    {
        Assert.Equal(
            Run("AAB").State.RelicBag.Remaining.ToList(),
            Run("AAB").State.RelicBag.Remaining.ToList()
        );
        Assert.NotEqual(
            Run("AAB").State.RelicBag.Remaining.ToList(),
            Run("ABCDEF").State.RelicBag.Remaining.ToList()
        );
    }
}
