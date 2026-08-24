using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;

namespace Sts2Emulator.Core;

public static class CombatFactory
{
    // Set before CreateEncounter; CreateEnemy uses this for HP instead of the main rng.
    // Matches RunState.Rng.Niche which is used exclusively by SetUniqueMonsterHpValue.
    private static CountingRandom? _currentNicheHpRng;

    // Tracks HP values already assigned in the current encounter to prevent duplicates,
    // matching Creature.SetUniqueMonsterHpValue which excludes already-used MaxHp values.
    private static HashSet<int>? _usedNicheHps;

    private const int StartingPlayerHp = 64;
    private const int StartingPlayerMaxHp = 80;

    /// <summary>The ascension of the combat currently being built; see CreateEnemy.</summary>
    private static int _currentAscension = Ascension.DefaultLevel;
    private const int StartingEnergy = 3;

    // Ironclad starting deck card IDs (from Generated/Cards.g.cs).
    private const int StrikeId = IC.StrikeIronclad; // 472
    private const int DefendId = IC.DefendIronclad; // 131
    private const int BashId = IC.Bash; // 30
    private const int AscendersBaneId = IC.AscendersBane; // 10001

    /// <summary>
    /// The encounters the emulator can build. Internal rather than private so a combat
    /// test can name the encounter it is about instead of passing a bare index.
    /// </summary>
    internal enum ActOneEncounter
    {
        Cultists,
        Chompers,
        NibbitsWeak,
        SlimesWeak,
        Exoskeletons,
        Inklets,
        TwoTailedRats,
        GremlinMerc,
        FuzzyWurmCrawler,
        CorpseSlugs,
        SludgeSpinner,
        ShrinkerBeetle,
        Seapunk,
        Toadpoles,
        Mawler,
        NibbitsNormal,
        SlimesNormal,
        FlyconidNormal,
        SnappingJaxfruitNormal,
        CubexConstruct,
        VineShambler,
        OvergrowthCrawlers,
        CultistAndSeapunk,
        FossilStalker,
        PunchConstruct,
        SewerClam,
        HauntedShip,
        SlitheringStrangler,
        RubyRaiders,
        Fogmog,
        LivingFog,
        BowlbugsWeak,
        Bowlbugs,
        Tunneler,
        TunnelerAndChomper,
        ThievingHopper,
        Mytes,
        SlumberingBeetle,
        SpinyToad,
        Ovicopter,
        LouseProgenitor,
        HunterKiller,
        Axebot,
        DevotedSculptor,
        Fabricator,
        FrogKnight,
        GlobeHead,
        TurretOperator,
        OwlMagistrate,
        ScrollsWeak,
        Scrolls,
        SlimedBerserker,
        LostAndForgotten,
        Obscura,
        ConstructMenagerie,
        DenseVegetation,
        PunchOff,
        FakeMerchant,
        MysteriousKnight,
        BattlewornDummy1,
        BattlewornDummy2,
        BattlewornDummy3,
        BygoneEffigy,
        Entomancer,
        InfestedPrisms,
        PhrogParasite,
        SoulNexus,
        TerrorEel,
        Byrdonis,
        Decimillipede,
        Knights,
        MechaKnight,
        PhantasmalGardeners,
        Aeonglass,
        CeremonialBeast,
        KaiserCrab,
        KnowledgeDemon,
        LagavulinMatriarch,
        Queen,
        SoulFysh,
        TestSubject,
        TheInsatiable,
        TheKin,
        Vantom,
        WaterfallGiant,
        Architect,
        SkulkingColony,
    }

    private static readonly ActOneEncounter[] OvergrowthWeakEncounters =
    [
        ActOneEncounter.NibbitsWeak,
        ActOneEncounter.SlimesWeak,
        ActOneEncounter.ShrinkerBeetle,
        ActOneEncounter.FuzzyWurmCrawler,
    ];

    private static readonly ActOneEncounter[] UnderdocksWeakEncounters =
    [
        ActOneEncounter.CorpseSlugs,
        ActOneEncounter.Seapunk,
        ActOneEncounter.SludgeSpinner,
        ActOneEncounter.Toadpoles,
    ];

    public static CombatState NewCombat(int seed)
    {
        return NewCombat(new Random(seed));
    }

    public static CombatState NewCombat(Random rng)
    {
        var state = new CombatState
        {
            PlayerHp = StartingPlayerHp,
            PlayerMaxHp = StartingPlayerMaxHp,
            Energy = StartingEnergy,
            MaxEnergy = StartingEnergy,
        };
        Reset(state, rng);
        return state;
    }

    public static void Reset(CombatState state, int? seed = null)
    {
        Reset(state, seed.HasValue ? new Random(seed.Value) : new Random());
    }

    public static void Reset(CombatState state, Random rng)
    {
        Reset(state, rng, StarterDeck());
    }

    public static void Reset(CombatState state, Random rng, ReadOnlySpan<int> deckIds)
    {
        Reset(state, rng, deckIds, null);
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId
    )
    {
        Reset(state, rng, deckIds, encounterId, []);
    }

