using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Rng;
using Sts2Emulator.Core.Run;
using Sts2Emulator.Interop;
using Xunit;

namespace Sts2Emulator.Tests;

public class RunEngineTests
{
    [Fact]
    public void DeterministicHash_MatchesPythonPinnedValues()
    {
        Assert.Equal(-842352754, DeterministicHash.GetDeterministicHashCode("0"));
        Assert.Equal(348630327, DeterministicHash.GetDeterministicHashCode("NEOW"));
        Assert.Equal(-1986686621, DeterministicHash.GetDeterministicHashCode("shuffle"));
        Assert.Equal(1703902611, DeterministicHash.GetDeterministicHashCode("monster_ai"));
    }



    [Fact]
    public void GameRng_HelperOutputsAreLocked()
    {
        // Regression lock over the MegaRandom (Xoshiro256**) port, NOT ground truth:
        // these are this implementation's own outputs. The only value here pinned
        // against the real game is the live-capture check in
        // MegaRandomHypothesisTests. Re-pinned when the RNG was corrected from
        // .NET's legacy Random to the game's MegaRandom.
        var ints = new GameRng(123, "shuffle");
        Assert.Equal(
            new[] { 9, 1, 6, 9, 2 },
            Enumerable.Range(0, 5).Select(_ => ints.NextInt(10)).ToArray()
        );

        var bools = new GameRng(123, "shuffle");
        Assert.Equal(
            new[] { false, true, false, false, true },
            Enumerable.Range(0, 5).Select(_ => bools.NextBool()).ToArray()
        );

        var item = new GameRng(123, "shuffle");
        Assert.Equal(50, item.NextItem(new[] { 10, 20, 30, 40, 50 }));

        var shuffle = new GameRng(123, "shuffle");
        var shuffled = Enumerable.Range(0, 10).ToList();
        shuffle.Shuffle(shuffled);
        Assert.Equal(new[] { 0, 4, 2, 7, 3, 8, 6, 5, 1, 9 }, shuffled);
        Assert.Equal(9, shuffle.CallCount);

        var stable = new GameRng(123, "shuffle");
        var stableShuffled = new List<int> { 3, 1, 2, 5, 4 };
        stable.StableShuffle(stableShuffled, Comparer<int>.Default);
        Assert.Equal(new[] { 4, 3, 2, 1, 5 }, stableShuffled);

        var gaussian = new GameRng(123, "niche");
        Assert.Equal(
            new[] { 52, 42, 55, 61, 40 },
            Enumerable.Range(0, 5).Select(_ => gaussian.NextGaussianInt(50, 10, 30, 70)).ToArray()
        );
        Assert.Equal(10, gaussian.CallCount);
    }

    [Fact]
    public void RunRngSet_NamedStreamOutputsAreLocked()
    {
        var rng = new RunRngSet("0");

        Assert.Equal(3452614542u, rng.Seed);
        Assert.Equal(1278256123, rng.UpFront.NextInt(int.MaxValue));
        Assert.Equal(1626764238, rng.Shuffle.NextInt(int.MaxValue));
        Assert.Equal(445936266, rng.UnknownMapPoint.NextInt(int.MaxValue));
        Assert.Equal(511159123, rng.CombatCardGeneration.NextInt(int.MaxValue));
        Assert.Equal(1929685146, rng.CombatPotionGeneration.NextInt(int.MaxValue));
        Assert.Equal(1069254844, rng.CombatCardSelection.NextInt(int.MaxValue));
        Assert.Equal(68763658, rng.CombatEnergyCosts.NextInt(int.MaxValue));
        Assert.Equal(1203427389, rng.CombatTargets.NextInt(int.MaxValue));
        Assert.Equal(1845763343, rng.MonsterAi.NextInt(int.MaxValue));
        Assert.Equal(2129060231, rng.Niche.NextInt(int.MaxValue));
        Assert.Equal(1926858856, rng.CombatOrbs.NextInt(int.MaxValue));
        Assert.Equal(1577988061, rng.TreasureRoomRelics.NextInt(int.MaxValue));
    }

    [Fact]
    public void RunRngSet_DerivesGameSeedForStringSeed()
    {
        // Captured from a live v0.107.1 custom run: input seed "ABCDEF" -> the game's
        // per-player rng seed 3334281563 (netId 0, so run seed == player seed). This
        // pins the string->seed derivation against the real game for a non-trivial seed.
        Assert.Equal(3334281563u, new RunRngSet("ABCDEF").Seed);
    }

