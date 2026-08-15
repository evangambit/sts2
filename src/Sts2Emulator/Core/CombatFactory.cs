using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core.Effects;

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
    private const int StartingEnergy = 3;

    // Ironclad starting deck card IDs (from Generated/Cards.g.cs).
    private const int StrikeId = IC.StrikeIronclad; // 472
    private const int DefendId = IC.DefendIronclad; // 131
    private const int BashId = IC.Bash; // 30
    private const int AscendersBaneId = IC.AscendersBane; // 10001

    private enum ActOneEncounter
    {
        Cultists,
        Chompers,
        Nibbit,
        Slimes,
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
        Nibbits,
        LargeSlimes,
        SlimeAndFlyconid,
        JaxfruitAndFlyconid,
        CubexConstruct,
        VineShambler,
        ShrinkerAndFuzzy,
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
        ActOneEncounter.Nibbit,
        ActOneEncounter.Slimes,
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
        int completedCombatRoomsBeforeCurrent
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
        state.PlayerBuffs = [];
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
        state.ShuffleRng = shuffleRng;
        state.AiRng = aiRng;

        state.DrawPile = deck.ToArray().ToList();

        var encounter = encounterId.HasValue
            ? (ActOneEncounter)encounterId.Value
            : SelectFirstCombatEncounter(rng);
        state.EncounterId = (int)encounter;
        state.IsEliteCombat = IsEliteEncounter(encounter);
        // Use state.NicheHpRng (set by caller) as the dedicated HP RNG matching
        // RunState.Rng.Niche.  Null falls back to the main combat rng.
        _currentNicheHpRng = state.NicheHpRng;
        _usedNicheHps = _currentNicheHpRng != null ? new HashSet<int>() : null;
        state.Enemies = CreateEncounter(
            encounter,
            rng,
            encounterRngSeed,
            completedCombatRoomsBeforeCurrent
        );
        _currentNicheHpRng = null;
        _usedNicheHps = null;
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng, state.AiRng);
        EnemyAI.UpdateSecondaryIntents(state.Enemies);

        // Shuffle draw pile (skip if caller pre-shuffled it) and deal opening hand of 5.
        if (!deckPreShuffled)
        {
            CardEffects.ShufflePile(state.DrawPile, rng);
        }

        RelicEffects.ApplyBeforeOpeningHand(state, rng);

        for (int i = 0; i < 5 && state.DrawPile.Count > 0; i++)
        {
            state.Hand.Add(state.DrawPile[0]);
            state.DrawPile.RemoveAt(0);
        }

        RelicEffects.ApplyCombatStart(state, rng);
        RelicEffects.ApplyStartOfPlayerTurn(state);
        RelicEffects.ApplyAfterPlayerHpChanged(state);
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
        int completedCombatRoomsBeforeCurrent = -1
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

            ActOneEncounter.Nibbit =>
            [
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Attack, 13)),
            ],

            ActOneEncounter.Slimes => CreateSlimeEncounter(rng, encounterRngSeed),

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

            ActOneEncounter.TwoTailedRats => CreateTwoTailedRatsEncounter(rng),

            ActOneEncounter.GremlinMerc => [CreateGremlinMerc(rng)],

            ActOneEncounter.FuzzyWurmCrawler =>
            [
                CreateEnemy(KE.FuzzyWurmCrawler, rng, new Intent(IntentType.Attack, 6)),
            ],

            ActOneEncounter.CorpseSlugs => CreateCorpseSlugsEncounter(
                rng,
                completedCombatRoomsBeforeCurrent is >= 0 and < 3
            ),

            ActOneEncounter.SludgeSpinner =>
            [
                CreateEnemy(KE.SludgeSpinner, rng, new Intent(IntentType.Debuff, 9)),
            ],

            ActOneEncounter.ShrinkerBeetle =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
            ],

            ActOneEncounter.Seapunk =>
            [
                CreateEnemy(KE.Seapunk, rng, new Intent(IntentType.Attack, 13)),
            ],

            ActOneEncounter.Toadpoles =>
            [
                CreateEnemy(KE.Toadpole, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.Toadpole, rng, new Intent(IntentType.Attack, 8), moveIndex: 2),
            ],

            ActOneEncounter.Mawler =>
            [
                CreateEnemy(KE.Mawler, rng, new Intent(IntentType.Attack, 10)),
            ],

            ActOneEncounter.Nibbits =>
            [
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Attack, 7), moveIndex: 1),
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Buff, 0), moveIndex: 2),
            ],

            ActOneEncounter.LargeSlimes => CreateLargeSlimesEncounter(rng),

            ActOneEncounter.SlimeAndFlyconid =>
            [
                CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM, rng),
                CreateEnemy(KE.Flyconid, rng, FlyconidInitialIntent(rng), moveIndex: rng.Next(2)),
            ],

            ActOneEncounter.JaxfruitAndFlyconid =>
            [
                CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Attack, 4)),
                CreateEnemy(KE.Flyconid, rng, FlyconidInitialIntent(rng), moveIndex: rng.Next(2)),
            ],

            ActOneEncounter.CubexConstruct => [CreateCubexConstruct(rng)],

            ActOneEncounter.VineShambler =>
            [
                CreateEnemy(KE.VineShambler, rng, new Intent(IntentType.Attack, 14)),
            ],

            ActOneEncounter.ShrinkerAndFuzzy =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
                CreateEnemy(KE.FuzzyWurmCrawler, rng, new Intent(IntentType.Attack, 6)),
            ],

            ActOneEncounter.CultistAndSeapunk =>
            [
                CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.Seapunk, rng, new Intent(IntentType.Attack, 13)),
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

            ActOneEncounter.SlitheringStrangler => CreateSlitheringStranglerEncounter(rng),

            ActOneEncounter.RubyRaiders => CreateRubyRaiders(rng),

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

    private static bool IsEliteEncounter(ActOneEncounter encounter) =>
        encounter is >= ActOneEncounter.BygoneEffigy and <= ActOneEncounter.WaterfallGiant;

    private static List<EnemyState> CreateSlimeEncounter(Random rng, int? encounterRngSeed = null)
    {
        // Type selection uses the encounter-specific RNG (seeded from run_seed + floor + hash("slimes_weak")),
        // matching SlimesWeak.GenerateMonsters() which uses base.Rng separate from the niche RNG.
        var typeRng = encounterRngSeed.HasValue ? new Random(encounterRngSeed.Value) : rng;
        int firstSmall = typeRng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS;
        int middle = typeRng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM;
        int secondSmall = firstSmall == KE.LeafSlimeS ? KE.TwigSlimeS : KE.LeafSlimeS;

        // LeafSlimeS starting intent depends on slot: firstSmall=Attack(0), secondSmall=Debuff(1).
        // TwigSlimeS always starts with Attack(5). These are slot-deterministic, not niche-RNG-based.
        var firstIntent =
            firstSmall == KE.LeafSlimeS
                ? new Intent(IntentType.Attack, 4)
                : new Intent(IntentType.Attack, 5);
        var secondIntent =
            secondSmall == KE.LeafSlimeS
                ? new Intent(IntentType.Debuff, 1)
                : new Intent(IntentType.Attack, 5);
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

    private static List<EnemyState> CreateLargeSlimesEncounter(Random rng)
    {
        bool leafSmallFirst = rng.Next(2) == 0;
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

    private static List<EnemyState> CreateTwoTailedRatsEncounter(Random rng)
    {
        int firstMove = rng.Next(3);
        return
        [
            CreateTwoTailedRat(rng, firstMove),
            CreateTwoTailedRat(rng, (firstMove + 1) % 3),
            CreateTwoTailedRat(rng, (firstMove + 2) % 3),
        ];
    }

    private static List<EnemyState> CreateSlitheringStranglerEncounter(Random rng)
    {
        var enemies = new List<EnemyState>();
        switch (rng.Next(3))
        {
            case 0:
                enemies.Add(
                    CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Attack, 4))
                );
                break;
            case 1:
                enemies.Add(CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM, rng));
                break;
            default:
                enemies.Add(CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS, rng));
                enemies.Add(CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS, rng));
                break;
        }
        enemies.Add(CreateEnemy(KE.SlitheringStrangler, rng, new Intent(IntentType.Debuff, 3)));
        return enemies;
    }

    private static List<EnemyState> CreateRubyRaiders(Random rng)
    {
        return
        [
            CreateEnemy(KE.TrackerRubyRaider, rng, new Intent(IntentType.Debuff, 2), fixedHp: 25),
            CreateEnemy(KE.AssassinRubyRaider, rng, new Intent(IntentType.Attack, 11), fixedHp: 24),
            CreateEnemy(KE.BruteRubyRaider, rng, new Intent(IntentType.Attack, 8), fixedHp: 31),
        ];
    }

    private static EnemyState CreateTwoTailedRat(Random rng, int moveIndex)
    {
        var enemy = CreateEnemy(KE.TwoTailedRat, rng, RatIntent(moveIndex), moveIndex);
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

    private static Intent FlyconidInitialIntent(Random rng) =>
        rng.Next(3) switch
        {
            0 or 1 => new Intent(IntentType.Attack, 9),
            _ => new Intent(IntentType.Attack, 12),
        };

    private static List<EnemyState> CreateCorpseSlugsEncounter(Random rng, bool weak = false)
    {
        if (weak)
        {
            return [CreateCorpseSlug(rng, 2, fixedHp: 27), CreateCorpseSlug(rng, 0, fixedHp: 29)];
        }

        return
        [
            CreateCorpseSlug(rng, 2, fixedHp: 27),
            CreateCorpseSlug(rng, 0, fixedHp: 28),
            CreateCorpseSlug(rng, 1, fixedHp: 29),
        ];
    }

    private static EnemyState CreateCorpseSlug(Random rng, int moveIndex, int? fixedHp = null) =>
        CreateCorpseSlugEnemy(rng, moveIndex, fixedHp);

    private static EnemyState CreateCorpseSlugEnemy(Random rng, int moveIndex, int? fixedHp = null)
    {
        var enemy = CreateEnemy(
            KE.CorpseSlug,
            rng,
            CorpseSlugIntent(moveIndex),
            moveIndex,
            fixedHp
        );
        BuffSystem.Apply(enemy.Buffs, BuffId.Ravenous, 5);
        return enemy;
    }

    private static Intent CorpseSlugIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 6),
            1 => new Intent(IntentType.Attack, 9),
            _ => new Intent(IntentType.Debuff, 2),
        };

    public static EnemyState CreateEnemy(
        int defId,
        Random rng,
        Intent startingIntent,
        int moveIndex = 0,
        int? fixedHp = null
    )
    {
        var def = GeneratedData.Enemies.Get(defId);
        // Use the dedicated niche HP RNG when available, matching SetUniqueMonsterHpValue
        // which calls rng.NextItem(remaining_set) to avoid duplicate HP values across
        // creatures on the same side.
        int hp;
        if (fixedHp.HasValue)
        {
            hp = fixedHp.Value;
            if (_currentNicheHpRng != null)
            {
                _currentNicheHpRng.Next(0, def.MaxHp - def.MinHp + 1);
            }
        }
        else if (_currentNicheHpRng != null)
        {
            // Build remaining set = [minHp..maxHp] minus already-used HP values.
            var range = Enumerable.Range(def.MinHp, def.MaxHp - def.MinHp + 1).ToHashSet();
            if (_usedNicheHps != null)
            {
                range.ExceptWith(_usedNicheHps);
            }

            if (range.Count == 0)
            {
                // Fallback: full range (matches game behaviour when all values are taken).
                hp = _currentNicheHpRng.Next(def.MinHp, def.MaxHp + 1);
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
            hp = rng.Next(def.MinHp, def.MaxHp + 1);
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
            moveIndex: 1,
            fixedHp: 55
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
