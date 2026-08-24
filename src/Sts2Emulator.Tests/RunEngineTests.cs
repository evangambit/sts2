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
    public void RunRngSet_CanonicalizesSeedLikeTheGame()
    {
        // The game canonicalizes every chosen seed before hashing it
        // (StartRunLobby.BeginRunLocally -> SeedHelper.CanonicalizeSeed): uppercase,
        // O -> 0, I -> 1, trimmed. Its seed alphabet contains neither O nor I. Hashing
        // the string as typed would derive a different uint than the live run for any
        // seed with lowercase or those letters — a silent, total divergence.
        Assert.Equal("ABCDEF0", SeedHelper.Canonicalize(" abcdefo "));
        Assert.Equal("1", SeedHelper.Canonicalize("i"));
        Assert.DoesNotContain('I', SeedHelper.Characters);
        Assert.DoesNotContain('O', SeedHelper.Characters);

        Assert.Equal(new RunRngSet("ABCDEF").Seed, new RunRngSet("abcdef").Seed);
        Assert.Equal(new RunRngSet("4WN61S1G").Seed, new RunRngSet("4WN6ISIG").Seed);

        var engine = new RunEngine();
        engine.Reset("4wn6isig");
        Assert.Equal("4WN61S1G", engine.State.StringSeed);
    }

    [Fact]
    public void EncounterRng_SeedMirrorsTheGamesFormula()
    {
        // EncounterModel builds its own stream as
        //   new Rng((uint)((int)runState.Rng.Seed + runState.TotalFloor
        //                  + GetDeterministicHashCode(Id.Entry)))
        // and rolls Slimes rosters / Corpse Slug starting moves from it. This pins the
        // three inputs, because getting any of them wrong is silent: the roster is
        // still plausible, just not the one the live game generated.
        uint runSeed = new RunRngSet("UVUYCBYWB6").Seed;

        int? slimes = EncounterRng.SeedFor(
            (int)runSeed,
            totalFloor: 1,
            RunConstants.SlimesWeakEncounterId,
            weakVariant: true
        );
        Assert.Equal(
            unchecked(
                (int)(
                    runSeed + 1u + (uint)DeterministicHash.GetDeterministicHashCode("SLIMES_WEAK")
                )
            ),
            slimes
        );

        // The floor term is real: a different floor is a different roster.
        Assert.NotEqual(
            slimes,
            EncounterRng.SeedFor(
                (int)runSeed,
                totalFloor: 2,
                RunConstants.SlimesWeakEncounterId,
                weakVariant: true
            )
        );

        // Slug variants share an emulator encounter id but not an entry id.
        Assert.NotEqual(
            EncounterRng.SeedFor(
                (int)runSeed,
                1,
                RunConstants.CorpseSlugsEncounterId,
                weakVariant: true
            ),
            EncounterRng.SeedFor(
                (int)runSeed,
                1,
                RunConstants.CorpseSlugsEncounterId,
                weakVariant: false
            )
        );

        // Encounters that do not roll their composition get no stream at all.
        Assert.Null(EncounterRng.SeedFor((int)runSeed, 1, encounterId: 0, weakVariant: true));
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
            new[] { 965, 246, 0, 824, 687 },
            Enumerable.Range(0, 5).Select(_ => neow.NextInt(1000)).ToArray()
        );
        // Locked to catch drift. These moved when PlayerRngSet stopped adding 1 to the
        // run seed: Player.cs seeds it with hash(seed) + the owner's player slot, and a
        // solo run's only player is slot 0. The live-derived anchor for that is
        // Kaleidoscope_OffersTheCardsTheGameOffers.
        Assert.Equal(1246982107, player.Rewards.NextInt(int.MaxValue));
    }

    [Fact]
    public void RunGeneration_MatchesLiveCaptureForAbcdef()
    {
        // Ground truth: a live v0.107.1 run seeded "ABCDEF" at A8, read out of its
        // current_run.save (acts[0].rooms). Real game outputs — keep them.
        //
        // Hand-transcribed because this capture predates fixtures and its save was
        // overwritten by a later run, so it cannot be re-distilled. Every other
        // capture lives in tests/fixtures/run_generation/ and generates its test via
        // scripts/generate_capture_tests.py; re-capture "ABCDEF" and this can go too.
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
    public void RunReset_StartsAtAncientPhaseWithStarterState()
    {
        var engine = new RunEngine();

        engine.Reset("0");

        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);
        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(11, engine.State.Deck.Count);
        // Locked to catch drift, not ground truth — the live-derived anchor is
        // NeowOptions_MatchTheLiveGame below.
        Assert.Equal(new[] { 124, 231, 240 }, engine.State.NeowOptions);
    }

    /// <summary>
    /// Ground truth: a live A8 capture on seed QS2GYXRKWN offers Kaleidoscope,
    /// Nutritious Oyster and Neow's Bones, in that order.
    ///
    /// EventModel seeds each event with Seed + the owner's player slot + hash(Id.Entry),
    /// and a solo run's only player is slot 0. Seeding with 1 gave a different stream and
    /// therefore three different relics — in EVERY run, since Neow opens all of them.
    /// </summary>
    [Fact]
    public void NeowOptions_MatchTheLiveGame()
    {
        var engine = new RunEngine();

        engine.Reset("QS2GYXRKWN");

        Assert.Equal(
            new[]
            {
                RunConstants.RelicKaleidoscope,
                RunConstants.RelicNutritiousOyster,
                RunConstants.RelicNeowsBones,
            },
            engine.State.NeowOptions
        );
    }

    /// <summary>
    /// Ground truth: the live A8 capture on seed QS2GYXRKWN is offered Calcify, Prepared
    /// and Skim, then Acrobatics, Collision Course and Boost Away — one card from each of
    /// three other characters' pools, twice.
    ///
    /// Four things have to be right at once for this to land, and each was wrong:
    /// the Rewards stream's seed (the player slot again), three draws per card rather
    /// than two (rarity, card, upgrade), rarity read from the card data rather than a
    /// 144-id table that defaulted to Common, and Basic cards excluded from a Common
    /// reward list instead of counted into it.
    /// </summary>
    [Fact]
    public void Kaleidoscope_OffersTheCardsTheGameOffers()
    {
        var engine = new RunEngine();
        engine.Reset("QS2GYXRKWN");

        engine.Step(0, -1, out _, out _, out _); // take Kaleidoscope
        engine.Step(0, -1, out _, out _, out _); // claim the first reward
        Assert.Equal(new[] { "Calcify", "Prepared", "Skim" }, OfferedNames(engine));

        engine.Step(0, -1, out _, out _, out _); // take Calcify
        engine.Step(0, -1, out _, out _, out _); // claim the second reward
        Assert.Equal(new[] { "Acrobatics", "CollisionCourse", "BoostAway" }, OfferedNames(engine));
    }

    private static string[] OfferedNames(RunEngine engine) =>
        [.. engine.State.RewardCards.Select(id => GeneratedData.Cards.Get(id).Name)];

    /// <summary>
    /// Kaleidoscope offers "2 card rewards from other characters" — two screens the
    /// player answers one after the other, not two cards appended to the deck. Confirmed
    /// live: the capture shows two card-reward screens between Neow and the map.
    /// </summary>
    [Fact]
    public void Kaleidoscope_OffersTwoCardRewardsFromOtherCharacters()
    {
        var engine = new RunEngine();
        engine.Reset("QS2GYXRKWN");
        int deckBefore = engine.State.Deck.Count;

        engine.Step(0, -1, out _, out _, out _); // take Kaleidoscope

        // Both rewards land on the rewards screen at once and are answered one at a time,
        // which is the sequence the capture shows: rewards, card, rewards, card, back to
        // Neow, proceed, map.
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(2, engine.State.PendingOtherCharacterCardRewards);
        Assert.Equal(deckBefore, engine.State.Deck.Count);

        engine.Step(0, -1, out _, out _, out _); // claim the first
        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        // Every offered card comes from a character the Ironclad is not.
        Assert.All(
            engine.State.RewardCards,
            cardId => Assert.DoesNotContain(cardId, GeneratedData.CardPools.Ironclad.ToArray())
        );

        engine.Step(0, -1, out _, out _, out _); // take a card
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(deckBefore + 1, engine.State.Deck.Count);

        engine.Step(0, -1, out _, out _, out _); // claim the second
        Assert.Equal(RunPhase.CardReward, engine.State.Phase);

        engine.Step(0, -1, out _, out _, out _); // take a card
        Assert.Equal(deckBefore + 2, engine.State.Deck.Count);

        // Neow is still there, waiting to be dismissed.
        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
        engine.Step(0, -1, out _, out _, out _);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
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
            RunConstants.NodeEvent,
            RunConstants.NodeRelic,
            RunConstants.NodeBoss,
        ];
        engine.State.MapChoices = [201, 202, 203, 204, 205, 206, 207];
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
                RunConstants.NodeEvent,
                RunConstants.NodeRelic,
                RunConstants.NodeBoss,
                201,
                202,
                203,
                204,
                205,
                206,
                207,
                401,
                RunConstants.EventBrainLeech,
                501,
                502,
                503,
            },
            obs[offset..(offset + RunConstants.RunScalarObsSize)]
        );

        // The deck and the relics follow the scalars, in the order the run holds them.
        int deck = offset + RunConstants.DeckObsOffset;
        Assert.Equal(
            new[] { 1, 0, 2, 0, 3, 1 },
            new[]
            {
                obs[deck],
                obs[deck + 1],
                obs[deck + RunConstants.DeckSlotSize],
                obs[deck + RunConstants.DeckSlotSize + 1],
                obs[deck + 2 * RunConstants.DeckSlotSize],
                obs[deck + 2 * RunConstants.DeckSlotSize + 1],
            }
        );

        int relics = offset + RunConstants.RelicObsOffset;
        Assert.Equal(
            new[] { 10, 20, 0 },
            new[]
            {
                obs[relics],
                obs[relics + RunConstants.RelicSlotSize],
                obs[relics + 2 * RunConstants.RelicSlotSize],
            }
        );

        // The shop's slots are indexed by the action that buys them: seven cards, three
        // relics, three potions, then the removal service, each with its price.
        int shop = offset + RunConstants.ShopObsOffset;
        Assert.Equal(
            new[] { 301, 302, 303, 304, 305, 306, 307, 601, 602, 603, 701, 702, 703, 0 },
            Enumerable
                .Range(0, RunConstants.ShopSlots)
                .Select(i => obs[shop + i * RunConstants.ShopSlotSize])
                .ToArray()
        );
        Assert.Equal(
            175,
            obs[shop + RunConstants.ShopRemoveAction * RunConstants.ShopSlotSize + 1]
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
        Assert.Equal(1, obs[offset + RunConstants.PotionObsOffset]);
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
            RunConstants.NodeNone,
            RunConstants.NodeNone,
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
        // Seed "0" now offers Kaleidoscope at index 0, which owes two card rewards; this
        // test is about what one card reward does, so answer them first.
        engine.State.PendingOtherCharacterCardRewards = 0;
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
        // A relic node is floors past Neow, so Neow is not still waiting on a Proceed.
        // The step above took a blessing and left the flag set; leaving it set here builds
        // a state no run reaches, and the screen would correctly go back to the ancient
        // rather than to the map (which is what Small Capsule's skippable relic offer
        // needs it to do).
        engine.State.NeowAwaitingProceed = false;

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

        // The reward is ROLLED from the colourless pool, so it costs draws off the
        // rewards stream -- CardFactory.CreateForReward rolls a rarity and then a card
        // for each of the three. This used to assert that nothing was drawn, which was
        // true only because three card ids were hard-written here and the same three came
        // out of every seed.
        Assert.True(
            engine.State.PlayerRng.Rewards.CallCount > rewardsCallsBefore,
            "a rolled card reward has to cost draws"
        );
        Assert.All(
            engine.State.RewardCards,
            cardId => Assert.Contains(cardId, GeneratedData.CardPools.Colorless.ToArray())
        );
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

        // The selection is answered directly. It used to take one step more than this --
        // the older TransformSelectedDeckIndex path spent a step arriving at a screen it
        // had already opened -- and a live capture (`N11HWGCNUN`) is back at the ancient
        // with the card transformed on the step this one selects (catalogue E50).
        status = engine.Step(selectedDeckIndex, -1, out _, out terminal, out _);

        Assert.Equal(0, status);
        Assert.False(terminal);
        Assert.NotEqual(originalCard, engine.State.Deck[selectedDeckIndex].DefId);

        // And a selection Neow opened returns to Neow, which stays up for one Proceed.
        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
        engine.Step(0, -1, out _, out terminal, out _);
        Assert.Equal(RunPhase.Map, engine.State.Phase);
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
        // AfterObtained is a RewardsCmd.OfferCustom of two rewards, so both sit on a
        // SCREEN to be claimed -- it does not hand the card reward straight over.
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Contains(engine.State.Relics, relic => relic.DefId == RunConstants.RelicLostCoffer);
        Assert.True(engine.State.RewardCardPending);
        Assert.NotEqual(0, engine.State.RewardPotion);
        // The potion is not in a slot until it is claimed.
        Assert.All(engine.State.PotionSlots, potionId => Assert.Equal(0, potionId));
        // PotionReward.Populate only draws a potion; the odds belong to RewardsSet, which
        // is the combat path that ROLLS whether to offer one. A guaranteed potion leaves
        // them alone.
        Assert.Equal(0.4, engine.State.PotionRewardOdds, precision: 6);
    }

    /// <summary>
    /// Claiming both rewards off the coffer's screen: the potion goes to a slot, the card
    /// reward opens, and picking a card lands back on Neow's finished page rather than on
    /// an empty rewards screen.
    /// </summary>
    [Fact]
    public void AncientLostCoffer_ClaimsBothRewardsThenReturnsToNeow()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.NeowOptions = [RunConstants.RelicLostCoffer, 0, 0];
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        // The potion first, then the card reward.
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Contains(engine.State.PotionSlots, potionId => potionId != 0);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        Assert.All(engine.State.RewardCards, cardId => Assert.NotEqual(0, cardId));

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.Ancient, engine.State.Phase);
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