    [Fact]
    public void RunRngSet_FreshSpecialStreamOutputsAreLocked()
    {
        var rng = new RunRngSet("0");
        var actMap = rng.ActMapRng();
        var neow = rng.NeowRng();
        var player = new PlayerRngSet(rng);

        Assert.Equal(
            new[] { 695, 229, 947, 783, 562 },
            Enumerable.Range(0, 5).Select(_ => actMap.NextInt(1000)).ToArray()
        );
        Assert.Equal(
            new[] { 422, 952, 934, 172, 499 },
            Enumerable.Range(0, 5).Select(_ => neow.NextInt(1000)).ToArray()
        );
        Assert.Equal(1821698096, player.Rewards.NextInt(int.MaxValue));
        Assert.Equal(1625900047, player.Shops.NextInt(int.MaxValue));
        Assert.Equal(1805565444, player.Transformations.NextInt(int.MaxValue));
    }

    [Fact]
    public void RunGeneration_MatchesLiveCaptureForAbcdef()
    {
        // Ground truth: a live v0.107.1 run seeded "ABCDEF" at A8, read out of its
        // current_run.save (acts[0].rooms). Unlike the RNG value pins elsewhere in
        // this file, these are real game outputs — keep them.
        // Reproduce with: python scripts/verify_run_generation.py
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        var s = engine.State;

        Assert.Equal(RunConstants.ActOvergrowth, s.Act);

        // ShrinkerBeetle, FuzzyWurmCrawler, Slimes, Inklets, Nibbits,
        // SlitheringStrangler, OvergrowthCrawlers, VineShambler, RubyRaiders,
        // CubexConstruct, Mawler, SlimesNormal, Fogmog, Flyconid, SnappingJaxfruit
        Assert.Equal(
            new[] { 11, 8, 3, 5, 15, 27, 21, 20, 28, 19, 14, 16, 29, 17, 18 },
            s.NormalEncounterSequence
        );

        // BygoneEffigy(62) / Byrdonis(68) / PhrogParasite(65), drawn from a bag that
        // refills every 3 and never repeats the previous elite.
        Assert.Equal(
            new[] { 62, 68, 65, 62, 65, 68, 62, 68, 65, 68, 62, 65, 62, 65, 68 },
            s.EliteEncounterSequence
        );

        // TheKin — rolled after the elites on the same stream, so this also guards
        // the elite draw count.
        Assert.Equal(82, s.BossEncounterId);

        // Map, from the same save's saved_map: 64 points including start and boss,
        // with these per-row counts. Guards the whole generate/assign/prune/
        // post-process pipeline — reproduce the full column-and-type diff with
        // scripts/verify_run_generation.py.
        Assert.Equal(64, s.MapNodes.Count);
        Assert.Equal(
            new[] { 1, 3, 4, 3, 3, 4, 5, 5, 3, 5, 5, 3, 5, 4, 5, 5, 1 },
            Enumerable
                .Range(0, RunConstants.MapBossRow + 1)
                .Select(row => s.MapNodes.Values.Count(n => n.Row == row))
                .ToArray()
        );

        // The point-type budget the map is built to. NumOfElites is 8 (the game's
        // round(5 * 1.6) with SwarmingElites), not 5 — assignment and post-prune
        // repair used to disagree about this, which left the map under-pruned.
        Assert.Equal(
            RunConstants.MapEliteCount,
            s.MapNodes.Values.Count(n => n.NodeType == RunConstants.NodeElite)
        );
        Assert.Equal(
            RunConstants.MapShopCount,
            s.MapNodes.Values.Count(n => n.NodeType == RunConstants.NodeShop)
        );
    }

    [Fact]
    public void RunGeneration_MatchesLiveCaptureForAab()
    {
        // Second live capture, seed "AAB" at A8 — a different seed on the same profile.
        // This one caught three real defects that a single sample could not: act
        // selection was a coin flip on the wrong stream, NibbitsNormal was wrongly
        // tagged, and the map stream was keyed on act identity rather than act index.
        var engine = new RunEngine();
        engine.Reset("AAB");
        var s = engine.State;

        Assert.Equal(RunConstants.ActOvergrowth, s.Act);

        // FuzzyWurmCrawlerWeak, SlimesWeak, NibbitsWeak, NibbitsNormal, Fogmog,
        // SlimesNormal, OvergrowthCrawlers, Mawler, VineShambler, Inklets, Flyconid,
        // SlitheringStrangler, SnappingJaxfruit, CubexConstruct, RubyRaiders.
        // Note weak->normal Nibbits back to back: only NibbitsWeak carries the Nibbit
        // tag in the game, so the no-repeat rule does not block it.
        Assert.Equal(
            new[] { 8, 3, 2, 15, 29, 16, 21, 14, 20, 5, 17, 27, 18, 19, 28 },
            s.NormalEncounterSequence
        );

        // Boss is CeremonialBeast for this seed, not TheKin as for "ABCDEF".
        Assert.Equal(74, s.BossEncounterId);

        // Map matches the live saved_map exactly — every row, column and point type
        // (61 nodes incl. start + boss). The full structural comparison lives in
        // tests/python/test_live_fixtures.py against the committed capture; this pins
        // the node count and per-row shape here too.
        Assert.Equal(61, s.MapNodes.Count);
        Assert.Equal(
            new[] { 1, 3, 5, 4, 3, 2, 3, 5, 6, 5, 5, 3, 5, 4, 3, 3, 1 },
            Enumerable
                .Range(0, RunConstants.MapBossRow + 1)
                .Select(row => s.MapNodes.Values.Count(n => n.Row == row))
                .ToArray()
        );
    }