    // Same as the deck+encounter reset but lets callers set the "weak" combat
    // context. completedCombatRoomsBeforeCurrent in [0,3) selects the weak
    // encounter variant (e.g. CorpseSlugsWeak = 2 slugs vs Normal = 3); -1 (the
    // default elsewhere) yields the normal variant.
    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId,
        int completedCombatRoomsBeforeCurrent,
        int? encounterRngSeed = null
    )
    {
        Reset(
            state,
            rng,
            deckIds,
            encounterId,
            [],
            StartingPlayerHp,
            StartingPlayerMaxHp,
            [],
            playerGold: 0,
            encounterRngSeed: encounterRngSeed,
            completedCombatRoomsBeforeCurrent: completedCombatRoomsBeforeCurrent
        );
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId,
        ReadOnlySpan<int> relicIds
    )
    {
        Reset(state, rng, deckIds, encounterId, relicIds, StartingPlayerHp, StartingPlayerMaxHp);
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId,
        ReadOnlySpan<int> relicIds,
        int playerHp,
        int playerMaxHp
    )
    {
        Reset(state, rng, deckIds, encounterId, relicIds, playerHp, playerMaxHp, []);
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId,
        ReadOnlySpan<int> relicIds,
        int playerHp,
        int playerMaxHp,
        ReadOnlySpan<int> potionIds
    )
    {
        Reset(
            state,
            rng,
            deckIds,
            encounterId,
            relicIds,
            playerHp,
            playerMaxHp,
            potionIds,
            playerGold: 0
        );
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<int> deckIds,
        int? encounterId,
        ReadOnlySpan<int> relicIds,
        int playerHp,
        int playerMaxHp,
        ReadOnlySpan<int> potionIds,
        int playerGold,
        bool deckPreShuffled = false,
        CountingRandom? shuffleRng = null,
        int? encounterRngSeed = null,
        int nicheSkipCount = 0,
        Random? aiRng = null,
        int completedCombatRoomsBeforeCurrent = -1
    )
    {
        var deck = deckIds.ToArray().Select(id => new CardInstance(Math.Abs(id), id < 0)).ToArray();
        Reset(
            state,
            rng,
            deck,
            encounterId,
            relicIds,
            playerHp,
            playerMaxHp,
            potionIds,
            playerGold,
            deckPreShuffled,
            shuffleRng,
            encounterRngSeed,
            nicheSkipCount,
            aiRng,
            completedCombatRoomsBeforeCurrent
        );
    }

    public static void Reset(
        CombatState state,
        Random rng,
        ReadOnlySpan<CardInstance> deck,
        int? encounterId,
        ReadOnlySpan<int> relicIds,
        int playerHp,
        int playerMaxHp,
        ReadOnlySpan<int> potionIds,
        int playerGold,
        bool deckPreShuffled = false,
        CountingRandom? shuffleRng = null,
        int? encounterRngSeed = null,
        int nicheSkipCount = 0,
        Random? aiRng = null,
        int completedCombatRoomsBeforeCurrent = -1
    )
    {
        state.PlayerMaxHp = Math.Max(1, playerMaxHp);
        state.PlayerHp = Math.Clamp(playerHp, 0, state.PlayerMaxHp);
        state.PlayerGold = Math.Max(0, playerGold);
        state.Energy = StartingEnergy;
        state.MaxEnergy = StartingEnergy;
        state.PlayerBlock = 0;
        state.PendingSelection = null;
        state.AutoPlaying = false;
        state.PlayerBuffs = [];
        state.ForgetDrawOrder();
        state.Hand = [];
        state.DiscardPile = [];
        state.ExhaustPile = [];
        state.ReturnToHandBeforeDraw = [];
        state.PotionSlots = new int[3];
        for (int i = 0; i < Math.Min(state.PotionSlots.Length, potionIds.Length); i++)
        {
            state.PotionSlots[i] = potionIds[i];
        }

        state.Relics = relicIds.ToArray().Select(id => new RelicInstance(id)).ToList();
        state.Turn = 0;
        state.PlayerTurn = true;
        state.SkillPlayedWhileSmoggy = false;
        state.AttackCardsPlayedThisTurn = 0;
        state.PlayerHpLostThisTurn = 0;
        state.CardsExhaustedThisTurn = 0;
        // Preserve a ShuffleRng the caller set on the state (the direct combat env
        // wires GameRng(seed,"shuffle") there); only override when a shuffleRng arg
        // is explicitly passed (the run engine).
        state.ShuffleRng = shuffleRng ?? state.ShuffleRng;
        // Preserve an AiRng the caller set (the direct combat env wires
        // GameRng(seed,"monster_ai") there); only override when one is passed in.
        state.AiRng = aiRng ?? state.AiRng;

        state.DrawPile = deck.ToArray().ToList();

        var encounter = encounterId.HasValue
            ? (ActOneEncounter)encounterId.Value
            : SelectFirstCombatEncounter(rng);
        state.EncounterId = (int)encounter;
        state.IsEliteCombat = IsEliteEncounter(encounter);
        // Use state.NicheHpRng (set by caller) as the dedicated HP RNG matching
        // RunState.Rng.Niche.  Null falls back to the main combat rng.
        _currentAscension = state.AscensionLevel;
        _currentNicheHpRng = state.NicheHpRng;
        _usedNicheHps = _currentNicheHpRng != null ? new HashSet<int>() : null;
        state.Enemies = CreateEncounter(
            encounter,
            rng,
            encounterRngSeed,
            completedCombatRoomsBeforeCurrent,
            state.AscensionLevel
        );
        _currentNicheHpRng = null;
        _usedNicheHps = null;
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng, state.AiRng, state.AscensionLevel);
        EnemyAI.UpdateSecondaryIntents(state.Enemies);

        // Shuffle draw pile (skip if caller pre-shuffled it) and deal opening hand of 5.
        // Use the dedicated Shuffle stream when available (matches the game's
        // State.Rng.Shuffle); ShufflePile is the same Fisher-Yates as GameRng.Shuffle.
        if (!deckPreShuffled)
        {
            CardEffects.ShufflePile(state.DrawPile, state.ShuffleRng ?? rng);
        }

        RelicEffects.ApplyBeforeOpeningHand(state, rng);

        int handDraw = ApplyTurnOneDrawPileReorder(state.DrawPile, 5);

        for (int i = 0; i < handDraw && state.DrawPile.Count > 0; i++)
        {
            // Through the same roll the ordinary draw path uses: the opening hand is a
            // draw, so a Slither-enchanted card in it costs what the stream says rather
            // than what it is printed at.
            state.Hand.Add(CardEffects.RollSlitherCost(state, state.DrawPile[0], rng));
            state.RemoveFromDrawPileAt(0);
        }

        RelicEffects.ApplyCombatStart(state, rng);
        RelicEffects.ApplyStartOfPlayerTurn(state, rng);
        RelicEffects.ApplyAfterPlayerHpChanged(state);
    }

    /// <summary>The game's <c>CardPile.MaxCardsInHand</c>.</summary>
    private const int MaxCardsInHand = 10;

    /// <summary>
    /// Port of the game's turn-1 draw-pile reorder (decompiled
    /// <c>MegaCrit.Sts2.Core.Combat/CombatManager.cs</c> ~line 658), applied after the
    /// shuffle and before the opening draw: cards flagged
    /// <c>ShouldStartAtBottomOfDrawPile</c> go to the bottom, then Innate cards (minus
    /// those just sent to the bottom) go to the top, and the draw count is raised to
    /// cover every Innate card, capped at the max hand size.
    /// </summary>
    /// <param name="drawPile">Draw pile, index 0 = top. Reordered in place.</param>
    /// <param name="handDraw">Base number of cards to draw.</param>
    /// <returns>The possibly-increased number of cards to draw.</returns>
    public static int ApplyTurnOneDrawPileReorder(List<CardInstance> drawPile, int handDraw)
    {
        // The game moves each bottom-sorted card with Remove+Add, so afterwards they
        // sit in the last slots in their original relative order — a stable partition.
        var bottom = new List<CardInstance>();
        var kept = new List<CardInstance>();
        foreach (var card in drawPile)
        {
            (card.StartsAtBottomOfDrawPile() ? bottom : kept).Add(card);
        }

        // Innate cards are then selected from the post-move pile and Except'd against
        // the bottom group, so a card that is both stays at the bottom.
        var innate = new List<CardInstance>();
        var rest = new List<CardInstance>();
        foreach (var card in kept)
        {
            (card.IsInnate() ? innate : rest).Add(card);
        }

        // MoveToTopInternal inserts at index 0, so walking the innate cards in pile
        // order leaves that block reversed relative to where it started.
        innate.Reverse();

        drawPile.Clear();
        drawPile.AddRange(innate);
        drawPile.AddRange(rest);
        drawPile.AddRange(bottom);

        return Math.Min(Math.Max(handDraw, innate.Count), MaxCardsInHand);
    }

    private static int[] StarterDeck() =>
        [
            .. Enumerable.Repeat(StrikeId, 5),
            .. Enumerable.Repeat(DefendId, 4),
            BashId,
            AscendersBaneId,
        ];

    private static List<EnemyState> CreateEncounter(
        ActOneEncounter encounter,
        Random rng,
        int? encounterRngSeed = null,
        int completedCombatRoomsBeforeCurrent = -1,
        int ascension = Ascension.DefaultLevel
    ) =>
        encounter switch
        {
            ActOneEncounter.Cultists =>
            [
                CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.DampCultist, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.Chompers =>
            [
                CreateChomper(rng, new Intent(IntentType.Attack, 18)),
                CreateChomper(rng, new Intent(IntentType.Debuff, 3), moveIndex: 1),
            ],

            ActOneEncounter.NibbitsWeak =>
            [
                CreateEnemy(
                    KE.Nibbit,
                    rng,
                    // Nibbit.ButtDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 12)
                    )
                ),
            ],

            ActOneEncounter.SlimesWeak => CreateSlimeEncounter(rng, encounterRngSeed, ascension),

            ActOneEncounter.Exoskeletons =>
            [
                CreateExoskeleton(rng, new Intent(IntentType.Attack, 4)),
                CreateExoskeleton(rng, new Intent(IntentType.Attack, 9)),
                CreateExoskeleton(rng, new Intent(IntentType.Buff, 0)),
                CreateExoskeleton(rng, new Intent(IntentType.Attack, 9)),
            ],

            ActOneEncounter.Inklets =>
            [
                CreateInklet(rng, new Intent(IntentType.Attack, 4)),
                CreateInklet(rng, new Intent(IntentType.Attack, 9), moveIndex: 1),
                CreateInklet(rng, new Intent(IntentType.Attack, 4)),
            ],

            ActOneEncounter.TwoTailedRats => CreateTwoTailedRatsEncounter(rng, encounterRngSeed),

            ActOneEncounter.GremlinMerc => [CreateGremlinMerc(rng)],

            ActOneEncounter.FuzzyWurmCrawler =>
            [
                CreateEnemy(
                    KE.FuzzyWurmCrawler,
                    rng,
                    // FuzzyWurmCrawler.AcidGoopDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 4)
                    )
                ),
            ],

            ActOneEncounter.CorpseSlugs => CreateCorpseSlugsEncounter(
                rng,
                completedCombatRoomsBeforeCurrent is >= 0 and < 3,
                encounterRngSeed,
                ascension
            ),

            ActOneEncounter.SludgeSpinner =>
            [
                CreateEnemy(
                    KE.SludgeSpinner,
                    rng,
                    // SludgeSpinner.OilSprayDamage (OIL_SPRAY is attack + debuff)
                    new Intent(
                        IntentType.Debuff,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
                    )
                ),
            ],

            ActOneEncounter.ShrinkerBeetle =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
            ],

            // SeapunkWeak is one Seapunk; SeapunkNormal is a Calcified Cultist AND a
            // Seapunk. They are two encounters in the game and one entry here, the same
            // way CorpseSlugs is -- so the variant has to come off the weak flag. Without
            // it the fourth normal fight of an Underdocks run was a lone Seapunk where
            // the live run had a cultist beside it, and the sequence check could not see
            // it: verify_run_generation normalises the WEAK/NORMAL suffix away.
            ActOneEncounter.Seapunk => completedCombatRoomsBeforeCurrent is >= 0 and < 3
                ?
                [
                    CreateEnemy(
                        KE.Seapunk,
                        rng,
                        // Seapunk.SeaKickDamage
                        new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 11)
                        )
                    ),
                ]
                :
                [
                    CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                    CreateEnemy(
                        KE.Seapunk,
                        rng,
                        new Intent(
                            IntentType.Attack,
                            Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 11)
                        )
                    ),
                ],

            ActOneEncounter.Toadpoles =>
            [
                CreateEnemy(KE.Toadpole, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(
                    KE.Toadpole,
                    rng,
                    // Toadpole.WhirlDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 8, 7)
                    ),
                    moveIndex: 2
                ),
            ],

            ActOneEncounter.Mawler =>
            [
                CreateEnemy(KE.Mawler, rng, new Intent(IntentType.Attack, 10)),
            ],

            ActOneEncounter.NibbitsNormal =>
            [
                CreateEnemy(
                    KE.Nibbit,
                    rng,
                    // Nibbit.SliceDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 7, 6)
                    ),
                    moveIndex: 1
                ),
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Buff, 0), moveIndex: 2),
            ],

            ActOneEncounter.SlimesNormal => CreateSlimesNormalEncounter(rng, encounterRngSeed),

            ActOneEncounter.FlyconidNormal => CreateFlyconidNormalEncounter(rng, encounterRngSeed),

            ActOneEncounter.SnappingJaxfruitNormal =>
            [
                CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Attack, 4)),
                CreateEnemy(KE.Flyconid, rng, new Intent(IntentType.Attack, 8)),
            ],

            ActOneEncounter.CubexConstruct => [CreateCubexConstruct(rng)],

            ActOneEncounter.VineShambler =>
            [
                CreateEnemy(KE.VineShambler, rng, new Intent(IntentType.Attack, 14)),
            ],

            ActOneEncounter.OvergrowthCrawlers =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
                CreateEnemy(
                    KE.FuzzyWurmCrawler,
                    rng,
                    // FuzzyWurmCrawler.AcidGoopDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 6, 4)
                    )
                ),
            ],

            ActOneEncounter.CultistAndSeapunk =>
            [
                CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(
                    KE.Seapunk,
                    rng,
                    // Seapunk.SeaKickDamage
                    new Intent(
                        IntentType.Attack,
                        Ascension.Value(ascension, Ascension.DeadlyEnemies, 13, 11)
                    )
                ),
            ],

            ActOneEncounter.FossilStalker => [CreateFossilStalker(rng)],

            ActOneEncounter.PunchConstruct =>
            [
                CreatePunchConstruct(rng, startsWithFastPunch: rng.Next(2) == 0),
            ],

            ActOneEncounter.SewerClam => [CreateSewerClam(rng)],

            ActOneEncounter.HauntedShip =>
            [
                CreateEnemy(KE.HauntedShip, rng, new Intent(IntentType.Debuff, 5)),
            ],

            ActOneEncounter.SlitheringStrangler => CreateSlitheringStranglerEncounter(
                rng,
                encounterRngSeed
            ),

            ActOneEncounter.RubyRaiders => CreateRubyRaiders(rng, encounterRngSeed),

            ActOneEncounter.SkulkingColony => [CreateSkulkingColony(rng)],

            ActOneEncounter.Fogmog => [CreateEnemy(KE.Fogmog, rng, new Intent(IntentType.Buff, 0))],

            ActOneEncounter.LivingFog =>
            [
                CreateEnemy(KE.LivingFog, rng, new Intent(IntentType.Debuff, 9)),
            ],

            ActOneEncounter.BowlbugsWeak => CreateBowlbugsWeakEncounter(rng),

            ActOneEncounter.Bowlbugs => CreateBowlbugsEncounter(rng),

            ActOneEncounter.Tunneler =>
            [
                CreateEnemy(KE.Tunneler, rng, new Intent(IntentType.Attack, 15)),
            ],

            ActOneEncounter.TunnelerAndChomper =>
            [
                CreateChomper(rng, new Intent(IntentType.Debuff, 3)),
                CreateEnemy(KE.Tunneler, rng, new Intent(IntentType.Attack, 15)),
            ],

            ActOneEncounter.ThievingHopper =>
            [
                CreateEnemy(KE.ThievingHopper, rng, new Intent(IntentType.Attack, 19)),
            ],

            ActOneEncounter.Mytes =>
            [
                CreateEnemy(KE.Myte, rng, new Intent(IntentType.Debuff, 2)),
                CreateEnemy(KE.Myte, rng, new Intent(IntentType.Attack, 6)),
            ],

            ActOneEncounter.SlumberingBeetle =>
            [
                CreateEnemy(KE.BowlbugRock, rng, new Intent(IntentType.Attack, 16)),
                CreateBowlbugWorker(KE.BowlbugSilk, rng),
                CreateSlumberingBeetle(rng),
            ],

            ActOneEncounter.SpinyToad =>
            [
                CreateEnemy(KE.SpinyToad, rng, new Intent(IntentType.Buff, 5)),
            ],

            ActOneEncounter.Ovicopter =>
            [
                CreateEnemy(KE.Ovicopter, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.LouseProgenitor =>
            [
                CreateEnemy(KE.LouseProgenitor, rng, new Intent(IntentType.Attack, 10)),
            ],

            ActOneEncounter.HunterKiller =>
            [
                CreateEnemy(KE.HunterKiller, rng, new Intent(IntentType.Debuff, 1)),
            ],

            ActOneEncounter.Axebot =>
            [
                CreateEnemy(KE.Axebot, rng, new Intent(IntentType.Attack, 14), moveIndex: 2),
            ],

            ActOneEncounter.DevotedSculptor =>
            [
                CreateEnemy(KE.DevotedSculptor, rng, new Intent(IntentType.Buff, 9)),
            ],

            ActOneEncounter.Fabricator =>
            [
                CreateEnemy(
                    KE.Fabricator,
                    rng,
                    rng.Next(2) == 0
                        ? new Intent(IntentType.Buff, 0)
                        : new Intent(IntentType.Attack, 21)
                ),
            ],

            ActOneEncounter.FrogKnight => [CreateFrogKnight(rng)],

            ActOneEncounter.GlobeHead =>
            [
                CreateEnemy(KE.GlobeHead, rng, new Intent(IntentType.Attack, 14)),
            ],

            ActOneEncounter.TurretOperator => [CreateLivingShield(rng), CreateTurretOperator(rng)],

            ActOneEncounter.OwlMagistrate =>
            [
                CreateEnemy(KE.OwlMagistrate, rng, new Intent(IntentType.Attack, 17)),
            ],

            ActOneEncounter.ScrollsWeak => CreateScrollsEncounter(rng, 3),

            ActOneEncounter.Scrolls => CreateScrollsEncounter(rng, 4),

            ActOneEncounter.SlimedBerserker =>
            [
                CreateEnemy(KE.SlimedBerserker, rng, new Intent(IntentType.Debuff, 10)),
            ],

            ActOneEncounter.LostAndForgotten =>
            [
                CreateEnemy(KE.TheLost, rng, new Intent(IntentType.Debuff, 2)),
                CreateEnemy(KE.TheForgotten, rng, new Intent(IntentType.Debuff, 2)),
            ],

            ActOneEncounter.Obscura =>
            [
                CreateEnemy(KE.TheObscura, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.ConstructMenagerie =>
            [
                CreatePunchConstruct(rng, startsWithFastPunch: false),
                CreateCubexConstruct(rng),
                CreateCubexConstruct(rng),
            ],

            ActOneEncounter.DenseVegetation =>
            [
                CreateEnemy(KE.Wriggler, rng, new Intent(IntentType.Attack, 7)),
                CreateEnemy(KE.Wriggler, rng, new Intent(IntentType.Buff, 1), moveIndex: 1),
                CreateEnemy(KE.Wriggler, rng, new Intent(IntentType.Attack, 7)),
                CreateEnemy(KE.Wriggler, rng, new Intent(IntentType.Buff, 1), moveIndex: 1),
            ],

            ActOneEncounter.PunchOff =>
            [
                CreatePunchOffConstruct(rng, startsWithFastPunch: true),
                CreatePunchOffConstruct(rng, startsWithFastPunch: false),
            ],

            ActOneEncounter.FakeMerchant =>
            [
                CreateEnemy(KE.FakeMerchant, rng, new Intent(IntentType.Attack, 15)),
            ],

            ActOneEncounter.MysteriousKnight => [CreateMysteriousKnight(rng)],

            ActOneEncounter.BattlewornDummy1 =>
            [
                CreateEnemy(KE.BattleFriendV1, rng, new Intent(IntentType.Unknown, 0)),
            ],

            ActOneEncounter.BattlewornDummy2 =>
            [
                CreateEnemy(KE.BattleFriendV2, rng, new Intent(IntentType.Unknown, 0)),
            ],

            ActOneEncounter.BattlewornDummy3 =>
            [
                CreateEnemy(KE.BattleFriendV3, rng, new Intent(IntentType.Unknown, 0)),
            ],

            ActOneEncounter.BygoneEffigy =>
            [
                CreateEnemy(KE.BygoneEffigy, rng, new Intent(IntentType.Unknown, 0)),
            ],

            ActOneEncounter.Entomancer =>
            [
                CreateEnemy(KE.Entomancer, rng, new Intent(IntentType.Attack, 24), moveIndex: 1),
            ],

            ActOneEncounter.InfestedPrisms =>
            [
                CreateEnemy(KE.InfestedPrism, rng, new Intent(IntentType.Attack, 17)),
            ],

            ActOneEncounter.PhrogParasite =>
            [
                CreateEnemy(KE.PhrogParasite, rng, new Intent(IntentType.Debuff, 3)),
            ],

            ActOneEncounter.SoulNexus =>
            [
                CreateEnemy(KE.SoulNexus, rng, new Intent(IntentType.Attack, 31)),
            ],

            ActOneEncounter.TerrorEel =>
            [
                CreateEnemy(KE.TerrorEel, rng, new Intent(IntentType.Attack, 18)),
            ],

            ActOneEncounter.Byrdonis =>
            [
                CreateEnemy(KE.Byrdonis, rng, new Intent(IntentType.Attack, 19)),
            ],

            ActOneEncounter.Decimillipede => CreateDecimillipede(rng),

            ActOneEncounter.Knights =>
            [
                CreateEnemy(KE.FlailKnight, rng, new Intent(IntentType.Attack, 17), moveIndex: 2),
                CreateEnemy(KE.SpectralKnight, rng, new Intent(IntentType.Debuff, 2)),
                CreateEnemy(KE.MagiKnight, rng, new Intent(IntentType.Attack, 7)),
            ],

            ActOneEncounter.MechaKnight => [CreateMechaKnight(rng)],

            ActOneEncounter.PhantasmalGardeners => CreatePhantasmalGardeners(rng),

            ActOneEncounter.Aeonglass => [CreateAeonglass(rng)],

            ActOneEncounter.CeremonialBeast =>
            [
                CreateEnemy(KE.CeremonialBeast, rng, new Intent(IntentType.Buff, 160)),
            ],

            ActOneEncounter.KaiserCrab =>
            [
                CreateEnemy(KE.Crusher, rng, new Intent(IntentType.Attack, 21)),
                CreateEnemy(KE.Rocket, rng, new Intent(IntentType.Attack, 4)),
            ],

            ActOneEncounter.KnowledgeDemon =>
            [
                CreateEnemy(KE.KnowledgeDemon, rng, new Intent(IntentType.Debuff, 0)),
            ],

            ActOneEncounter.LagavulinMatriarch => [CreateLagavulinMatriarch(rng)],

            ActOneEncounter.Queen =>
            [
                CreateEnemy(KE.TorchHeadAmalgam, rng, new Intent(IntentType.Attack, 19)),
                CreateEnemy(KE.Queen, rng, new Intent(IntentType.Debuff, 3)),
            ],

            ActOneEncounter.SoulFysh =>
            [
                CreateEnemy(KE.SoulFysh, rng, new Intent(IntentType.Debuff, 2)),
            ],

            ActOneEncounter.TestSubject => [CreateTestSubject(rng)],

            ActOneEncounter.TheInsatiable =>
            [
                CreateEnemy(KE.TheInsatiable, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.TheKin =>
            [
                CreateKinFollower(rng, startsWithDance: true),
                CreateKinFollower(rng, startsWithDance: false),
                CreateEnemy(KE.KinPriest, rng, new Intent(IntentType.Attack, 9)),
            ],

            ActOneEncounter.Vantom => [CreateVantom(rng)],

            ActOneEncounter.WaterfallGiant =>
            [
                CreateEnemy(KE.WaterfallGiant, rng, new Intent(IntentType.Buff, 20)),
            ],

            ActOneEncounter.Architect =>
            [
                CreateEnemy(KE.Architect, rng, new Intent(IntentType.Unknown, 0)),
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(encounter), encounter, null),
        };

    private static ActOneEncounter SelectFirstCombatEncounter(Random rng)
    {
        var pool = rng.Next(2) == 0 ? OvergrowthWeakEncounters : UnderdocksWeakEncounters;
        return pool[rng.Next(pool.Length)];
    }

    /// <summary>
    /// The act-1 elites, named rather than described as a range.
    /// </summary>
    /// <remarks>
    /// This was <c>>= BygoneEffigy and &lt;= WaterfallGiant</c>, which was true when
    /// WaterfallGiant was the last name in the enum and quietly stopped being true when
    /// Architect and SkulkingColony were appended after it: a Skulking Colony elite did
    /// not read as one, so Booming Conch never fired and a live capture opened that fight
    /// with seven cards and four energy where the emulator had five and three. A range
    /// over an enum is a promise about declaration ORDER that nothing enforces.
    ///
    /// <para>
    /// It also swept up every boss, and the game does not: BoomingConch asks for
    /// <c>CurrentRoom.RoomType == RoomType.Elite</c>, and a boss room is RoomType.Boss.
    /// Act 2's elites are deliberately absent — the emulator models act 1, and listing
    /// names it cannot reach would be guessing.
    /// </para>
    /// </remarks>
    private static readonly ActOneEncounter[] EliteEncounters =
    [
        // Overgrowth, per Acts/Overgrowth.cs.
        ActOneEncounter.BygoneEffigy,
        ActOneEncounter.Byrdonis,
        ActOneEncounter.PhrogParasite,
        // Underdocks, per Acts/Underdocks.cs.
        ActOneEncounter.PhantasmalGardeners,
        ActOneEncounter.SkulkingColony,
        ActOneEncounter.TerrorEel,
    ];

    private static bool IsEliteEncounter(ActOneEncounter encounter) =>
        Array.IndexOf(EliteEncounters, encounter) >= 0;

    private static List<EnemyState> CreateSlimeEncounter(
        Random rng,
        int? encounterRngSeed = null,
        int ascension = Ascension.DefaultLevel
    )
    {
        // SlimesWeak.GenerateMonsters, call for call. It draws THREE times from the
        // encounter's own Rng, not two:
        //
        //     m1 = Rng.NextItem(smalls);  smalls.Remove(m1);
        //     m2 = Rng.NextItem(smalls);            // one item left — still a draw
        //     add m1; add Rng.NextItem(mediums); add m2;
        //
        // The second draw is forced (one candidate) but it advances the stream, so
        // inferring the second small "for free" — what this used to do — read the
        // MEDIUM slime off the wrong draw and produced the wrong roster.
        //
        // Without a seed there is no encounter stream to speak of; fall back to the
        // combat rng so the direct env still produces something playable, and note that
        // such a roster is NOT comparable to the live game.
        int[] smalls = [KE.LeafSlimeS, KE.TwigSlimeS];
        int[] mediums = [KE.LeafSlimeM, KE.TwigSlimeM];
        int firstSmall;
        int secondSmall;
        int middle;
        if (encounterRngSeed.HasValue)
        {
            var typeRng = EncounterRng.Stream(encounterRngSeed.Value);
            var remaining = smalls.ToList();
            firstSmall = remaining[typeRng.NextInt(0, remaining.Count)];
            remaining.Remove(firstSmall);
            secondSmall = remaining[typeRng.NextInt(0, remaining.Count)];
            middle = mediums[typeRng.NextInt(0, mediums.Length)];
        }
        else
        {
            firstSmall = rng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS;
            middle = rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM;
            secondSmall = firstSmall == KE.LeafSlimeS ? KE.TwigSlimeS : KE.LeafSlimeS;
        }

        // LeafSlimeS starting intent depends on slot: firstSmall=Attack(0), secondSmall=Debuff(1).
        // TwigSlimeS always starts with Attack(5). These are slot-deterministic, not niche-RNG-based.
        var firstIntent =
            firstSmall == KE.LeafSlimeS
                // LeafSlimeS.TackleDamage
                ? new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 4, 3)
                )
                // TwigSlimeS.TackleDamage
                : new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4)
                );
        var secondIntent =
            secondSmall == KE.LeafSlimeS
                ? new Intent(IntentType.Debuff, 1)
                : new Intent(
                    IntentType.Attack,
                    Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4)
                );
        int secondMoveIndex = secondSmall == KE.LeafSlimeS ? 1 : 0;

        return
        [
            CreateEnemy(firstSmall, rng, firstIntent),
            CreateSlime(middle, rng),
            CreateEnemy(secondSmall, rng, secondIntent, secondMoveIndex),
        ];
    }

    private static List<EnemyState> CreateBowlbugsWeakEncounter(Random rng) =>
        [
            CreateEnemy(KE.BowlbugRock, rng, new Intent(IntentType.Attack, 16)),
            CreateBowlbugWorker(rng.Next(2) == 0 ? KE.BowlbugEgg : KE.BowlbugNectar, rng),
        ];

    private static List<EnemyState> CreateBowlbugsEncounter(Random rng)
    {
        int[] workers = [KE.BowlbugEgg, KE.BowlbugSilk, KE.BowlbugNectar];
        return
        [
            CreateEnemy(KE.BowlbugRock, rng, new Intent(IntentType.Attack, 16)),
            .. workers.OrderBy(_ => rng.Next()).Take(2).Select(id => CreateBowlbugWorker(id, rng)),
        ];
    }

    private static EnemyState CreateBowlbugWorker(int defId, Random rng) =>
        defId switch
        {
            KE.BowlbugEgg => CreateEnemy(defId, rng, new Intent(IntentType.Attack, 8)),
            KE.BowlbugNectar => CreateEnemy(defId, rng, new Intent(IntentType.Attack, 3)),
            KE.BowlbugSilk => CreateEnemy(defId, rng, new Intent(IntentType.Debuff, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(defId), defId, null),
        };

    private static EnemyState CreateSlumberingBeetle(Random rng)
    {
        var enemy = CreateEnemy(KE.SlumberingBeetle, rng, new Intent(IntentType.Unknown, 0));
        enemy.Block = 18;
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 18);
        return enemy;
    }

    private static EnemyState CreateFrogKnight(Random rng)
    {
        var enemy = CreateEnemy(
            KE.FrogKnight,
            rng,
            new Intent(IntentType.Attack, 14),
            moveIndex: 2
        );
        enemy.Block = 19;
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 19);
        return enemy;
    }

    private static EnemyState CreateLivingShield(Random rng) =>
        CreateEnemy(KE.LivingShield, rng, new Intent(IntentType.Attack, 6));

    private static EnemyState CreateTurretOperator(Random rng)
    {
        var enemy = CreateEnemy(KE.TurretOperator, rng, new Intent(IntentType.Attack, 20));
        enemy.Block = 25;
        return enemy;
    }

    private static List<EnemyState> CreateScrollsEncounter(Random rng, int count)
    {
        int firstMove = rng.Next(3);
        var enemies = new List<EnemyState>();
        for (int i = 0; i < count; i++)
        {
            int moveIndex = count == 4 && i == 3 ? 2 : (firstMove + i) % 3;
            var scroll = CreateEnemy(KE.ScrollOfBiting, rng, ScrollIntent(moveIndex), moveIndex);
            if (i < 3)
            {
                BuffSystem.Apply(scroll.Buffs, BuffId.PaperCuts, 2);
            }

            enemies.Add(scroll);
        }
        return enemies;
    }

    private static Intent ScrollIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 16),
            1 => new Intent(IntentType.Attack, 12),
            _ => new Intent(IntentType.Buff, 2),
        };

    private static EnemyState CreatePunchOffConstruct(Random rng, bool startsWithFastPunch)
    {
        var enemy = CreatePunchConstruct(rng, startsWithFastPunch);
        int hpReduction = rng.Next(2, 10);
        enemy.Hp = Math.Max(1, enemy.Hp - hpReduction);
        return enemy;
    }

    private static EnemyState CreateMysteriousKnight(Random rng)
    {
        var enemy = CreateEnemy(
            KE.FlailKnight,
            rng,
            new Intent(IntentType.Attack, 23),
            moveIndex: 2
        );
        enemy.Block = 6;
        BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 6);
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 6);
        return enemy;
    }

    private static List<EnemyState> CreateDecimillipede(Random rng)
    {
        int starter = rng.Next(3);
        var enemies = new List<EnemyState>(3);
        for (int i = 0; i < 3; i++)
        {
            int moveIndex = (starter + i) % 3;
            var enemy = CreateEnemy(
                KE.DecimillipedeSegment,
                rng,
                DecimillipedeIntent(moveIndex),
                moveIndex
            );
            MakeDecimillipedeHpEvenAndUnique(enemy, enemies);
            enemies.Add(enemy);
        }
        return enemies;
    }

    private static Intent DecimillipedeIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 12),
            1 => new Intent(IntentType.Attack, 7),
            _ => new Intent(IntentType.Attack, 9),
        };

    private static void MakeDecimillipedeHpEvenAndUnique(
        EnemyState enemy,
        List<EnemyState> existing
    )
    {
        int hp = enemy.MaxHp;
        if (hp % 2 == 1)
        {
            hp++;
        }

        while (existing.Any(e => e.MaxHp == hp))
        {
            hp += 2;
            if (hp > 52)
            {
                hp = 46;
            }
        }
        enemy.MaxHp = hp;
        enemy.Hp = hp;
    }

    private static EnemyState CreateMechaKnight(Random rng)
    {
        var enemy = CreateEnemy(KE.MechaKnight, rng, new Intent(IntentType.Attack, 30));
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 3);
        return enemy;
    }

    private static List<EnemyState> CreatePhantasmalGardeners(Random rng) =>
        [
            CreateEnemy(KE.PhantasmalGardener, rng, new Intent(IntentType.Attack, 3), moveIndex: 2),
            CreateEnemy(KE.PhantasmalGardener, rng, new Intent(IntentType.Attack, 5)),
            CreateEnemy(KE.PhantasmalGardener, rng, new Intent(IntentType.Attack, 7), moveIndex: 1),
            CreateEnemy(KE.PhantasmalGardener, rng, new Intent(IntentType.Buff, 3), moveIndex: 3),
        ];

    private static EnemyState CreateAeonglass(Random rng)
    {
        var enemy = CreateEnemy(KE.Aeonglass, rng, new Intent(IntentType.Attack, 32));
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 3);
        return enemy;
    }

    private static EnemyState CreateLagavulinMatriarch(Random rng)
    {
        var enemy = CreateEnemy(KE.LagavulinMatriarch, rng, new Intent(IntentType.Unknown, 0));
        enemy.Block = 12;
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 12);
        BuffSystem.Apply(enemy.Buffs, BuffId.Asleep, 3);
        return enemy;
    }

    private static EnemyState CreateTestSubject(Random rng)
    {
        var enemy = CreateEnemy(KE.TestSubject, rng, new Intent(IntentType.Attack, 22));
        BuffSystem.Apply(enemy.Buffs, BuffId.Adaptable, 1);
        BuffSystem.Apply(enemy.Buffs, BuffId.Enrage, 3);
        return enemy;
    }

    private static EnemyState CreateKinFollower(Random rng, bool startsWithDance) =>
        CreateEnemy(
            KE.KinFollower,
            rng,
            startsWithDance ? new Intent(IntentType.Buff, 3) : new Intent(IntentType.Attack, 5),
            startsWithDance ? 2 : 0
        );

    private static EnemyState CreateVantom(Random rng)
    {
        var enemy = CreateEnemy(KE.Vantom, rng, new Intent(IntentType.Attack, 8));
        BuffSystem.Apply(enemy.Buffs, BuffId.Slippery, 9);
        return enemy;
    }

    private static List<EnemyState> CreateSlimesNormalEncounter(
        Random rng,
        int? encounterRngSeed = null
    )
    {
        // SlimesNormal.GenerateMonsters: ONE NextBool on the encounter's own stream
        // decides which small slime leads; the two mediums are fixed. Rolling this on
        // the combat rng instead — what this used to do — gets it right half the time.
        bool leafSmallFirst = encounterRngSeed.HasValue
            ? EncounterRng.Stream(encounterRngSeed.Value).NextBool()
            : rng.Next(2) == 0;
        int firstSmall = leafSmallFirst ? KE.LeafSlimeS : KE.TwigSlimeS;
        int secondSmall = leafSmallFirst ? KE.TwigSlimeS : KE.LeafSlimeS;

        return
        [
            CreateSlime(KE.TwigSlimeM, rng),
            CreateSlime(KE.LeafSlimeM, rng),
            CreateSlime(firstSmall, rng),
            CreateSlime(secondSmall, rng),
        ];
    }

    private static EnemyState CreateSlime(int defId, Random rng)
    {
        return defId switch
        {
            KE.LeafSlimeS => CreateEnemy(
                defId,
                rng,
                new Intent(IntentType.Debuff, 1),
                moveIndex: 1
            ),

            KE.TwigSlimeS => CreateEnemy(defId, rng, new Intent(IntentType.Attack, 5)),

            KE.LeafSlimeM => CreateEnemy(
                defId,
                rng,
                new Intent(IntentType.Debuff, 2),
                moveIndex: 1
            ),

            KE.TwigSlimeM => CreateEnemy(
                defId,
                rng,
                new Intent(IntentType.Debuff, 1),
                moveIndex: 1
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(defId), defId, null),
        };
    }

    private static List<EnemyState> CreateTwoTailedRatsEncounter(Random rng, int? encounterRngSeed)
    {
        // TwoTailedRatsNormal.GenerateMonsters: Rng.NextInt(3) on the encounter's own
        // stream picks the first rat's StarterMoveIndex, and the other two follow it. On
        // the combat rng instead, all three openings were wrong together.
        int firstMove = encounterRngSeed.HasValue
            ? EncounterRng.Stream(encounterRngSeed.Value).NextInt(0, 3)
            : rng.Next(3);
        // TwoTailedRatsNormal places its three in Slots[2..4] of five, which is what
        // decides where a summoned rat joins the roster.
        return
        [
            CreateTwoTailedRat(rng, firstMove, slot: 2),
            CreateTwoTailedRat(rng, (firstMove + 1) % 3, slot: 3),
            CreateTwoTailedRat(rng, (firstMove + 2) % 3, slot: 4),
        ];
    }

    /// <summary>
    /// FlyconidNormal.GenerateMonsters: one NextItem over the two medium slimes on the
    /// encounter's own stream, then the Flyconid.
    /// </summary>
    private static List<EnemyState> CreateFlyconidNormalEncounter(Random rng, int? encounterRngSeed)
    {
        var stream = encounterRngSeed.HasValue ? EncounterRng.Stream(encounterRngSeed.Value) : null;
        int medium = (stream?.NextInt(0, 2) ?? rng.Next(2)) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM;
        return
        [
            CreateSlime(medium, rng),
            CreateEnemy(KE.Flyconid, rng, new Intent(IntentType.Attack, 8)),
        ];
    }

    /// <summary>
    /// SlitheringStranglerNormal.GenerateMonsters: a NextItem over the three
    /// SecondaryEnemyType values, then a slime NextItem for two of the three branches —
    /// all on the encounter's own stream, so the roster size itself depends on it.
    /// </summary>
    private static List<EnemyState> CreateSlitheringStranglerEncounter(
        Random rng,
        int? encounterRngSeed
    )
    {
        var stream = encounterRngSeed.HasValue ? EncounterRng.Stream(encounterRngSeed.Value) : null;
        var enemies = new List<EnemyState>();
        switch (stream?.NextInt(0, 3) ?? rng.Next(3))
        {
            case 0:
                enemies.Add(
                    CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Attack, 4))
                );
                break;
            case 1:
                enemies.Add(
                    CreateSlime(
                        (stream?.NextInt(0, 2) ?? rng.Next(2)) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM,
                        rng
                    )
                );
                break;
            default:
                for (int i = 0; i < 2; i++)
                {
                    enemies.Add(
                        CreateSlime(
                            (stream?.NextInt(0, 2) ?? rng.Next(2)) == 0
                                ? KE.LeafSlimeS
                                : KE.TwigSlimeS,
                            rng
                        )
                    );
                }

                break;
        }
        enemies.Add(CreateEnemy(KE.SlitheringStrangler, rng, new Intent(IntentType.Debuff, 3)));
        return enemies;
    }

    /// <summary>
    /// RubyRaidersNormal.GenerateMonsters: three NextItem draws on the encounter's own
    /// stream over the raiders not yet taken — every type is capped at one, so the list
    /// shrinks 5, 4, 3 — in the order the count dictionary declares them. The emulator
    /// built a fixed Tracker/Assassin/Brute trio, which is one of the sixty possibilities.
    /// </summary>
    private static List<EnemyState> CreateRubyRaiders(Random rng, int? encounterRngSeed)
    {
        int[] pool =
        [
            KE.AxeRubyRaider,
            KE.AssassinRubyRaider,
            KE.BruteRubyRaider,
            KE.CrossbowRubyRaider,
            KE.TrackerRubyRaider,
        ];

        var stream = encounterRngSeed.HasValue ? EncounterRng.Stream(encounterRngSeed.Value) : null;
        var remaining = pool.ToList();
        var raiders = new List<EnemyState>();
        for (int i = 0; i < 3; i++)
        {
            int index = stream?.NextInt(0, remaining.Count) ?? rng.Next(remaining.Count);
            int defId = remaining[index];
            remaining.RemoveAt(index);
            raiders.Add(CreateEnemy(defId, rng, new Intent(IntentType.Unknown, 0)));
        }

        return raiders;
    }

    private static EnemyState CreateTwoTailedRat(Random rng, int moveIndex, int slot = -1)
    {
        var enemy = CreateEnemy(KE.TwoTailedRat, rng, RatIntent(moveIndex), moveIndex);
        enemy.Slot = slot;
        BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, 2);
        return enemy;
    }

    private static Intent RatIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 9),
            1 => new Intent(IntentType.Attack, 7),
            _ => new Intent(IntentType.Debuff, 1),
        };

    private static List<EnemyState> CreateCorpseSlugsEncounter(
        Random rng,
        bool weak = false,
        int? encounterRngSeed = null,
        int ascension = Ascension.DefaultLevel
    )
    {
        // CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves: the encounter rolls ONE
        // number and then deals consecutive starting moves to the slugs.
        //
        //     int num = rng.NextInt(3);
        //     foreach (slug) { slug.StarterMoveIdx = num % 3; num++; }
        //
        // The hardcoded (2, 0) this used to return is that sequence for a roll of 2 —
        // right one time in three, which is exactly how often the sweep saw it pass.
        int count = weak ? 2 : 3;
        int start = encounterRngSeed.HasValue
            ? EncounterRng.Stream(encounterRngSeed.Value).NextInt(3)
            : 2;

        var slugs = new List<EnemyState>(count);
        for (int i = 0; i < count; i++)
        {
            slugs.Add(CreateCorpseSlug(rng, (start + i) % 3, ascension: ascension));
        }

        return slugs;
    }

    private static EnemyState CreateCorpseSlug(
        Random rng,
        int moveIndex,
        int? fixedHp = null,
        // Zero means "whatever the combat being built runs at". CreateEncounter has a
        // hundred-odd CreateEnemy calls and threading the level through every one of them
        // would be a hundred chances to miss one, so it rides the same ambient field the
        // Niche HP stream already uses.
        int ascension = 0
    ) => CreateCorpseSlugEnemy(rng, moveIndex, fixedHp, ascension);

    private static EnemyState CreateCorpseSlugEnemy(
        Random rng,
        int moveIndex,
        int? fixedHp = null,
        // Zero means "whatever the combat being built runs at". CreateEncounter has a
        // hundred-odd CreateEnemy calls and threading the level through every one of them
        // would be a hundred chances to miss one, so it rides the same ambient field the
        // Niche HP stream already uses.
        int ascension = 0
    )
    {
        var enemy = CreateEnemy(
            KE.CorpseSlug,
            rng,
            CorpseSlugIntent(moveIndex, ascension),
            moveIndex,
            fixedHp
        );
        // RavenousStr is GetValueIfAscension(DeadlyEnemies, 5, 4): the 5 is the A9
        // branch, taken here as a bare literal. At A8 the slug gains 4 per dead ally,
        // and a live capture announces "7x2" -- (3 + 4) twice -- where a 5 gives 8x2.
        BuffSystem.Apply(
            enemy.Buffs,
            BuffId.Ravenous,
            Ascension.Value(ascension, Ascension.DeadlyEnemies, 5, 4)
        );
        return enemy;
    }

    // Move order is the game's MoveState declaration order in CorpseSlug.cs:
    // 0 WHIP_SLAP (MultiAttackIntent 3 x 2), 1 GLOMP (SingleAttackIntent), 2 GOOP (debuff).
    private static Intent CorpseSlugIntent(int moveIndex, int ascension = Ascension.DefaultLevel) =>
        (moveIndex % 3) switch
        {
            // WhipSlapDamage * WhipSlapRepeat; the live readout shows "3x2" and the
            // comparison comes down on total damage.
            0 => new Intent(IntentType.Attack, 3 * 2),
            1 => new Intent(
                IntentType.Attack,
                Ascension.Value(ascension, Ascension.DeadlyEnemies, 9, 8)
            ),
            _ => new Intent(IntentType.Debuff, 2),
        };

    public static EnemyState CreateEnemy(
        int defId,
        Random rng,
        Intent startingIntent,
        int moveIndex = 0,
        int? fixedHp = null,
        // Zero means "whatever the combat being built runs at". CreateEncounter has a
        // hundred-odd CreateEnemy calls and threading the level through every one of them
        // would be a hundred chances to miss one, so it rides the same ambient field the
        // Niche HP stream already uses.
        int ascension = 0
    )
    {
        var def = GeneratedData.Enemies.Get(defId);
        var band = def.HpBand(ascension > 0 ? ascension : _currentAscension);
        // Use the dedicated niche HP RNG when available, matching SetUniqueMonsterHpValue
        // which calls rng.NextItem(remaining_set) to avoid duplicate HP values across
        // creatures on the same side.
        int hp;
        if (fixedHp.HasValue)
        {
            hp = fixedHp.Value;
            if (_currentNicheHpRng != null)
            {
                _currentNicheHpRng.Next(0, band.Max - band.Min + 1);
            }
        }
        else if (_currentNicheHpRng != null)
        {
            // Build remaining set = [minHp..maxHp] minus already-used HP values.
            var range = Enumerable.Range(band.Min, band.Max - band.Min + 1).ToHashSet();
            if (_usedNicheHps != null)
            {
                range.ExceptWith(_usedNicheHps);
            }

            if (range.Count == 0)
            {
                // Fallback: full range (matches game behaviour when all values are taken).
                hp = _currentNicheHpRng.Next(band.Min, band.Max + 1);
            }
            else
            {
                // NextItem equivalent: NextInt(0, count) → ElementAt(index).
                int index = _currentNicheHpRng.Next(0, range.Count);
                hp = range.ElementAt(index);
            }
            _usedNicheHps?.Add(hp);
        }
        else
        {
            hp = rng.Next(band.Min, band.Max + 1);
        }
        var enemy = new EnemyState
        {
            DefId = defId,
            Hp = hp,
            MaxHp = hp,
            Block = 0,
            CurrentIntent = startingIntent,
            Buffs = [],
            MoveIndex = moveIndex,
        };
        ApplyIntrinsicEnemyPowers(enemy);
        return enemy;
    }

    private static void ApplyIntrinsicEnemyPowers(EnemyState enemy)
    {
        if (enemy.DefId == KE.ToughEgg)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Hatch, 1);
        }

        if (enemy.DefId == KE.Axebot)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Stock, 2);
        }

        if (enemy.DefId == KE.TerrorEel)
        {
            // TerrorEel.AfterAddedToRoom applies ShriekPower(ShriekAmount) — 75 at A8,
            // where ToughEnemies is live. It is the HP at or below which an unblocked hit
            // stuns it into TERROR.
            BuffSystem.Apply(
                enemy.Buffs,
                BuffId.Shriek,
                Ascension.Value(_currentAscension, Ascension.ToughEnemies, 75, 70)
            );
        }

        if (enemy.DefId == KE.BygoneEffigy)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Slow, 1);
        }

        if (enemy.DefId == KE.Byrdonis)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Territorial, 1);
        }

        if (enemy.DefId == KE.Entomancer)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.PersonalHive, 1);
        }

        if (enemy.DefId == KE.GlobeHead)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Galvanic, 6);
        }

        if (enemy.DefId == KE.LivingShield)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Rampart, 25);
        }

        if (enemy.DefId == KE.LouseProgenitor)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.CurlUp, 18);
        }

        if (enemy.DefId == KE.PhantasmalGardener)
        {
            // PhantasmalGardener.AfterAddedToRoom applies SkittishPower at SkittishAmount.
            BuffSystem.Apply(
                enemy.Buffs,
                BuffId.Skittish,
                Ascension.Value(_currentAscension, Ascension.ToughEnemies, 7, 6)
            );
        }

        if (enemy.DefId == KE.PhrogParasite)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Infested, 4);
        }

        if (enemy.DefId == KE.Zapbot)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.HighVoltage, 2);
        }
    }

    private static EnemyState CreateFossilStalker(Random rng)
    {
        var enemy = CreateEnemy(
            KE.FossilStalker,
            rng,
            new Intent(IntentType.Attack, 14),
            moveIndex: 1
        );
        BuffSystem.Apply(enemy.Buffs, BuffId.Suck, 3);
        return enemy;
    }

    private static EnemyState CreateChomper(Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var enemy = CreateEnemy(KE.Chomper, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 2);
        return enemy;
    }

    private static EnemyState CreateInklet(Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var enemy = CreateEnemy(KE.Inklet, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.Slippery, 1);
        return enemy;
    }

    private static EnemyState CreateGremlinMerc(Random rng)
    {
        var enemy = CreateEnemy(KE.GremlinMerc, rng, new Intent(IntentType.Attack, 16));
        BuffSystem.Apply(enemy.Buffs, BuffId.Surprise, 1);
        return enemy;
    }

    private static EnemyState CreateCubexConstruct(Random rng)
    {
        var enemy = CreateEnemy(KE.CubexConstruct, rng, new Intent(IntentType.Buff, 0));
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 1);
        return enemy;
    }

    private static EnemyState CreatePunchConstruct(Random rng, bool startsWithFastPunch)
    {
        var enemy = CreateEnemy(
            KE.PunchConstruct,
            rng,
            startsWithFastPunch
                ? new Intent(IntentType.Attack, 12)
                : new Intent(IntentType.Defend, 10),
            startsWithFastPunch ? 1 : 0
        );
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 1);
        return enemy;
    }

    private static EnemyState CreateSewerClam(Random rng)
    {
        var enemy = CreateEnemy(KE.SewerClam, rng, new Intent(IntentType.Attack, 11), moveIndex: 1);
        enemy.Block = 9;
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 9);
        return enemy;
    }

    private static EnemyState CreateExoskeleton(
        Random rng,
        Intent startingIntent,
        int moveIndex = 0
    )
    {
        var enemy = CreateEnemy(KE.Exoskeleton, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.HardToKill, 9);
        return enemy;
    }

    private static EnemyState CreateSkulkingColony(Random rng)
    {
        var enemy = CreateEnemy(KE.SkulkingColony, rng, new Intent(IntentType.Attack, 16));
        enemy.Hp = 80;
        enemy.MaxHp = 80;
        BuffSystem.Apply(enemy.Buffs, BuffId.HardenedShell, 20);
        return enemy;
    }
}