    [Fact]
    public void RunReset_StartsAtAncientPhaseWithStarterState()
    {
        var engine = new RunEngine();

        engine.Reset("0");

        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);
        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(11, engine.State.Deck.Count);
        Assert.Equal(new[] { 140, 242, 134 }, engine.State.NeowOptions);
    }

    [Fact]
    public void RunObservation_UsesCurrentRunExtraLayout()
    {
        var engine = new RunEngine();
        var obs = new int[RunConstants.RunObsSize];

        engine.Reset("0");
        engine.State.Phase = RunPhase.Shop;
        engine.State.Floor = 7;
        engine.State.Act = RunConstants.ActUnderdocks;
        engine.State.Deck =
        [
            new CardInstance(1, false),
            new CardInstance(2, false),
            new CardInstance(3, true),
        ];
        engine.State.Gold = 123;
        engine.State.PlayerHp = 55;
        engine.State.PlayerMaxHp = 77;
        engine.State.Relics = [new RelicInstance(10), new RelicInstance(20)];
        engine.State.CurrentNodeType = RunConstants.NodeShop;
        engine.State.RewardCards = [101, 102, 103];
        engine.State.MapNodeTypes =
        [
            RunConstants.NodeNormal,
            RunConstants.NodeElite,
            RunConstants.NodeRest,
            RunConstants.NodeShop,
        ];
        engine.State.MapChoices = [201, 202, 203, 204];
        engine.State.ShopCards = [301, 302, 303, 304, 305, 306, 307];
        engine.State.RelicReward = 401;
        engine.State.EventId = RunConstants.EventBrainLeech;
        engine.State.PotionSlots = [501, 502, 503];
        engine.State.ShopRelics = [601, 602, 603];
        engine.State.ShopPotions = [701, 702, 703];
        engine.State.ShopCosts[RunConstants.ShopRemoveAction] = 175;

        engine.WriteObservation(obs);

        int offset = RunConstants.CombatObsSize;
        Assert.Equal(
            new[]
            {
                (int)RunPhase.Shop,
                7,
                RunConstants.ActUnderdocks,
                3,
                123,
                55,
                77,
                2,
                RunConstants.NodeShop,
                101,
                102,
                103,
                RunConstants.NodeNormal,
                RunConstants.NodeElite,
                RunConstants.NodeRest,
                RunConstants.NodeShop,
                201,
                202,
                203,
                204,
                301,
                302,
                303,
                401,
                RunConstants.EventBrainLeech,
                501,
                502,
                503,
                601,
                602,
                603,
                701,
                702,
                703,
                175,
            },
            obs[offset..(offset + RunConstants.RunExtraObsSize)]
        );
    }

    [Fact]
    public void AncientActionMask_EnablesGeneratedNeowOptions()
    {
        var engine = new RunEngine();
        var mask = new int[RunConstants.MaxActions];

        engine.Reset("0");
        engine.WriteActionMask(mask);

        Assert.Equal(new[] { 1, 1, 1 }, mask[..3]);
        Assert.All(mask[3..], value => Assert.Equal(0, value));
    }

    /// <summary>
    /// Step until the run reaches the Map phase. Which phases sit between Neow and
    /// the map depends on the Neow options rolled for the seed (some grant a card
    /// reward first), so tests must not assume a fixed number of steps.
    /// </summary>
    private static void AdvanceToMapPhase(RunEngine engine, int maxSteps = 8)
    {
        for (int i = 0; i < maxSteps && engine.State.Phase != RunPhase.Map; i++)
        {
            engine.Step(0, -1, out _, out _, out _);
        }

        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    [Fact]
    public void Reset_GeneratesActRoomsAndMapOptions()
    {
        var engine = new RunEngine();
        var obs = new int[RunConstants.RunObsSize];
        var mask = new int[RunConstants.MaxActions];

        engine.Reset("0");
        Assert.NotEmpty(engine.State.NormalEncounterSequence);
        Assert.NotEmpty(engine.State.EliteEncounterSequence);
        Assert.True(engine.State.BossEncounterId > 0);
        Assert.True(engine.State.MapNodes.Count > 2);

        AdvanceToMapPhase(engine);
        engine.WriteObservation(obs);
        engine.WriteActionMask(mask);

        int offset = RunConstants.CombatObsSize;
        Assert.Equal((int)RunPhase.Map, obs[offset]);
        Assert.Equal(99, obs[offset + 4]);
        Assert.Equal(2, obs[offset + 7]);
        Assert.Contains(
            obs[(offset + 12)..(offset + 16)],
            nodeType => nodeType != RunConstants.NodeNone
        );
        Assert.Contains(mask[..RunConstants.MapChoices], value => value == 1);
    }

    [Fact]
    public void MapStepRoutesCombatNodesIntoRunCombat()
    {
        var engine = new RunEngine();
        var obs = new int[RunConstants.RunObsSize];

        engine.Reset("0");
        AdvanceToMapPhase(engine);
        int action = Array.FindIndex(
            engine.State.MapNodeTypes,
            nodeType => nodeType == RunConstants.NodeNormal
        );
        Assert.True(action >= 0);

        int status = engine.Step(action, -1, out _, out bool terminal, out bool truncated);
        engine.WriteObservation(obs);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.False(truncated);
        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveCombat);
        Assert.True(engine.ActiveEncounterId >= 0);
        Assert.Equal(2, engine.State.Floor);
        Assert.Equal(1, engine.State.NormalEncountersVisited);
        Assert.NotEqual(0, obs[0]);
    }

    [Fact]
    public void RunInfo_UsesFixedDiagnosticLayout()
    {
        var engine = new RunEngine();
        var info = new int[RunConstants.RunInfoSize];

        engine.Reset("0");
        engine.State.Phase = RunPhase.Event;
        engine.State.Floor = 4;
        engine.State.Act = RunConstants.ActUnderdocks;
        engine.State.Deck = [new CardInstance(1, false), new CardInstance(2, false)];
        engine.State.Gold = 88;
        engine.State.PlayerHp = 44;
        engine.State.PlayerMaxHp = 66;
        engine.State.Relics = [new RelicInstance(10), new RelicInstance(20), new RelicInstance(30)];
        engine.State.CurrentNodeType = RunConstants.NodeEvent;
        engine.State.EventId = RunConstants.EventSunkenTreasury;
        engine.State.RelicReward = 123;
        engine.WriteInfo(info);

        Assert.Equal(
            new[]
            {
                (int)RunPhase.Event,
                4,
                RunConstants.ActUnderdocks,
                2,
                88,
                44,
                66,
                3,
                RunConstants.NodeEvent,
                RunConstants.EventSunkenTreasury,
                123,
            },
            info
        );
    }

    [Fact]
    public void RunObservationAndInfo_IgnoreStaleActiveCombatOutsideCombatPhase()
    {
        var engine = new RunEngine();
        var obs = new int[RunConstants.RunObsSize];
        var info = new int[RunConstants.RunInfoSize];

        engine.Reset("0");
        engine.State.Phase = RunPhase.CardReward;
        engine.State.PlayerHp = 54;
        engine.State.PlayerMaxHp = 80;
        engine.State.Gold = 108;
        engine.State.PotionSlots = [1, 0, 0];
        engine.State.ActiveCombat = new CombatState
        {
            PlayerHp = 48,
            PlayerMaxHp = 70,
            PlayerGold = 99,
            PotionSlots = [2, 3, 0],
            Enemies = [new EnemyState { Hp = 10, MaxHp = 10 }],
        };

        engine.WriteObservation(obs);
        engine.WriteInfo(info);

        int offset = RunConstants.CombatObsSize;
        Assert.Equal(0, obs[54]);
        Assert.Equal(54, obs[offset + 5]);
        Assert.Equal(80, obs[offset + 6]);
        Assert.Equal(108, obs[offset + 4]);
        Assert.Equal(1, obs[offset + 25]);
        Assert.Equal(54, info[5]);
        Assert.Equal(80, info[6]);
        Assert.Equal(108, info[4]);
    }

    [Fact]
    public void RunActionMasks_CoverNonCombatPhases()
    {
        var engine = new RunEngine();
        var mask = new int[RunConstants.MaxActions];
        engine.Reset("0");

        engine.State.Phase = RunPhase.CardReward;
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 1, 2, RunConstants.RewardSkipAction);

        Array.Clear(mask);
        engine.State.Phase = RunPhase.Map;
        engine.State.MapNodeTypes =
        [
            RunConstants.NodeNormal,
            RunConstants.NodeNone,
            RunConstants.NodeEvent,
            RunConstants.NodeNone,
        ];
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 2);

        Array.Clear(mask);
        engine.State.Phase = RunPhase.Rest;
        engine.State.Deck = [new CardInstance(10001, false)];
        engine.WriteActionMask(mask);
        AssertMask(mask, RunConstants.RestHealAction, RunConstants.RewardSkipAction);

        Array.Clear(mask);
        engine.State.Deck = [new CardInstance(10001, false), new CardInstance(472, false)];
        engine.WriteActionMask(mask);
        AssertMask(
            mask,
            RunConstants.RestHealAction,
            RunConstants.RestUpgradeAction,
            RunConstants.RewardSkipAction
        );

        Array.Clear(mask);
        engine.State.Phase = RunPhase.RelicReward;
        engine.State.RelicReward = 0;
        engine.WriteActionMask(mask);
        AssertMask(mask, RunConstants.RewardSkipAction);

        Array.Clear(mask);
        engine.State.RelicReward = 42;
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, RunConstants.RewardSkipAction);

        Array.Clear(mask);
        engine.State.Phase = RunPhase.TransformSelect;
        engine.State.Deck =
        [
            new CardInstance(1, false),
            new CardInstance(2, true),
            new CardInstance(10001, false),
        ];
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 1, 2);

        Array.Clear(mask);
        engine.State.Phase = RunPhase.Complete;
        engine.WriteActionMask(mask);
        AssertMask(mask);
    }

    [Fact]
    public void ShopActionMask_UsesInventoryCostsGoldAndPotionSlots()
    {
        var engine = new RunEngine();
        var mask = new int[RunConstants.MaxActions];
        engine.Reset("0");
        engine.State.Phase = RunPhase.Shop;
        engine.State.Gold = 100;
        engine.State.Deck = [new CardInstance(1, false), new CardInstance(2, false)];
        engine.State.ShopCards[0] = 101;
        engine.State.ShopCards[1] = 102;
        engine.State.ShopCosts[0] = 50;
        engine.State.ShopCosts[1] = 150;
        engine.State.ShopRelics[0] = 201;
        engine.State.ShopCosts[7] = 100;
        engine.State.ShopPotions[0] = 301;
        engine.State.ShopCosts[10] = 90;
        engine.State.ShopCosts[RunConstants.ShopRemoveAction] = 100;

        engine.WriteActionMask(mask);

        AssertMask(mask, 0, 7, 10, RunConstants.ShopRemoveAction, RunConstants.ShopSkipAction);

        Array.Clear(mask);
        engine.State.PotionSlots = [1, 2, 3];
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 7, RunConstants.ShopRemoveAction, RunConstants.ShopSkipAction);
    }

    [Fact]
    public void EventActionMask_UsesCurrentPythonPredicates()
    {
        var engine = new RunEngine();
        var mask = new int[RunConstants.MaxActions];
        engine.Reset("0");
        engine.State.Phase = RunPhase.Event;
        engine.State.PlayerHp = 10;
        engine.State.PlayerMaxHp = 80;
        engine.State.Gold = 5;
        engine.State.Deck = [new CardInstance(10001, false), new CardInstance(472, false)];

        engine.State.EventId = RunConstants.EventTheLegendsWereTrue;
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 1, RunConstants.EventSkipAction);

        Array.Clear(mask);
        engine.State.PotionSlots = [1, 2, 3];
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, RunConstants.EventSkipAction);

        Array.Clear(mask);
        engine.State.EventId = RunConstants.EventResultPending;
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, RunConstants.EventSkipAction);

        Array.Clear(mask);
        engine.State.EventId = 999;
        engine.WriteActionMask(mask);
        AssertMask(mask, 0, 1, 2, RunConstants.EventSkipAction);
    }

    [Fact]
    public void CombatActionMask_DelegatesToActiveCombatState()
    {
        var engine = new RunEngine();
        var mask = new int[RunConstants.MaxActions];
        var combat = new CombatState();
        CombatFactory.Reset(combat, seed: 123);

        engine.Reset("0");
        engine.State.Phase = RunPhase.Combat;
        engine.State.ActiveCombat = combat;
        engine.WriteActionMask(mask);

        AssertMask(
            mask,
            CombatEngine
                .ValidActions(combat)
                .Where(action => action < RunConstants.MaxActions)
                .ToArray()
        );
    }

    [Fact]
    public void StartCombat_MatchesLegacyPreShuffledRunCombatReset()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        int[] deck = RunConstants.StarterDeckIds.ToArray();
        int[] relics = [RunConstants.RelicBurningBlood];
        int[] potions = [0, 0, 0];

        Assert.Equal(
            0,
            engine.StartCombat(
                deck,
                encounterId: 1,
                relics,
                playerHp: 64,
                playerMaxHp: 80,
                potions,
                playerGold: 99
            )
        );

        var expectedDeck = deck.ToArray();
        var shuffle = new GameRng(new RunRngSet("0").Seed, "shuffle");
        shuffle.Shuffle(expectedDeck);
        var expectedShuffleRng = new CountingRandom(shuffle.RawSeed);
        for (int i = 0; i < shuffle.CallCount; i++)
        {
            expectedShuffleRng.Next();
        }

        var expectedCombat = new CombatState
        {
            NicheHpRng = new CountingRandom(new RunRngSet("0").Niche.RawSeed),
        };
        CombatFactory.Reset(
            expectedCombat,
            new CountingRandom(new RunRngSet("0").Niche.RawSeed),
            expectedDeck,
            1,
            relics,
            64,
            80,
            potions,
            99,
            deckPreShuffled: true,
            expectedShuffleRng,
            encounterRngSeed: 0,
            nicheSkipCount: 0,
            new Random(new RunRngSet("0").MonsterAi.RawSeed)
        );

        var expectedObs = new int[CombatObservation.ObsSize];
        var actualObs = new int[CombatObservation.ObsSize];
        CombatObservation.Write(expectedCombat, expectedObs);
        CombatObservation.Write(engine.State.ActiveCombat!, actualObs);
        Assert.Equal(expectedObs, actualObs);
        Assert.Equal(shuffle.CallCount, engine.ActiveShuffleRngCallCount);
        Assert.Equal(expectedCombat.NicheHpRng!.CallCount, engine.ActiveNicheRngCallCount);
        Assert.Equal(engine.ActiveNicheRngCallCount, engine.State.Rng.Niche.CallCount);
    }

    [Fact]
    public void CombatStep_RoutesThroughActiveCombatAndUpdatesRunStateOnTerminal()
    {
        var engine = new RunEngine();
        var obs = new int[RunConstants.RunObsSize];
        var mask = new int[RunConstants.MaxActions];
        engine.Reset("0");
        engine.StartCombat(
            RunConstants.StarterDeckIds,
            encounterId: 1,
            [RunConstants.RelicBurningBlood],
            playerHp: 1,
            playerMaxHp: 80,
            [0, 0, 0],
            playerGold: 99
        );

        engine.WriteActionMask(mask);
        Assert.Contains(1, mask);

        int status = 0;
        float reward = 0;
        bool terminal = false;
        bool truncated = false;
        for (int i = 0; i < 20 && !terminal; i++)
        {
            int endTurn = CombatEngine.ValidActions(engine.State.ActiveCombat!).Last();
            status = engine.Step(endTurn, -1, out reward, out terminal, out truncated);
        }
        engine.WriteObservation(obs);

        Assert.Equal(0, status);
        Assert.True(terminal);
        Assert.True(truncated is false);
        Assert.True(reward < 0);
        Assert.False(engine.State.LastPlayerWon);
        Assert.Equal(0, engine.State.PlayerHp);
        Assert.Equal(0, obs[RunConstants.CombatObsSize + 5]);
    }

    [Fact]
    public void CombatWin_EntersCardRewardAndGeneratesRunRewards()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.CurrentNodeType = RunConstants.NodeNormal;
        engine.State.PlayerHp = 20;
        int rewardsCallsBefore = engine.State.PlayerRng.Rewards.CallCount;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.All(engine.State.RewardCards, cardId => Assert.NotEqual(0, cardId));
        Assert.True(engine.State.RewardGold > 0);
        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(26, engine.State.PlayerHp);
        Assert.False(engine.State.PendingRelicReward);
        Assert.True(engine.State.RewardCardPending);
        Assert.True(engine.State.PlayerRng.Rewards.CallCount >= rewardsCallsBefore + 11);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.True(engine.State.Gold > 99);
    }

    [Fact]
    public void CombatWin_HealingRelicsDoNotOverheal()
    {
        var burningBlood = new RunState
        {
            PlayerHp = 48,
            PlayerMaxHp = 80,
            Gold = 99,
            CurrentNodeType = RunConstants.NodeNormal,
            Relics = [new RelicInstance(RunConstants.RelicBurningBlood)],
        };
        RunRewardGenerator.GenerateCombatRewards(burningBlood);
        Assert.Equal(54, burningBlood.PlayerHp);

        var blackBlood = new RunState
        {
            PlayerHp = 73,
            PlayerMaxHp = 80,
            Gold = 99,
            CurrentNodeType = RunConstants.NodeNormal,
            Relics = [new RelicInstance(RunConstants.RelicBlackBlood)],
        };
        RunRewardGenerator.GenerateCombatRewards(blackBlood);
        Assert.Equal(80, blackBlood.PlayerHp);
    }

    [Fact]
    public void CardRewardStep_AddsSelectedCardAndReturnsToMap()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.Step(0, -1, out _, out _, out _);
        int deckSize = engine.State.Deck.Count;
        engine.State.Phase = RunPhase.CardReward;
        engine.State.RewardCards = [13, 20, 50];
        engine.State.RewardUpgraded = [false, true, false];

        int status = engine.Step(1, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
        Assert.Equal(deckSize + 1, engine.State.Deck.Count);
        Assert.Contains(new CardInstance(20, Upgraded: true), engine.State.Deck);
        Assert.All(engine.State.RewardCards, cardId => Assert.Equal(0, cardId));
    }

    [Fact]
    public void RelicRewardStep_AddsRelicAndReturnsToMap()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.Step(0, -1, out _, out _, out _);
        int relicCount = engine.State.Relics.Count;
        engine.State.Phase = RunPhase.RelicReward;
        engine.State.CurrentNodeType = RunConstants.NodeRelic;
        engine.State.RelicReward = RunConstants.RelicMeatOnTheBone;

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(relicCount + 1, engine.State.Relics.Count);
        Assert.Contains(
            engine.State.Relics,
            relic => relic.DefId == RunConstants.RelicMeatOnTheBone
        );
        Assert.Equal(0, engine.State.RelicReward);

        status = engine.Step(RunConstants.RewardSkipAction, -1, out _, out terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    [Fact]
    public void ShopGenerationAndPurchaseUseNativeRunState()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.Gold = 1_000;
        int rewardsCallsBefore = engine.State.PlayerRng.Rewards.CallCount;
        int shopCallsBefore = engine.State.PlayerRng.Shops.CallCount;

        RunRewardGenerator.EnterShop(engine.State);

        Assert.Equal(RunPhase.Shop, engine.State.Phase);
        Assert.All(engine.State.ShopCards, cardId => Assert.NotEqual(0, cardId));
        Assert.All(engine.State.ShopRelics, relicId => Assert.NotEqual(0, relicId));
        Assert.All(engine.State.ShopPotions, potionId => Assert.NotEqual(0, potionId));
        Assert.True(engine.State.PlayerRng.Rewards.CallCount >= rewardsCallsBefore + 2);
        Assert.True(engine.State.PlayerRng.Shops.CallCount > shopCallsBefore);

        int deckSize = engine.State.Deck.Count;
        int card = engine.State.ShopCards[0];
        int cost = engine.State.ShopCosts[0];
        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Shop, engine.State.Phase);
        Assert.Equal(1_000 - cost, engine.State.Gold);
        Assert.Equal(deckSize + 1, engine.State.Deck.Count);
        Assert.Contains(new CardInstance(card, Upgraded: false), engine.State.Deck);
        Assert.Equal(0, engine.State.ShopCards[0]);

        status = engine.Step(RunConstants.ShopSkipAction, -1, out _, out terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    [Fact]
    public void RestStep_HealsThirtyPercentOrUpgradesThenReturnsToMapAfterConfirmation()
    {
        var healEngine = new RunEngine();
        healEngine.Reset("0");
        healEngine.Step(0, -1, out _, out _, out _);
        healEngine.State.Phase = RunPhase.Rest;
        healEngine.State.PlayerHp = 48;

        int healStatus = healEngine.Step(
            RunConstants.RestHealAction,
            -1,
            out _,
            out bool healTerminal,
            out _
        );

        Assert.Equal(0, healStatus);
        Assert.False(healTerminal);
        Assert.Equal(RunPhase.Rest, healEngine.State.Phase);
        Assert.Equal(72, healEngine.State.PlayerHp);
        Assert.Equal(0, healEngine.Step(RunConstants.RestHealAction, -1, out _, out _, out _));
        Assert.Equal(RunPhase.Map, healEngine.State.Phase);

        var upgradeEngine = new RunEngine();
        upgradeEngine.Reset("0");
        upgradeEngine.Step(0, -1, out _, out _, out _);
        upgradeEngine.State.Phase = RunPhase.Rest;

        int upgradeStatus = upgradeEngine.Step(
            RunConstants.RestUpgradeAction,
            -1,
            out _,
            out bool upgradeTerminal,
            out _
        );

        Assert.Equal(0, upgradeStatus);
        Assert.False(upgradeTerminal);
        Assert.Equal(RunPhase.TransformSelect, upgradeEngine.State.Phase);
        Assert.Equal(0, upgradeEngine.Step(0, -1, out _, out upgradeTerminal, out _));
        Assert.False(upgradeTerminal);
        Assert.Equal(RunPhase.Rest, upgradeEngine.State.Phase);
        Assert.Contains(upgradeEngine.State.Deck, card => card.Upgraded);
        Assert.Equal(
            0,
            upgradeEngine.Step(RunConstants.RestUpgradeAction, -1, out _, out _, out _)
        );
        Assert.Equal(RunPhase.Map, upgradeEngine.State.Phase);
    }

    [Fact]
    public void EventStep_AppliesModeledEventEffectsAndProceedScreens()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.Step(0, -1, out _, out _, out _);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventDoorsOfLightAndDark;
        engine.State.Deck =
        [
            new CardInstance(472, false),
            new CardInstance(131, false),
            new CardInstance(30, false),
        ];

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(RunConstants.EventResultPending, engine.State.EventId);
        Assert.Equal(2, engine.State.Deck.Count(card => card.Upgraded));

        status = engine.Step(0, -1, out _, out terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
        Assert.Equal(0, engine.State.EventId);
    }

    [Fact]
    public void SunkenTreasury_UsesResultPageBeforeReturningToMap()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.Step(0, -1, out _, out _, out _);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventSunkenTreasury;
        engine.State.Gold = 100;

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(RunConstants.EventResultPending, engine.State.EventId);
        Assert.InRange(engine.State.Gold, 152, 167);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.Map, engine.State.Phase);
    }

    [Fact]
    public void StartCombat_PreservesRunPotionSlots()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.PotionSlots[0] = 18;

        int status = engine.StartCombat(
            engine.State.Deck.Select(card => card.DefId).ToArray(),
            RunConstants.SlimesWeakEncounterId,
            engine.State.Relics.Select(relic => relic.DefId).ToArray(),
            engine.State.PlayerHp,
            engine.State.PlayerMaxHp,
            engine.State.PotionSlots,
            engine.State.Gold
        );

        Assert.Equal(0, status);
        Assert.Equal(18, engine.State.PotionSlots[0]);
        Assert.Equal(18, engine.State.ActiveCombat!.PotionSlots[0]);
    }

    [Fact]
    public void BrainLeechRewardBranch_EntersNativeCardRewardFlow()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventBrainLeech;
        engine.State.PlayerHp = 30;
        int rewardsCallsBefore = engine.State.PlayerRng.Rewards.CallCount;

        int status = engine.Step(1, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(25, engine.State.PlayerHp);
        Assert.All(engine.State.RewardCards, cardId => Assert.NotEqual(0, cardId));
        Assert.True(engine.State.RewardCardPending);
        Assert.Equal(rewardsCallsBefore, engine.State.PlayerRng.Rewards.CallCount);
    }

    [Fact]
    public void AncientNewLeaf_UsesTransformSelectionScreen()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.NeowOptions = [RunConstants.RelicNewLeaf, 0, 0];
        const int selectedDeckIndex = 9;
        int originalCard = engine.State.Deck[selectedDeckIndex].DefId;

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Contains(engine.State.Relics, relic => relic.DefId == RunConstants.RelicNewLeaf);

        status = engine.Step(0, -1, out _, out terminal, out _);
        Assert.Equal(0, status);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        status = engine.Step(selectedDeckIndex, -1, out _, out terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
        Assert.NotEqual(originalCard, engine.State.Deck[selectedDeckIndex].DefId);
    }

    [Fact]
    public void AncientLostCoffer_EntersCardRewardAndGrantsPotion()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.NeowOptions = [RunConstants.RelicLostCoffer, 0, 0];

        int status = engine.Step(0, -1, out _, out bool terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        Assert.Contains(engine.State.Relics, relic => relic.DefId == RunConstants.RelicLostCoffer);
        Assert.All(engine.State.RewardCards, cardId => Assert.NotEqual(0, cardId));
        Assert.Contains(engine.State.PotionSlots, potionId => potionId != 0);
        Assert.Equal(0.3, engine.State.PotionRewardOdds, precision: 6);
    }

    [Fact]
    public void AncientAstrolabeAndEmptyCage_ResolveSelectionFollowUps()
    {
        var astrolabe = new RunEngine();
        astrolabe.Reset("0");
        astrolabe.State.NeowOptions = [RunConstants.RelicAstrolabe, 0, 0];

        int status = astrolabe.Step(0, -1, out _, out _, out _);
        Assert.Equal(0, status);
        Assert.Equal(RunPhase.TransformSelect, astrolabe.State.Phase);

        status = astrolabe.Step(0, -1, out _, out bool terminal, out _);
        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, astrolabe.State.Phase);
        Assert.Contains(astrolabe.State.Deck, card => card.Upgraded);

        var cage = new RunEngine();
        cage.Reset("0");
        cage.State.NeowOptions = [RunConstants.RelicEmptyCage, 0, 0];
        int deckSize = cage.State.Deck.Count;

        status = cage.Step(0, -1, out _, out _, out _);
        Assert.Equal(0, status);
        Assert.Equal(RunPhase.TransformSelect, cage.State.Phase);

        status = cage.Step(0, -1, out _, out terminal, out _);
        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.Equal(RunPhase.Map, cage.State.Phase);
        Assert.Equal(deckSize - 2, cage.State.Deck.Count);
    }

    private static void AssertMask(int[] mask, params int[] enabledActions)
    {
        var expected = new int[mask.Length];
        foreach (int action in enabledActions)
        {
            expected[action] = 1;
        }

        Assert.Equal(expected, mask);
    }
}
