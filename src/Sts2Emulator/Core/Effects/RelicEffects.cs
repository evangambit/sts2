namespace Sts2Emulator.Core.Effects;

using Run;

public static class RelicEffects
{
    public const int Akabeko = 1;
    public const int Anchor = 4;
    public const int ArtOfWar = 7;
    public const int BagOfMarbles = 9;
    public const int BagOfPreparation = 10;
    public const int BlessedAntler = 21;
    public const int BigMushroom = 16;
    public const int BiiigHug = 17;
    public const int PaelsLegion = 181;

    // The unmodelled pass. Every one of these had an entry in `Relics.g.cs` and no id
    // constant, which means the game could hand it over and the emulator would do nothing
    // at all -- a relic that is in the run's list, shows in the observation, and is inert.
    public const int VeryHotCocoa = 283;
    public const int FencingManual = 86;
    public const int RunicCapacitor = 226;
    public const int SymbioticVirus = 257;
    public const int TwistedFunnel = 275;
    public const int OrangeDough = 171;
    public const int BigHat = 15;
    public const int PowerCell = 203;
    public const int Brimstone = 34;
    public const int NinjaScroll = 165;
    public const int FuneraryMask = 94;
    public const int ToughBandages = 269;
    public const int Tingsha = 264;
    public const int CharonsAshes = 45;
    public const int HelicalDart = 112;
    public const int PaperKrane = 187;
    public const int PaperPhrog = 188;
    public const int UndyingSigil = 277;
    public const int VitruvianMinion = 285;
    public const int SneckoSkull = 244;
    public const int RuinedHelmet = 225;
    public const int Regalite = 216;
    public const int LunarPastry = 143;
    public const int GoldPlatedCables = 106;
    public const int BoneFlute = 24;
    public const int BookRepairKnife = 28;
    public const int Bookmark = 26;
    public const int DemonTongue = 59;
    public const int EmotionChip = 73;
    public const int MiniRegent = 155;
    public const int GalacticDust = 96;
    public const int Metronome = 152;
    public const int LoomingFruit = 138;
    public const int FresnelLens = 92;

    // The fourteen an Act 1 run can be handed and that still did nothing, found when
    // `--reachable` turned out to mean "not in the event pool" rather than "reachable".
    public const int BoneTea = 25;
    public const int DarkstonePeriapt = 55;
    public const int DreamCatcher = 67;
    public const int EmberTea = 72;
    public const int HandDrill = 109;
    public const int HistoryCourse = 113;
    public const int LastingCandy = 130;
    public const int MawBank = 146;
    public const int RazorTooth = 213;
    public const int SparklingRouge = 246;
    public const int SwordOfJade = 255;
    public const int SwordOfStone = 256;
    public const int TeaOfDiscourtesy = 259;
    public const int TheBoot = 261;

    /// <summary>Bone Tea's `Combats` var: one fight, then it is used up.</summary>
    public const int BoneTeaCombats = 1;

    /// <summary>Ember Tea's: five fights of Strength 2.</summary>
    public const int EmberTeaCombats = 5;

    /// <summary>Tea of Discourtesy's: one fight, and its `DazedCount` of two.</summary>
    public const int TeaOfDiscourtesyCombats = 1;

    public const int TeaOfDiscourtesyDazed = 2;

    /// <summary>Sword of Stone's `DynamicVar("Elites", 5m)` -- five, not three.</summary>
    public const int SwordOfStoneElites = 5;
    public const int BloodVial = 23;
    public const int BoomingConch = 29;
    public const int BronzeScales = 35;
    public const int CaptainsWheel = 41;
    public const int CentennialPuzzle = 43;
    public const int CloakClasp = 51;
    public const int Ectoplasm = 70;
    public const int DataDisk = 56;
    public const int FestivePopper = 87;
    public const int Gorget = 107;
    public const int HappyFlower = 110;
    public const int HornCleat = 114;
    public const int IvoryTile = 119;
    public const int Kunai = 126;
    public const int Kusarigama = 127;
    public const int GremlinHorn = 108;
    public const int Lantern = 128;
    public const int LetterOpener = 136;
    public const int LizardTail = 137;
    public const int MealTicket = 147;
    public const int MummifiedHand = 158;
    public const int Nunchaku = 166;
    public const int OddlySmoothStone = 169;
    public const int Orichalcum = 172;
    public const int OrnamentalFan = 173;
    public const int ParryingShield = 189;
    public const int Pendulum = 191;
    public const int Pocketwatch = 199;
    public const int PhilosophersStone = 196;
    public const int Permafrost = 193;
    public const int RedMask = 214;
    public const int RedSkull = 215;
    public const int RegalPillow = 217;
    public const int ScreamingFlagon = 230;
    public const int SelfFormingClay = 234;
    public const int Shuriken = 237;
    public const int Sozu = 245;
    public const int SpikedGauntlets = 247;
    public const int StoneCracker = 249;
    public const int TinyMailbox = 265;
    public const int StoneCalendar = 248;
    public const int VenerableTeaSetActive = 100282;
    public const int Vajra = 279;
    public const int TuningFork = 274;
    public const int VelvetChoker = 281;

    // The shared pool's commons and uncommons, added with the batch that modelled them.
    public const int BookOfFiveRings = 27;
    public const int BowlerHat = 31;
    public const int Candelabra = 40;
    public const int EternalFeather = 75;
    public const int JossPaper = 122;
    public const int LuckyFysh = 142;
    public const int MercuryHourglass = 151;
    public const int MiniatureCannon = 153;
    public const int PenNib = 192;
    public const int PetrifiedToad = 194;
    public const int PotionBelt = 202;
    public const int ReptileTrinket = 218;
    public const int RippleBasin = 222;
    public const int StrikeDummy = 253;
    public const int Vambrace = 280;
    public const int VenerableTeaSet = 282;
    public const int AmethystAubergine = 3;
    // The shared pool's rares.
    public const int BeatingRemnant = 11;
    public const int Girya = 100;
    public const int GamblingChip = 97;
    // The four Starter relics, one per character. Every run of that character holds one.
    public const int BoundPhylactery = 30;
    public const int CrackedCore = 52;
    public const int DivineRight = 64;
    public const int RingOfTheSnake = 221;
    // The shop pool.
    public const int BeltBuckle = 14;
    public const int Cauldron = 42;
    public const int DingyRug = 61;
    public const int DragonFruit = 66;
    public const int DollysMirror = 65;
    public const int LavaLamp = 131;
    public const int Orrery = 174;
    public const int WingCharm = 292;
    public const int GnarledHammer = 103;
    public const int Kifuda = 125;
    public const int PunchDagger = 210;
    public const int RoyalStamp = 224;
    public const int Bread = 32;
    public const int BurningSticks = 37;
    public const int ChemicalX = 46;
    public const int GhostSeed = 99;
    public const int MiniatureTent = 154;
    public const int MysticLighter = 160;
    public const int RingingTriangle = 219;
    public const int SlingOfCourage = 241;
    public const int TheAbacus = 260;
    public const int Toolbox = 267;
    public const int UnsettlingLamp = 278;
    public const int Shovel = 236;
    public const int Bellows = 13;
    public const int Chandelier = 44;
    public const int GamePiece = 98;
    public const int IceCream = 115;
    public const int IntimidatingHelmet = 117;
    public const int PrayerWheel = 204;
    public const int RainbowRing = 212;
    public const int SturdyClamp = 254;
    public const int TheCourier = 262;
    public const int TungstenRod = 273;
    public const int UnceasingTop = 276;
    public const int VexingPuzzlebox = 284;
    public const int WhiteStar = 291;
    public const int JuzuBracelet = 123;
    public const int Pantograph = 186;
    public const int Planisphere = 198;

    /// <summary>The token potion Petrified Toad procures before every combat.</summary>
    private const int PotionShapedRock = 45;

    /// <summary>
    /// The relics whose ModifyMaxEnergy adds an EnergyVar(1). Every one of them is the same
    /// +1; what separates them is the price, which each pays through a different hook.
    /// </summary>
    private static readonly int[] MaxEnergyRelics =
    [
        Ectoplasm,
        Sozu,
        SpikedGauntlets,
        VelvetChoker,
        PhilosophersStone,
        BlessedAntler,
    ];

    /// <summary>The game's DynamicVar("Cards", 6) on Velvet Choker.</summary>
    private const int VelvetChokerCardLimit = 6;

    /// <summary>
    /// Velvet Choker's ShouldPlay: once six cards have been played this turn, nothing else
    /// can be. The game applies this to auto-played cards too (Havoc, Mayhem), which the
    /// engine does not — see the approximation table in HANDOFF.md.
    /// </summary>
    public static bool BlocksFurtherCardPlays(CombatState state) =>
        HasRelic(state, VelvetChoker) && state.CardPlaysThisTurn >= VelvetChokerCardLimit;

    /// <summary>
    /// Spiked Gauntlets' TryModifyEnergyCostInCombat: Powers cost one more. Returned as an
    /// addend so the caller keeps owning the rest of the cost rules.
    /// </summary>
    public static int ExtraEnergyCost(CombatState state, CardDef def) =>
        def.Type == CardType.Power && HasRelic(state, SpikedGauntlets) ? 1 : 0;

    /// <summary>Ectoplasm's ModifyGoldGained returns 0m: the owner gains no gold, ever.</summary>
    public static int ModifyGoldGained(IEnumerable<RelicInstance> relics, int amount)
    {
        var held = relics as IReadOnlyCollection<RelicInstance> ?? relics.ToList();
        if (held.Any(relic => relic.DefId == Ectoplasm))
        {
            return 0;
        }

        // `BowlerHat.ModifyGoldGained` multiplies by a `DynamicVar("GoldIncrease", 1.25m)`.
        // A decimal multiply, so 15 gold becomes 18 and not 19 -- the truncation is the
        // game's, not a rounding choice made here.
        if (held.Any(relic => relic.DefId == BowlerHat))
        {
            return (int)(amount * 1.25m);
        }

        return amount;
    }

    /// <summary>
    /// `DragonFruit.AfterGoldGained`: 1 max HP, through `CreatureCmd.GainMaxHp` -- so it
    /// HEALS 1 as well as raising the cap.
    ///
    /// Once per gain EVENT, not per gold: a hundred from a boss is the same +1 as fifteen
    /// from a mushroom. And `PlayerCmd.GainGold` returns on `!(amount > 0m)` BEFORE
    /// reaching the hook, which is why both chokepoints drop out on a zeroed amount --
    /// Ectoplasm suppresses this relic entirely rather than merely taking the gold.
    /// </summary>
    internal static void ApplyAfterGoldGained(Run.RunState state)
    {
        if (Has(state.Relics, DragonFruit))
        {
            Run.RunNonCombatEffects.GainMaxHp(state, 1);
        }
    }

    /// <inheritdoc cref="ApplyAfterGoldGained(Run.RunState)" />
    internal static void ApplyAfterGoldGained(CombatState state)
    {
        if (HasRelic(state, DragonFruit))
        {
            CardEffects.GainMaxHp(state, 1);
        }
    }

    /// <summary>
    /// `BoundPhylactery.AfterEnergyResetLate`: Osty is re-summoned at the start of every
    /// turn EXCEPT the first, whose summon `BeforeCombatStart` already did.
    ///
    /// The game picks `AfterEnergyResetLate` deliberately and says why: anything that
    /// asks whether Osty exists -- Friendship is the one it names -- has to run BEFORE
    /// the summon, or the relic answers its own question. On a living Osty the summon is
    /// `GainMaxHp`, so turn after turn it is +1 rather than a fresh pet.
    /// </summary>
    internal static void ApplyBoundPhylacteryTurnStart(CombatState state, int turnNumber)
    {
        if (turnNumber != 1 && HasRelic(state, BoundPhylactery))
        {
            CardEffects.SummonOsty(state, 1);
        }
    }

    /// <summary>
    /// Relics whose combat counter means "spent for the rest of the run" rather than
    /// something a fresh combat resets. The run carries these in RunState.UsedUpRelics.
    /// </summary>
    private static readonly int[] OncePerRunRelics = [LizardTail];

    /// <summary>Marks relics the run has already spent, so a new combat cannot reuse them.</summary>
    public static void RestoreUsedUpRelics(CombatState state, IEnumerable<int> usedUp)
    {
        foreach (int relicId in usedUp)
        {
            SetCounter(state, relicId, 1);
        }
    }

    /// <summary>Reports which one-per-run relics this combat spent.</summary>
    public static void CollectUsedUpRelics(CombatState state, List<int> usedUp)
    {
        foreach (int relicId in OncePerRunRelics)
        {
            int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
            if (index >= 0 && state.Relics[index].Counter > 0 && !usedUp.Contains(relicId))
            {
                usedUp.Add(relicId);
            }
        }
    }

    public static void ApplyBeforeOpeningHand(CombatState state, Random rng)
    {
        if (HasRelic(state, BlessedAntler))
        {
            // BeforeHandDraw on turn one: three Dazed into the draw pile at
            // CardPilePosition.Random, which CardPileCmd resolves off Rng.Shuffle.
            CardEffects.AddCardToDrawPileRandomly(state, ST.Dazed, 3, state.ShuffleRng ?? rng);
        }

        // `NinjaScroll.BeforeHandDraw` at TurnNumber <= 1: `DynamicVar("Shivs", 3m)` into
        // HAND, before the opening draw -- so the hand is three Shivs plus the usual five.
        if (HasRelic(state, NinjaScroll))
        {
            CardEffects.AddGeneratedCardsToHand(state, 430, 3);
        }

        // `FuneraryMask.BeforeHandDraw` at TurnNumber == 1: CardsVar(3) Souls into the
        // DRAW pile at CardPilePosition.Random, one insert point per card off Rng.Shuffle
        // -- Blessed Antler's shape with a card the player wants.
        //
        // Note the guard is `== 1` where Ninja Scroll's is `<= 1`. Both mean turn one, and
        // both are transcribed as they are written rather than normalised.
        if (HasRelic(state, FuneraryMask))
        {
            CardEffects.AddCardToDrawPileRandomly(state, 446, 3, state.ShuffleRng ?? rng);
        }

        if (!HasRelic(state, StoneCracker))
        {
            return;
        }

        // The relic takes Cards.Where(IsUpgradable).Take(2) off the draw pile — the first
        // two in pile order, not two at random, which is what this used to roll for.
        var upgradableIndices = state
            .DrawPile.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .Take(2)
            .ToList();

        foreach (int drawPileIndex in upgradableIndices)
        {
            state.DrawPile[drawPileIndex] = state.DrawPile[drawPileIndex] with { Upgraded = true };
        }
    }

    public static void ApplyCombatStart(CombatState state, Random rng)
    {
        // ModifyMaxEnergy is read every time the game asks for max energy, so the bonus has
        // to reach MaxEnergy (which each turn refills from) and the energy already handed
        // out for turn one.
        int extraEnergy = state.Relics.Count(relic => MaxEnergyRelics.Contains(relic.DefId));
        state.MaxEnergy += extraEnergy;
        // NOT through GainEnergy: this is part of turn one's RESET catching up with the
        // new maximum, not a `PlayerCmd.GainEnergy`, and the reset is a different path
        // that `NoEnergyGainPower` does not touch.
        state.Energy += extraEnergy;

        // `Girya.AfterRoomEntered(CombatRoom)` -- Strength equal to the lifts spent on it.
        // Read off the relic INSTANCE's counter, which is why the run has to hand the
        // counter to the combat rather than just the relic's id.
        // `SlingOfCourage.AfterRoomEntered(RoomType.Elite)`: 2 Strength on the fight that
        // room starts. The relic's hook fires before the combat exists, so the room TYPE
        // has to be handed over rather than looked up.
        if (state.IsEliteRoom && HasRelic(state, SlingOfCourage))
        {
            GainPlayerStrength(state, 2);
        }

        // The three Starter relics that open a combat with something. Every run of that
        // character holds one, so these are the most-exercised relics in the game and
        // were the last four with nothing behind them.
        //
        // `CrackedCore.BeforeSideTurnStart` guards on `TurnNumber <= 1`, so its Lightning
        // is a combat-start channel and not a per-turn one.
        if (HasRelic(state, CrackedCore))
        {
            CardEffects.ChannelOrb(state, OrbType.Lightning, rng);
        }

        // `DivineRight.AfterRoomEntered(CombatRoom)`: three Stars, and stars live on the
        // PlayerCombatState -- so this is three at the top of every fight, not three that
        // accumulate across the run.
        if (HasRelic(state, DivineRight))
        {
            CardEffects.GainStars(state, 3);
        }

        // `BoundPhylactery.BeforeCombatStart` summons Osty at `SummonVar(1)` -- one HP,
        // which is a body to soak one hit rather than a wall.
        if (HasRelic(state, BoundPhylactery))
        {
            CardEffects.SummonOsty(state, 1);
        }

        RefreshBeltBuckle(state);

        int girya = state.Relics.FirstOrDefault(relic => relic.DefId == Girya).Counter;
        if (girya > 0)
        {
            GainPlayerStrength(state, girya);
        }

        if (HasRelic(state, PhilosophersStone))
        {
            // AfterRoomEntered applies StrengthPower(1m) to every living opponent. The game
            // also catches enemies that join mid-combat (AfterCreatureAddedToCombat); a
            // summoned enemy here does not get it.
            foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
            }
        }

        // `PetrifiedToad.BeforeCombatStartLate` procures a Shaped Rock into the first free
        // potion slot. `TryToProcure` fails silently when the belt is full, which is why
        // this looks for a slot rather than making room.
        if (HasRelic(state, PetrifiedToad))
        {
            for (int i = 0; i < state.PotionSlots.Length; i++)
            {
                if (state.PotionSlots[i] == 0)
                {
                    state.PotionSlots[i] = PotionShapedRock;
                    break;
                }
            }
        }

        // Every relic below guards on `TurnNumber <= 1` inside `AfterSideTurnStart` or
        // `BeforeSideTurnStart`, which is the combat-start shape Cracked Core already
        // uses -- a once-per-fight opener wearing a per-turn hook.

        // `VeryHotCocoa`: EnergyVar(4). An Ancient relic, so a whole extra turn's energy
        // on turn one.
        if (HasRelic(state, VeryHotCocoa))
        {
            CardEffects.GainEnergy(state, 4);
        }

        // `FencingManual`: ForgeVar(10) -- a Sovereign Blade at 10, the Regent's own
        // mechanic handed out by a Common relic.
        if (HasRelic(state, FencingManual))
        {
            CardEffects.Forge(state, 10);
        }

        // `RunicCapacitor`: RepeatVar(3), `OrbCmd.AddSlots`. Three orb slots for the whole
        // fight, which for a Defect is more than doubling the ring.
        if (HasRelic(state, RunicCapacitor))
        {
            state.OrbCapacity += 3;
        }

        // `SymbioticVirus`: `DynamicVar("Dark", 1m)` channelled as a DarkOrb.
        if (HasRelic(state, SymbioticVirus))
        {
            CardEffects.ChannelOrb(state, OrbType.Dark, rng);
        }

        // `TwistedFunnel`: Poison 4 on every hittable enemy.
        if (HasRelic(state, TwistedFunnel))
        {
            CardEffects.ApplyPoisonToAllEnemies(state, 4, rng);
        }

        // `OrangeDough`: CardsVar(2) DISTINCT colourless cards into hand, off
        // `CombatCardGeneration` -- the same shuffle-then-take that every other
        // `GetDistinctForCombat` caller uses.
        if (HasRelic(state, OrangeDough))
        {
            CardEffects.AddColorlessCardsToHand(state, 2, rng);
        }

        // `BigHat`: CardsVar(2) distinct ETHEREAL cards from the player's OWN pool. Its
        // filter is `c.Keywords.Contains(Ethereal)` over the character pool rather than
        // the colourless one, and the whole block is skipped when the filter is empty --
        // which for a character with no Ethereal cards is the difference between two
        // cards and none.
        if (HasRelic(state, BigHat))
        {
            CardEffects.AddDistinctEtherealCardsToHand(state, 2, rng);
        }

        // `PowerCell`: CardsVar(2) ZERO-COST cards out of the draw pile and into hand.
        // `StableShuffle(CombatCardSelection)` then Take -- a different stream from the
        // generation ones above, because these cards already exist.
        if (HasRelic(state, PowerCell))
        {
            CardEffects.MoveZeroCostDrawCardsToHandForPowerCell(state, 2, rng);
        }

        // ── The three teas, and Sword of Jade ──────────────────────────────────
        // Each tea counts DOWN the combats it has left, and the count is run state: the
        // combat is handed the relic already charged (see CombatFactory.Reset), spends
        // one here, and the run reads the remainder back at the end of the fight.

        // `BoneTea.AfterSideTurnStart` at TurnNumber <= 1: UPGRADE EVERY CARD IN HAND.
        // One combat only.
        if (SpendTeaCombat(state, BoneTea))
        {
            for (int i = 0; i < state.Hand.Count; i++)
            {
                state.Hand[i] = state.Hand[i] with { Upgraded = true };
            }
        }

        // `EmberTea.AfterRoomEntered(CombatRoom)`: Strength 2, for five combats.
        if (SpendTeaCombat(state, EmberTea))
        {
            GainPlayerStrength(state, 2);
        }

        // `TeaOfDiscourtesy.BeforeCombatStart`: two Dazed into the DRAW pile at random
        // positions. One combat, and it is the price the Tea Master's free tea charges.
        if (SpendTeaCombat(state, TeaOfDiscourtesy))
        {
            CardEffects.AddCardToDrawPileRandomly(
                state,
                ST.Dazed,
                TeaOfDiscourtesyDazed,
                state.ShuffleRng ?? rng
            );
        }

        // `SwordOfJade.AfterRoomEntered(CombatRoom)`: Strength 3, every fight, forever.
        // What Sword of Stone becomes after five elites.
        if (HasRelic(state, SwordOfJade))
        {
            GainPlayerStrength(state, 3);
        }

        if (HasRelic(state, BloodVial))
        {
            CardEffects.HealPlayer(state, 2);
        }

        if (HasRelic(state, Anchor))
        {
            // BlockVar(10m, ValueProp.Unpowered) — Dexterity does not raise it.
            CardEffects.GainUnpoweredBlock(state, 10, rng);
        }

        if (HasRelic(state, Vajra))
        {
            GainPlayerStrength(state, 1);
        }

        if (HasRelic(state, OddlySmoothStone))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
        }

        if (HasRelic(state, DataDisk))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, 1);
        }

        if (HasRelic(state, Gorget))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, 4);
        }

        if (HasRelic(state, BronzeScales))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 3);
        }

        // Only the energy is the conch's combat-start effect. Its two CARDS are a
        // `ModifyHandDraw`, and they ride `ExtraOpeningHandDraw` with Ring of the Snake's
        // and Bag of Preparation's -- see the remark there.
        if (HasRelic(state, BoomingConch) && state.IsEliteCombat)
        {
            CardEffects.GainEnergy(state, 1);
        }
    }

    /// <summary>
    /// Hook.AfterCreatureAddedToCombat: an enemy that joins a combat in progress gets the
    /// same treatment as one that started it. Returns the enemy so a spawn site can wrap
    /// its own construction call rather than remembering to follow it with this.
    /// </summary>
    public static EnemyState Spawned(CombatState state, EnemyState enemy)
    {
        if (HasRelic(state, PhilosophersStone))
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
        }

        return enemy;
    }

    public static void ApplyStartOfPlayerTurn(CombatState state, Random? rng = null)
    {
        int turnNumber = state.Turn + 1;

        // Every "N cards played this turn" relic counts from zero again; the game resets
        // them in BeforeSideTurnStart, which is this same boundary.
        foreach (int relicId in PerTurnCounters)
        {
            SetCounter(state, relicId, 0);
        }

        if (turnNumber == 1)
        {
            if (HasRelic(state, Lantern))
            {
                CardEffects.GainEnergy(state, 1);
            }

            if (HasRelic(state, VenerableTeaSetActive))
            {
                CardEffects.GainEnergy(state, 2);
            }

            if (HasRelic(state, Akabeko))
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vigor, 8);
            }

            if (HasRelic(state, BagOfMarbles))
            {
                foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Vulnerable, 1);
                }
            }

            if (HasRelic(state, RedMask))
            {
                foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Weak, 1);
                }
            }

            if (HasRelic(state, FestivePopper))
            {
                // DamageVar(9m, ValueProp.Unpowered) — Strength does not raise it.
                CardEffects.DealUnpoweredDamageToAll(state, 9);
            }
        }

        // `Brimstone.AfterSideTurnStart` with NO turn guard: 2 Strength to the player and
        // 1 to every LIVING opponent, every single turn. The enemy half is the point --
        // it is a Shop relic that arms the room as fast as it arms you.
        if (HasRelic(state, Brimstone))
        {
            GainPlayerStrength(state, 2);
            foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
            {
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
            }
        }

        // `EmotionChip.AfterPlayerTurnStart`: if the player took unblocked damage during
        // the PREVIOUS player turn, every orb in the queue fires its passive. The history
        // query is `HappenedLastPlayerTurn`, so it is the last turn's damage rather than
        // any damage -- the flag is set when damage lands and read here.
        if (HasRelic(state, EmotionChip) && GetCounter(state, EmotionChip) > 0)
        {
            CardEffects.TriggerEveryOrbPassive(state, DrawRng(state, rng));
        }

        SetCounter(state, EmotionChip, 0);

        // `MiniRegent` and `DemonTongue` both clear a once-per-turn flag in
        // `BeforeSideTurnStart`; the counter IS that flag.
        SetCounter(state, MiniRegent, 0);
        SetCounter(state, DemonTongue, 0);

        // Both are BlockVar(..., ValueProp.Unpowered): Dexterity does not raise them.
        if (turnNumber == 2 && HasRelic(state, HornCleat))
        {
            CardEffects.GainUnpoweredBlock(state, 14, rng);
        }

        if (turnNumber == 3 && HasRelic(state, CaptainsWheel))
        {
            CardEffects.GainUnpoweredBlock(state, 18, rng);
        }

        if (CountTowards(state, HappyFlower, period: 3))
        {
            CardEffects.GainEnergy(state, 1);
        }

        if (CountTowards(state, Pendulum, period: 3))
        {
            CardEffects.DrawCards(state, 1, DrawRng(state, rng));
        }

        // Both of these ask what the player did LAST turn, and the engine clears its
        // per-turn tallies after this hook — so this is the one moment they still hold it.
        if (turnNumber > 1 && HasRelic(state, ArtOfWar) && state.AttackCardsPlayedThisTurn == 0)
        {
            // AfterEnergyReset: the energy arrives on a turn following one with no Attack.
            CardEffects.GainEnergy(state, 1);
        }

        // Pocketwatch's ModifyHandDraw runs after the tallies are cleared, so the verdict
        // is taken here and parked on the relic for the draw to read.
        SetCounter(
            state,
            Pocketwatch,
            turnNumber > 1 && state.CardPlaysThisTurn <= PocketwatchCardThreshold ? 1 : 0
        );
    }

    /// <summary>Pocketwatch's DynamicVar("CardThreshold", 3m) and CardsVar(3).</summary>
    private const int PocketwatchCardThreshold = 3;

    /// <summary>
    /// Extra cards the opening draw of a turn owes to relics — Pocketwatch's CardsVar(3)
    /// after a turn of three cards or fewer.
    /// </summary>
    public static int ExtraHandDraw(CombatState state) =>
        state.Relics.Any(relic => relic.DefId == Pocketwatch && relic.Counter > 0) ? 3 : 0;

    /// <summary>
    /// `RingOfTheSnake.ModifyHandDraw` and `BagOfPreparation.ModifyHandDraw`: `CardsVar(2)`
    /// each while `TurnNumber > 1` is false, so they pay on turn ONE and never again --
    /// seven cards in the opening hand rather than five.
    ///
    /// They ride the opening hand rather than `ExtraHandDraw` because that is where turn
    /// one's draw happens: the turn-start path only ever runs from turn two, where both
    /// relics are already spent.
    /// </summary>
    /// <remarks>
    /// Bag of Preparation used to draw its two through a separate `DrawCards` at COMBAT
    /// START, which runs after the opening hand is already dealt. Same seven cards, and
    /// two things wrong with them: the pair were not part of the hand draw, so the hooks
    /// that fire only on EXTRA draws saw them (Speedster, Death March -- see E329), and the
    /// opening-hand size feeds `ApplyTurnOneDrawPileReorder`, which is what decides how
    /// many Innate cards the hand is guaranteed to hold.
    ///
    /// THREE relics carry the same mechanic and the emulator had them three different
    /// ways: Ring of the Snake through this path and correct, Bag of Preparation and
    /// Booming Conch through a separate `DrawCards` at combat start. One shape modelled
    /// more than once is a shape modelled wrongly somewhere, which is the more useful half
    /// of the finding.
    ///
    /// Booming Conch also asks the ROOM: its `ModifyHandDraw` pays only in an Elite, and
    /// only on turn one. Its energy is a separate `AfterSideTurnStart` and stays where it
    /// is, in `ApplyCombatStart`.
    /// </remarks>
    public static int ExtraOpeningHandDraw(CombatState state) =>
        (HasRelic(state, RingOfTheSnake) ? 2 : 0)
        + (HasRelic(state, BagOfPreparation) ? 2 : 0)
        + (HasRelic(state, BoomingConch) && state.IsEliteCombat ? 2 : 0)
        // Big Mushroom's `ModifyHandDraw` SUBTRACTS its `CardsVar(2)` on turn one: the
        // opening hand is three, and that is the price of its twenty max HP. Its pickup
        // half was modelled and this one was not -- read the `AfterObtained` and stop, and
        // a relic with a drawback becomes a relic without one.
        - (HasRelic(state, BigMushroom) ? 2 : 0);

    /// <summary>
    /// The relics that pay out every Nth card of a type played in one turn. The game holds
    /// this on the relic (Kunai's AttacksPlayedThisTurn and friends), so the counter lives
    /// on the relic instance here too rather than on a shared per-turn tally.
    /// </summary>
    private static readonly int[] PerTurnCounters =
    [
        Kunai,
        Kusarigama,
        Shuriken,
        OrnamentalFan,
        LetterOpener,
    ];

    public static void ApplyAfterCardPlayed(
        CombatState state,
        CardDef def,
        Random? rng = null,
        int energySpent = 0
    )
    {
        // Ivory Tile reads CardPlay.Resources.EnergyValue — what was actually spent, so an
        // X-cost card or a cost reduction changes the answer — and ignores card type.
        if (HasRelic(state, IvoryTile) && energySpent >= 3)
        {
            CardEffects.GainEnergy(state, 1);
        }

        // `HelicalDart.AfterCardPlayed`: a card tagged Shiv applies
        // `HelicalDartPower(Dexterity 1)`. The TAG, not the Shiv id -- Knife Trap carries
        // it too, so it pays on the trap as well as on every Shiv the trap replays.
        if (HasRelic(state, HelicalDart) && def.ShivTag)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
        }

        switch (def.Type)
        {
            case CardType.Attack:
                if (CountTowards(state, Shuriken, period: 3))
                {
                    GainPlayerStrength(state, 1);
                }

                if (CountTowards(state, Kunai, period: 3))
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
                }

                if (CountTowards(state, OrnamentalFan, period: 3))
                {
                    // BlockVar(4m, ValueProp.Unpowered).
                    CardEffects.GainUnpoweredBlock(state, 4, rng);
                }

                if (CountTowards(state, Kusarigama, period: 3))
                {
                    // DamageVar(6m, Unpowered) at Rng.CombatTargets.NextItem(HittableEnemies).
                    var target = CardEffects.RandomLivingEnemy(state, rng);
                    if (target != null)
                    {
                        CardEffects.DealUnpoweredDamageToEnemy(state, target, 6);
                    }
                }

                // Nunchaku counts attacks for the whole combat, not the turn, so its
                // counter is deliberately absent from PerTurnCounters.
                if (CountTowards(state, Nunchaku, period: 10))
                {
                    CardEffects.GainEnergy(state, 1);
                }

                break;

            case CardType.Skill:
                if (CountTowards(state, LetterOpener, period: 3))
                {
                    // DamageVar(5m, ValueProp.Unpowered), to every hittable enemy.
                    CardEffects.DealUnpoweredDamageToAll(state, 5);
                }

                // Tuning Fork keeps its tally across turns (SkillsPlayed is a
                // SavedProperty), unlike Letter Opener two lines up.
                if (CountTowards(state, TuningFork, period: 10))
                {
                    CardEffects.GainUnpoweredBlock(state, 7, rng);
                }

                break;

            case CardType.Power:
                if (FiresOncePerCombat(state, Permafrost))
                {
                    // BlockVar(7m, ValueProp.Unpowered), on the first Power only.
                    CardEffects.GainUnpoweredBlock(state, 7, rng);
                }

                MakeRandomHandCardFree(state, rng);
                break;
        }
    }

    /// <summary>
    /// Fires when the player takes unblocked damage. The game hangs both of these off
    /// Hook.AfterDamageReceived, which runs per damage instance; here the caller is the
    /// enemy attack path, so self-damage from a card does not trigger them.
    /// </summary>
    /// <param name="unblocked">How much of the hit actually landed, for Demon Tongue.</param>
    public static void ApplyAfterUnblockedDamageReceived(
        CombatState state,
        Random? rng = null,
        int unblocked = 0
    )
    {
        if (HasRelic(state, SelfFormingClay))
        {
            // SelfFormingClayPower: 3 unpowered block after the next block clear.
            BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, 3);
        }

        if (FiresOncePerCombat(state, CentennialPuzzle))
        {
            CardEffects.DrawCards(state, 3, DrawRng(state, rng));
        }

        // `EmotionChip.AfterDamageReceived` sets `Status = Active` on ANY unblocked hit,
        // whichever side dealt it -- the flag is read at the next player turn start and
        // cleared there. History stamps an entry with the player's TurnNumber, which does
        // not move during the enemy phase, so an enemy attack counts as "last turn" too.
        if (HasRelic(state, EmotionChip))
        {
            SetCounter(state, EmotionChip, 1);
        }

        // `DemonTongue.AfterDamageReceived`: the FIRST unblocked hit the player takes on
        // their own turn is healed straight back, once per turn.
        // `CombatState.CurrentSide == Owner.Creature.Side` -- the player's OWN side turn,
        // which `state.PlayerTurn` is. So it heals self-inflicted damage (Blood Wall,
        // Offering, Hemokinesis, a card that hits its owner) and never an enemy attack.
        if (
            state.PlayerTurn
            && unblocked > 0
            && HasRelic(state, DemonTongue)
            && GetCounter(state, DemonTongue) == 0
        )
        {
            SetCounter(state, DemonTongue, 1);
            CardEffects.HealPlayer(state, unblocked);
        }
    }

    /// <summary>
    /// Mummified Hand: after a Power, a random card in hand costs nothing this turn. The
    /// game's preference order is cards with a printed cost that still cost something,
    /// then anything that still costs something, then anything printed-costed, then
    /// anything at all — so a hand of free cards still picks a card.
    /// </summary>
    private static void MakeRandomHandCardFree(CombatState state, Random? rng)
    {
        if (!HasRelic(state, MummifiedHand) || state.Hand.Count == 0)
        {
            return;
        }

        var all = Enumerable.Range(0, state.Hand.Count).ToList();
        var printedCosted = all.Where(i => CostsAnything(state.Hand[i])).ToList();
        var stillCosts = all.Where(i =>
                CombatEngine.EffectiveCost(state.Hand[i], state) > 0 || HasStarCost(state.Hand[i])
            )
            .ToList();

        var candidates = printedCosted.Intersect(stillCosts).ToList();
        if (candidates.Count == 0)
        {
            candidates = stillCosts;
        }

        if (candidates.Count == 0)
        {
            candidates = printedCosted;
        }

        if (candidates.Count == 0)
        {
            candidates = all;
        }

        var selectionRng = state.CardSelectionRng ?? rng;
        int handIndex = candidates[selectionRng?.Next(candidates.Count) ?? 0];
        state.Hand[handIndex] = state.Hand[handIndex] with { FreeThisTurn = true };
    }

    private static int PrintedCost(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        int cost = card.CostForCombat == int.MinValue ? def.Cost : card.CostForCombat;
        return card.Upgraded ? cost + def.UpgradeCost : cost;
    }

    /// <summary>
    /// `card.BaseStarCost > 0` -- the Regent's second resource, which Mummified Hand's
    /// filters count alongside energy.
    /// </summary>
    private static bool HasStarCost(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.HasStarCostX || def.StarCost > 0;
    }

    /// <summary>
    /// The game's `EnergyCost.GetWithModifiers(None) > 0 || BaseStarCost > 0`: a card that
    /// is PRINTED with a price in either resource.
    /// </summary>
    /// <remarks>
    /// The star half was missing, and it is not a Regent-shaped detail so much as a
    /// Regent-shaped BLIND SPOT: most of the character's cards cost 0 energy and several
    /// stars, so Mummified Hand read a whole deck as free and fell through to its
    /// last-resort "anything at all" branch. Both filters are affected, so the pick came
    /// from the wrong pool AND the card-selection stream was drawn against the wrong size.
    /// </remarks>
    private static bool CostsAnything(CardInstance card) =>
        PrintedCost(card) > 0 || HasStarCost(card);

    /// <summary>
    /// Advances a relic's counter and reports whether this was the Nth tick. Absent relic
    /// counts as "no" without touching the list.
    /// </summary>
    private static bool CountTowards(CombatState state, int relicId, int period)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index < 0)
        {
            return false;
        }

        int seen = (state.Relics[index].Counter + 1) % period;
        state.Relics[index] = state.Relics[index] with { Counter = seen };
        return seen == 0;
    }

    /// <summary>
    /// A once-per-combat relic, held as a counter because that is the only per-relic state
    /// there is. Combat setup builds fresh RelicInstances, so the flag clears itself.
    /// </summary>
    private static bool FiresOncePerCombat(CombatState state, int relicId)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index < 0 || state.Relics[index].Counter != 0)
        {
            return false;
        }

        state.Relics[index] = state.Relics[index] with { Counter = 1 };
        return true;
    }

    /// <summary>This relic's counter, or 0 when the run does not hold it.</summary>
    private static int GetCounter(CombatState state, int relicId)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        return index >= 0 ? state.Relics[index].Counter : 0;
    }

    private static void SetCounter(CombatState state, int relicId, int counter)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index >= 0)
        {
            state.Relics[index] = state.Relics[index] with { Counter = counter };
        }
    }

    /// <summary>
    /// The only randomness a relic-driven draw can need is a reshuffle, and that always
    /// reads the run's shuffle stream. The last fallback is only reachable on a state built
    /// without one.
    /// </summary>
    private static Random DrawRng(CombatState state, Random? rng) =>
        state.ShuffleRng ?? rng ?? new Random(0);

    public static void ApplyAfterPlayerHpChanged(CombatState state)
    {
        // LizardTail.ShouldDieLate refuses the death once per run, then heals HealVar(50m)
        // percent of max HP. The relic is spent, not removed, so the counter records it.
        if (state.PlayerHp <= 0 && FiresOncePerCombat(state, LizardTail))
        {
            // Bare on purpose: this write is INSIDE the hook, and routing it through
            // HealPlayer would re-enter. Red Skull is re-read a few lines down anyway,
            // against the revived total.
            state.PlayerHp = Math.Max(1, state.PlayerMaxHp / 2);
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == RedSkull);
        if (index < 0)
        {
            return;
        }

        bool shouldBeActive = state.PlayerHp <= state.PlayerMaxHp / 2;
        bool isActive = state.Relics[index].Counter > 0;

        if (shouldBeActive == isActive)
        {
            return;
        }

        GainPlayerStrength(state, shouldBeActive ? 3 : -3);
        state.Relics[index] = state.Relics[index] with { Counter = shouldBeActive ? 1 : 0 };
    }

    public static void ApplyEndOfPlayerTurn(CombatState state, Random? rng = null)
    {
        // `LunarPastry.AfterSideTurnEnd`: StarsVar(1) whenever the player's side turn
        // ends. Through `GainStars` rather than `Stars +=`, so Black Hole sees it.
        if (HasRelic(state, LunarPastry))
        {
            CardEffects.GainStars(state, 1);
        }

        if (HasRelic(state, Orichalcum) && state.PlayerBlock == 0)
        {
            // BlockVar(6m, ValueProp.Unpowered) — flat, whatever the player's Dexterity.
            CardEffects.GainUnpoweredBlock(state, 6, rng);
        }

        if (HasRelic(state, CloakClasp) && state.Hand.Count > 0)
        {
            // BlockVar(1m, Unpowered) per card left in hand.
            CardEffects.GainUnpoweredBlock(state, state.Hand.Count, rng);
        }

        if (HasRelic(state, ScreamingFlagon) && state.Hand.Count == 0)
        {
            CardEffects.DealUnpoweredDamageToAll(state, 20);
        }

        if (HasRelic(state, StoneCalendar) && state.Turn + 1 == StoneCalendarDamageTurn)
        {
            CardEffects.DealUnpoweredDamageToAll(state, 52);
        }

        // AfterSideTurnEnd rather than Before, so it sees the block the three above added.
        if (HasRelic(state, ParryingShield) && state.PlayerBlock >= 10)
        {
            var target = CardEffects.RandomLivingEnemy(state, rng);
            if (target != null)
            {
                CardEffects.DealUnpoweredDamageToEnemy(state, target, 6);
            }
        }
    }

    /// <summary>Stone Calendar's DynamicVar("DamageTurn", 7m).</summary>
    private const int StoneCalendarDamageTurn = 7;

    /// <summary>Meal Ticket's HealVar(15m), on entering a merchant room.</summary>
    public const int MealTicketHeal = 15;

    /// <summary>Regal Pillow's ModifyRestSiteHealAmount: HealVar(15m) on top.</summary>
    public const int RegalPillowRestHeal = 15;

    public static bool Has(IEnumerable<RelicInstance> relics, int relicId) =>
        relics.Any(relic => relic.DefId == relicId);

    private static bool HasRelic(CombatState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);

    /// <summary>
    /// `ModifyDamageAdditive` from relics that read the CARD. Both are powered-attack only
    /// and both pay a flat 3.
    /// </summary>
    internal static int CardDamageBonus(CombatState state, CardDef def, bool upgraded)
    {
        if (def.Type != CardType.Attack)
        {
            return 0;
        }

        int bonus = 0;
        // MiniatureCannon: `if (!cardSource.IsUpgraded) return 0m;`
        if (upgraded && HasRelic(state, MiniatureCannon))
        {
            bonus += 3;
        }

        // StrikeDummy: `cardSource.Tags.Contains(CardTag.Strike)`. Tags are not extracted,
        // so the NAME stands in -- and here it is exact in both directions: all 22 cards
        // the source tags contain "Strike", and no untagged card does. `EndsWith` is the
        // trap, because the basic strikes are StrikeSilent, StrikeDefect and friends.
        if (HasRelic(state, StrikeDummy) && def.Name.Contains("Strike", StringComparison.Ordinal))
        {
            bonus += 3;
        }

        return bonus;
    }

    /// <summary>
    /// `PenNib.ModifyDamageMultiplicative`: every TENTH Attack the owner plays is doubled.
    /// The counter is incremented in `BeforeCardPlayed`, so the tenth card sees a counter
    /// that has just wrapped to zero.
    /// </summary>
    internal static int CardDamageMultiplier(CombatState state, CardDef def)
    {
        // `VitruvianMinion.ModifyDamageMultiplicative` returns a flat 2 for a card tagged
        // Minion, whatever its TYPE -- so it is read before the Attack gate rather than
        // inside it. Its block half is in `ModifyBlockMultiplicative`, the same rule at
        // the other door: the three Minion cards are Minion Strike, Minion Dive Bomb and
        // Minion Sacrifice, and the relic doubles whichever half each one pays.
        int minion = def.MinionTag && HasRelic(state, VitruvianMinion) ? 2 : 1;

        if (def.Type != CardType.Attack)
        {
            return minion;
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == PenNib);
        return minion
            * (index >= 0 && state.Relics[index].Counter == 0 && state.PenNibArmed ? 2 : 1);
    }

    /// <summary>Vitruvian Minion's block half: 2x the block of a Minion-tagged card.</summary>
    public static int CardBlockMultiplier(CombatState state, CardDef def) =>
        def.MinionTag && HasRelic(state, VitruvianMinion) ? 2 : 1;

    /// <summary>
    /// `UndyingSigil.ModifyDamageMultiplicative`: a powered attack aimed at the OWNER by
    /// an attacker whose current HP is at or below its own Doom lands at half.
    /// </summary>
    /// <remarks>
    /// The relic's own doc comment says it "doesn't actually _do_ anything" and that Doom
    /// checks for it -- but that is about the SECOND thing it does, moving enemy Doom to
    /// the start of the enemy turn so they die before they attack. `ModifyDamageMultiplicative`
    /// right below that comment is a real halving, and it is the reason the relic is worth
    /// buying: an enemy that is about to die to Doom is also hitting you for half.
    /// </remarks>
    public static float IncomingDamageMultiplierFromDoom(CombatState state, EnemyState attacker)
    {
        if (!HasRelic(state, UndyingSigil))
        {
            return 1f;
        }

        int doom = BuffSystem.Get(attacker.Buffs, BuffId.Doom);
        return doom > 0 && attacker.Hp <= doom ? 0.5f : 1f;
    }

    /// <summary>
    /// `PenNib.BeforeCardPlayed` -- the count rises before the card resolves, and wraps at
    /// ten. Called from the play path rather than from AfterCardPlayed, because the card
    /// being played is the one that gets doubled.
    /// </summary>
    internal static void BeforeCardPlayedRelics(CombatState state, CardDef def)
    {
        if (def.Type != CardType.Attack)
        {
            state.PenNibArmed = false;
            return;
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == PenNib);
        if (index < 0)
        {
            return;
        }

        int next = (state.Relics[index].Counter + 1) % 10;
        state.Relics[index] = state.Relics[index] with { Counter = next };
        state.PenNibArmed = next == 0;
    }

    /// <summary>
    /// `MercuryHourglass.AfterPlayerTurnStart` and `Candelabra.AfterSideTurnStart`, both of
    /// which fire at the start of the player's turn.
    /// </summary>
    internal static void ApplyStartOfPlayerTurnShared(CombatState state, int turnNumber, Random? rng)
    {
        if (HasRelic(state, MercuryHourglass))
        {
            CardEffects.DealUnpoweredDamageToAll(state, 3);
        }

        // Candelabra is turn TWO only -- `TurnNumber == 2`, not "from turn two".
        if (turnNumber == 2 && HasRelic(state, Candelabra))
        {
            CardEffects.GainEnergy(state, 2);
        }

        // `VenerableTeaSet.AfterEnergyReset` pays once, on the first energy reset of the
        // combat after a rest site, and clears its own flag. The run arms it on entering
        // the rest site; the existing VenerableTeaSetActive id is that armed marker.
        if (turnNumber == 1 && HasRelic(state, VenerableTeaSetActive))
        {
            CardEffects.GainEnergy(state, 2);
            state.Relics.RemoveAll(relic => relic.DefId == VenerableTeaSetActive);
        }

        _ = rng;
    }

    /// <summary>
    /// `RippleBasin.BeforeSideTurnEnd`: 4 unpowered block if the owner played NO Attack
    /// this turn. The emulator counts attacks per turn already.
    /// </summary>
    /// <summary>`RippleBasin.BeforeSideTurnEnd` — before the hand is flushed.</summary>
    internal static void ApplyBeforeEndOfPlayerTurnShared(CombatState state, Random? rng)
    {
        if (HasRelic(state, RippleBasin) && state.AttackCardsPlayedThisTurn == 0)
        {
            CardEffects.GainUnpoweredBlock(state, 4, rng);
        }
    }

    /// <summary>
    /// `JossPaper.AfterSideTurnEnd` folds the turn's ETHEREAL exhausts into the count in
    /// one go — they are deliberately not counted as they happen.
    /// </summary>
    /// <remarks>
    /// AFTER the hand flush, unlike Ripple Basin above. The distinction is load-bearing:
    /// cards drawn before the flush are thrown straight back out, so a Joss Paper that
    /// paid out early would appear to do nothing at all.
    /// </remarks>
    internal static void ApplyAfterEndOfPlayerTurnShared(CombatState state, Random? rng)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == JossPaper);
        if (index >= 0 && state.EtherealExhaustsThisTurn > 0)
        {
            AddJossPaperExhausts(state, index, state.EtherealExhaustsThisTurn, rng);
        }

        state.EtherealExhaustsThisTurn = 0;
    }

    /// <summary>
    /// `HandDrill.AfterDamageGiven`: Vulnerable 2 on an enemy whose block this hit broke.
    /// </summary>
    internal static void ApplyAfterBlockBroken(CombatState state, EnemyState target)
    {
        if (HasRelic(state, HandDrill) && target.Hp > 0)
        {
            BuffSystem.Apply(target.Buffs, BuffId.Vulnerable, 2);
        }
    }

    /// <summary>
    /// `TheBoot.ModifyHpLostAfterOstyLate`: 1..4 becomes 5. Not a bonus -- a FLOOR, so it
    /// does nothing to a hit that already lands for five or more, and nothing at all to a
    /// hit that landed for zero.
    /// </summary>
    /// <remarks>
    /// The var is named `DamageMinimum` and the relic also declares a `DamageThreshold` of
    /// 4 that its code never reads -- the comparison is `amount >= DamageMinimum`, so the
    /// threshold is display text. Transcribing the threshold instead would give the same
    /// answer here and a different one the day either number moves.
    /// </remarks>
    internal static int BootDamageFloor(CombatState state, int hpLost) =>
        HasRelic(state, TheBoot) && hpLost >= 1 && hpLost < BootDamageMinimum
            ? BootDamageMinimum
            : hpLost;

    private const int BootDamageMinimum = 5;

    /// <summary>
    /// `RazorTooth.AfterCardPlayed`: an Attack or Skill the player plays is UPGRADED, if
    /// it can be. Permanently for the combat, on the copy that was played -- so it lands
    /// in the discard pile upgraded and comes back that way.
    /// </summary>
    internal static bool UpgradesPlayedCard(CombatState state, CardDef def) =>
        HasRelic(state, RazorTooth) && def.Type is CardType.Attack or CardType.Skill;

    /// <summary>
    /// `SparklingRouge.AfterBlockCleared` on TURN THREE only: Strength 1 and Dexterity 1,
    /// once, and then never again for the whole combat.
    /// </summary>
    /// <remarks>
    /// `TurnNumber == 3` is exact -- not "from turn three". Block clears at the start of
    /// the player's turn, so this is the moment turn three begins.
    /// </remarks>
    internal static void ApplyAfterBlockCleared(CombatState state, int turnNumber)
    {
        if (turnNumber == 3 && HasRelic(state, SparklingRouge))
        {
            GainPlayerStrength(state, 1);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
        }
    }

    /// <summary>
    /// Relics the run can hold whose effect is NOT modelled, and why. Declared rather than
    /// left implicit, the way <c>Enchantments.InertInCombat</c> is: an unmodelled relic
    /// that nothing names reads exactly like a modelled one.
    /// </summary>
    /// <remarks>
    /// <b>Lasting Candy</b> ADDS a fourth option to the card-reward screen on every second
    /// combat -- `options.Add(...)`, not a swap. The emulator's screen is three slots
    /// (<c>RunState.RewardCards</c>) and <c>RunConstants.RewardSkipAction</c> is 3, so a
    /// fourth card and the skip would be the same action. Widening it is an ACTION-SPACE
    /// change: it renumbers the reward screen for every trained policy and for the Python
    /// bridge, which is not something to slip in beside a relic. Its trigger clock is
    /// modelled and pinned by <c>LastingCandyTests</c> so the remaining work is the screen
    /// alone.
    /// </remarks>
    public static readonly int[] UnmodelledInRun = [LastingCandy];

    /// <summary>
    /// `LastingCandy.IsInTriggeringCombat`: `CombatsSeen > 0 &amp;&amp; CombatsSeen % 2 == 0`,
    /// where `CombatsSeen` counts up in `AfterCombatEnd` -- so the reward screen of the
    /// second, fourth and sixth fights is the one that gets the extra Power.
    /// </summary>
    public static bool LastingCandyOffersAPower(Run.RunState state)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == LastingCandy);
        return index >= 0 && state.Relics[index].Counter > 0 && state.Relics[index].Counter % 2 == 0;
    }

    /// <summary>Counts the fight the player has just finished.</summary>
    public static void CountCombatForLastingCandy(Run.RunState state)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == LastingCandy);
        if (index >= 0)
        {
            state.Relics[index] = state.Relics[index] with
            {
                Counter = state.Relics[index].Counter + 1,
            };
        }
    }

    /// <summary>
    /// Relics whose counter is RUN state and that a COMBAT can move: the three teas
    /// spend a combat each fight, so the remainder has to travel home.
    /// </summary>
    /// <remarks>
    /// Deliberately a short list rather than "copy every counter back". Most counters are
    /// per-combat tallies -- Shuriken's three attacks, Joss Paper's five exhausts, Pen
    /// Nib's ten -- and carrying those into the run would have the next fight start
    /// part-way through them.
    /// </remarks>
    private static readonly int[] RunCounterRelics = [BoneTea, EmberTea, TeaOfDiscourtesy];

    /// <summary>Writes the combat's remaining counts back onto the run's relics.</summary>
    public static void CarryRunCountersBack(CombatState combat, List<RelicInstance> runRelics)
    {
        foreach (int relicId in RunCounterRelics)
        {
            int inCombat = combat.Relics.FindIndex(relic => relic.DefId == relicId);
            int inRun = runRelics.FindIndex(relic => relic.DefId == relicId);
            if (inCombat >= 0 && inRun >= 0)
            {
                runRelics[inRun] = runRelics[inRun] with
                {
                    Counter = combat.Relics[inCombat].Counter,
                };
            }
        }
    }

    /// <summary>
    /// A tea with combats left spends one and answers true. `IsUsedUp` is
    /// `CombatsLeft &lt;= 0`, so a spent tea is inert but still occupies a relic slot.
    /// </summary>
    private static bool SpendTeaCombat(CombatState state, int relicId)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index < 0 || state.Relics[index].Counter <= 0)
        {
            return false;
        }

        state.Relics[index] = state.Relics[index] with
        {
            Counter = state.Relics[index].Counter - 1,
        };
        return true;
    }

    /// <summary>
    /// `PaperKrane.ModifyWeakMultiplier`: -0.15 when the relic's owner is the TARGET of a
    /// powered attack, so a Weak enemy hits them for 0.60 rather than 0.75.
    /// </summary>
    /// <remarks>
    /// It reads the target, not the attacker -- Paper Krane is a defensive relic that
    /// deepens Weak on things hitting YOU, and does nothing to the Weak you apply.
    /// </remarks>
    public static float WeakMultiplierDeltaAgainstPlayer(CombatState state) =>
        HasRelic(state, PaperKrane) ? -0.15f : 0f;

    /// <summary>
    /// `PaperPhrog.ModifyVulnerableMultiplier`: +0.25 when the target is NOT its owner, so
    /// a Vulnerable enemy takes 1.75 rather than 1.5.
    /// </summary>
    /// <remarks>
    /// The mirror of Paper Krane, and note the asymmetry in the source: the Krane checks
    /// `target != Owner -> unchanged` and the Phrog checks `target == Owner -> unchanged`.
    /// One helps only when you are hit, the other only when you are not.
    /// </remarks>
    public static float VulnerableMultiplierDeltaAgainstEnemies(CombatState state) =>
        HasRelic(state, PaperPhrog) ? 0.25f : 0f;

    /// <summary>
    /// `PowerCmd.Apply` of Strength to the PLAYER, which is the one door Ruined Helmet
    /// stands at. Every grant of player Strength goes through here so the doubling is
    /// answered once rather than at twenty-eight call sites.
    /// </summary>
    /// <remarks>
    /// A LOSS passes through untouched: `TryModifyPowerAmountReceived` refuses
    /// `amount <= 0`, so Shockwave's -2 and the end-of-turn unwind of temporary Strength
    /// are not "doubled" into a bigger loss.
    /// </remarks>
    public static void GainPlayerStrength(CombatState state, int amount)
    {
        BuffSystem.Apply(
            state.PlayerBuffs,
            BuffId.Strength,
            DoubleFirstStrengthReceived(state, amount)
        );
    }

    /// <summary>
    /// `Bookmark.AfterFlush`: ONE of the cards that survived the flush, chosen at random
    /// off `Rng.CombatCardSelection`, costs one less until it is played.
    /// </summary>
    /// <remarks>
    /// The candidate list is the RETAINED cards filtered to `!CostsX && cost > 0` -- a
    /// card already at zero is not a candidate and neither is an X-cost one, so a hand of
    /// free Shivs gets nothing. `AddUntilPlayed(-1)` rides on the copy and survives the
    /// turn, which is why this writes `CostBump` rather than `CostForCombat`.
    ///
    /// The flush is not a discard (see `ApplyAfterCardDiscarded`), so this fires on a
    /// boundary where Tough Bandages and Tingsha deliberately do not.
    /// </remarks>
    internal static void ApplyAfterFlush(CombatState state, Random? rng)
    {
        if (!HasRelic(state, Bookmark))
        {
            return;
        }

        var candidates = Enumerable
            .Range(0, state.Hand.Count)
            .Where(i =>
            {
                var def = GeneratedData.Cards.Get(state.Hand[i].DefId);
                return !def.HasEnergyCostX
                    && CombatEngine.EffectiveCost(state.Hand[i], state) > 0;
            })
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var selection = CardEffects.CardSelectionRngFor(state, rng);
        int chosen = candidates[selection.Next(candidates.Count)];
        state.Hand[chosen] = state.Hand[chosen] with
        {
            CostBump = state.Hand[chosen].CostBump - 1,
        };
    }

    /// <summary>
    /// `GoldPlatedCables.ModifyOrbPassiveTriggerCounts`: `triggerCount + 1` for the orb at
    /// `OrbQueue.Orbs[0]` and no others.
    /// </summary>
    public static int ExtraFrontOrbPassiveTriggers(CombatState state) =>
        HasRelic(state, GoldPlatedCables) ? 1 : 0;

    /// <summary>
    /// `Metronome.AfterOrbChanneled`: the SEVENTH orb channelled in a combat deals
    /// DamageVar(30m, Unpowered) to every hittable enemy. Exactly the seventh -- the test
    /// is `OrbsChanneled == OrbCount`, not `>=`, so an eighth does nothing and the
    /// counter is reset only by entering a new combat room.
    /// </summary>
    internal static void ApplyAfterOrbChanneled(CombatState state)
    {
        if (!HasRelic(state, Metronome))
        {
            return;
        }

        int channelled = GetCounter(state, Metronome) + 1;
        SetCounter(state, Metronome, channelled);
        if (channelled == MetronomeOrbCount)
        {
            CardEffects.DealUnpoweredDamageToAll(state, MetronomeDamage);
        }
    }

    /// <summary>Metronome's `DynamicVar("OrbCount", 7m)` and `DamageVar(30m, Unpowered)`.</summary>
    private const int MetronomeOrbCount = 7;

    private const int MetronomeDamage = 30;

    /// <summary>
    /// `Regalite.AfterCardGeneratedForCombat`: BlockVar(2m, Unpowered) per card the player
    /// generated. Per CARD -- a generator that makes three pays six.
    /// </summary>
    internal static void ApplyAfterCardGenerated(CombatState state, int count, Random? rng)
    {
        if (count > 0 && HasRelic(state, Regalite))
        {
            CardEffects.GainUnpoweredBlock(state, 2 * count, rng);
        }
    }

    /// <summary>
    /// `BoneFlute.AfterAttack`: BlockVar(2m, Unpowered) whenever the OWNER's Osty attacks.
    /// The guard is on the attacker being an Osty whose `PetOwner` is this player, so it
    /// is the pet's swing rather than the card that told it to swing -- one block per
    /// attack, however many the card ordered.
    /// </summary>
    internal static void ApplyAfterOstyAttack(CombatState state, Random? rng)
    {
        if (HasRelic(state, BoneFlute))
        {
            CardEffects.GainUnpoweredBlock(state, 2, rng);
        }
    }

    /// <summary>
    /// `BookRepairKnife.AfterDiedToDoom`: HealVar(3m) for EACH creature that died to Doom,
    /// counting only those whose death triggers fatal effects -- `Powers.All(
    /// ShouldOwnerDeathTriggerFatal)`, which a Minion and an un-detached Decimillipede
    /// segment fail. Nothing at all when the count is zero.
    /// </summary>
    internal static void ApplyAfterDiedToDoom(CombatState state, int fatalDeaths)
    {
        if (fatalDeaths > 0 && HasRelic(state, BookRepairKnife))
        {
            CardEffects.HealPlayer(state, 3 * fatalDeaths);
        }
    }

    /// <summary>
    /// `SneckoSkull.ModifyPowerAmountGivenAdditive`: one more Poison on every Poison the
    /// OWNER applies. Additive on the amount GIVEN, so it lands once per application
    /// rather than once per stack.
    /// </summary>
    public static int ExtraPoisonGiven(CombatState state) =>
        HasRelic(state, SneckoSkull) ? 1 : 0;

    /// <summary>
    /// `RuinedHelmet.TryModifyPowerAmountReceived`: the FIRST positive Strength the player
    /// receives each combat is doubled, then the relic is spent until the combat ends.
    /// </summary>
    /// <remarks>
    /// It is `amount *= 2` on the amount RECEIVED, so it doubles whatever landed -- a
    /// Brimstone turn one gives 4, and everything after it gives the printed number.
    /// </remarks>
    internal static int DoubleFirstStrengthReceived(CombatState state, int amount)
    {
        if (amount <= 0 || !FiresOncePerCombat(state, RuinedHelmet))
        {
            return amount;
        }

        return amount * 2;
    }

    /// <summary>
    /// `Hook.AfterStarsSpent`, which only paying a card's star COST dispatches.
    /// </summary>
    /// <remarks>
    /// Mini-Regent fires once a TURN; Galactic Dust counts stars across the whole RUN
    /// (`[SavedProperty] StarsSpent`) and pays block every tenth. Two relics, two
    /// different clocks, one hook -- and the dust's counter surviving the combat is the
    /// part a per-combat model would get wrong.
    /// </remarks>
    internal static void ApplyAfterStarsSpent(CombatState state, int amount, Random? rng)
    {
        // `MiniRegent`: PowerVar<StrengthPower>(1m), the first spend each turn.
        if (HasRelic(state, MiniRegent) && GetCounter(state, MiniRegent) == 0)
        {
            SetCounter(state, MiniRegent, 1);
            GainPlayerStrength(state, 1);
        }

        // `GalacticDust`: StarsVar(10) and BlockVar(10m, Unpowered). It adds the spend to
        // its counter and, once the counter reaches ten, pays `floor(StarsSpent / 10) * 10`
        // block and takes the counter modulo ten -- so a single spend of twenty-five pays
        // twenty block at once and carries five over, rather than paying ten and losing
        // the rest.
        if (HasRelic(state, GalacticDust))
        {
            int spent = GetCounter(state, GalacticDust) + amount;
            if (spent >= GalacticDustStarsPerBlock)
            {
                CardEffects.GainUnpoweredBlock(
                    state,
                    spent / GalacticDustStarsPerBlock * GalacticDustBlock,
                    rng
                );
                spent %= GalacticDustStarsPerBlock;
            }

            SetCounter(state, GalacticDust, spent);
        }
    }

    /// <summary>Galactic Dust's `StarsVar(10)` and `BlockVar(10m, Unpowered)`.</summary>
    private const int GalacticDustStarsPerBlock = 10;

    private const int GalacticDustBlock = 10;

    /// <summary>
    /// `Hook.AfterCardDiscarded`, once per card an effect discards.
    /// </summary>
    /// <remarks>
    /// Both relics guard on `Owner.Creature.Side == CombatState.CurrentSide` -- the
    /// discard has to happen during the player's own turn. Every effect-driven discard in
    /// the emulator does, and the END-OF-TURN hand dump is not a discard at all:
    /// `FlushPlayerHand` is a plain `CardPileCmd.Add` followed by `Hook.AfterFlush`, with
    /// no `CardDiscarded` history row and no `AfterCardDiscarded` between them. **A hand
    /// emptied at end of turn pays neither of these relics**, which is most of what a
    /// player would assume they do.
    /// </remarks>
    internal static void ApplyAfterCardDiscarded(CombatState state, Random? rng)
    {
        // `ToughBandages`: BlockVar(3m, Unpowered) -- Dexterity does not raise it.
        if (HasRelic(state, ToughBandages))
        {
            CardEffects.GainUnpoweredBlock(state, 3, rng);
        }

        // `Tingsha`: DamageVar(3m, Unpowered) to `Rng.CombatTargets.NextItem(
        // HittableEnemies)` -- one random enemy per card, re-rolled each time.
        if (HasRelic(state, Tingsha))
        {
            // `state.TargetRng` is the run's CombatTargets stream and takes precedence
            // inside RandomLivingEnemy; the parameter is only the single-combat fallback.
            var target = CardEffects.RandomLivingEnemyFor(state, rng ?? new Random(0));
            if (target is not null)
            {
                CardEffects.DealUnpoweredDamage(state, target, 3);
            }
        }
    }

    /// <summary>
    /// `JossPaper.AfterCardExhausted`: every FIVE cards exhausted draws one. An exhaust    /// <summary>
    /// `JossPaper.AfterCardExhausted`: every FIVE cards exhausted draws one. An exhaust
    /// caused by Ethereal is banked for the end of the turn instead of counting now.
    /// </summary>
    internal static void ApplyAfterCardExhausted(
        CombatState state,
        bool causedByEthereal,
        Random? rng
    )
    {
        // `CharonsAshes.AfterCardExhausted`: DamageVar(3m, Unpowered) to every hittable
        // enemy, per card. No Ethereal exception -- a card exhausted by Ethereal at end of
        // turn pays it too, unlike Joss Paper's banked count below.
        if (HasRelic(state, CharonsAshes))
        {
            CardEffects.DealUnpoweredDamageToAll(state, 3);
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == JossPaper);
        if (index < 0)
        {
            return;
        }

        if (causedByEthereal)
        {
            state.EtherealExhaustsThisTurn++;
            return;
        }

        AddJossPaperExhausts(state, index, 1, rng);
    }

    private static void AddJossPaperExhausts(CombatState state, int index, int count, Random? rng)
    {
        int total = state.Relics[index].Counter + count;
        int draws = total / 5;
        state.Relics[index] = state.Relics[index] with { Counter = total % 5 };
        if (draws > 0 && rng is not null)
        {
            CardEffects.DrawCards(state, draws, rng);
        }
    }

    /// <summary>
    /// `Vambrace.ModifyBlockMultiplicative`: the FIRST card each combat that gains block
    /// gains double. `BlockGainedThisCombat` latches once the gain actually lands.
    /// </summary>
    internal static bool DoublesCardBlock(CombatState state) =>
        HasRelic(state, Vambrace) && !state.VambraceSpent;

    /// <summary>`PaelsLegion`'s `DynamicVar("Turns", 2)`.</summary>
    internal const int PaelsLegionCooldownTurns = 2;

    /// <summary>
    /// `PaelsLegion.ModifyBlockMultiplicative`: the pet doubles a CARD's block whenever
    /// its cooldown is clear, then sits out `Turns` of them. The relic was an id in
    /// `EnemyAI` and a Pael blessing option -- the pet existed and did nothing.
    /// </summary>
    internal static bool PaelsLegionDoublesBlock(CombatState state) =>
        HasRelic(state, PaelsLegion) && state.PaelsLegionCooldown <= 0;

    /// <summary>
    /// `PaelsLegion.AfterSideTurnStart` ticks the cooldown down at the start of each of
    /// its owner's turns.
    /// </summary>
    internal static void TickPaelsLegionCooldown(CombatState state)
    {
        if (state.PaelsLegionCooldown > 0)
        {
            state.PaelsLegionCooldown--;
        }
    }

    /// <summary>
    /// `ModifyHpLostAfterOsty` from the two relics that reduce or cap HP loss. Both are
    /// `Late`-ish modifiers on the amount that would actually come off, so they run after
    /// block has been taken off and before the HP is.
    /// </summary>
    internal static int ModifyHpLost(CombatState state, int hpLoss)
    {
        // TungstenRod: `Math.Max(0m, amount - 1m)`.
        if (HasRelic(state, TungstenRod))
        {
            hpLoss = Math.Max(0, hpLoss - 1);
        }

        // BeatingRemnant: `Math.Min(amount, 20 - DamageReceivedThisTurn)` -- a cap on the
        // TURN's total unblocked damage, not on one hit, and the running total resets at
        // the owner's side-turn start.
        if (HasRelic(state, BeatingRemnant))
        {
            hpLoss = Math.Max(0, Math.Min(hpLoss, 20 - state.UnblockedDamageThisTurn));
        }

        return hpLoss;
    }

    /// <summary>
    /// `IceCream.ShouldPlayerResetEnergy` returns FALSE from turn two onwards — so energy
    /// carries over instead of being refilled. Turn one still resets, which is what puts
    /// the starting energy on the board at all.
    /// </summary>
    internal static bool ShouldResetEnergy(CombatState state, int turnNumber) =>
        turnNumber <= 1 || !HasRelic(state, IceCream);

    /// <summary>
    /// `SturdyClamp.ShouldClearBlock` returns false for its owner, and
    /// `AfterPreventingBlockClear` then trims whatever is over ten. So it is not
    /// Barricade: block survives, but only ten of it.
    /// </summary>
    internal static bool KeepsBlockCappedAtTen(CombatState state) => HasRelic(state, SturdyClamp);

    /// <summary>
    /// `Bellows`, `Chandelier`, `VexingPuzzlebox` — the rest of the turn-start rares.
    /// </summary>
    internal static void ApplyStartOfPlayerTurnRares(CombatState state, int turnNumber, Random? rng)
    {
        if (turnNumber == 3 && HasRelic(state, Chandelier))
        {
            CardEffects.GainEnergy(state, 3);
        }

        if (turnNumber == 1 && HasRelic(state, Bellows))
        {
            // `CardCmd.Upgrade(PileType.Hand.GetPile(owner).Cards)` -- the opening hand,
            // and only the opening hand.
            for (int i = 0; i < state.Hand.Count; i++)
            {
                state.Hand[i] = state.Hand[i] with { Upgraded = true };
            }
        }

        if (turnNumber == 1 && HasRelic(state, VexingPuzzlebox) && rng is not null)
        {
            // A card from the character's WHOLE pool, not just the powers -- Creative Ai
            // filters to Power and this does not.
            CardEffects.AddRandomPoolCardToHand(state, rng);
        }
    }

    /// <summary>
    /// `GamePiece` (draw after a Power) and `RainbowRing` (one Attack, one Skill and one
    /// Power in a turn pays Strength and Dexterity, once).
    /// </summary>
    internal static void ApplyAfterCardPlayedRares(CombatState state, CardDef def, Random? rng)
    {
        if (def.Type == CardType.Power && HasRelic(state, GamePiece) && rng is not null)
        {
            CardEffects.DrawCards(state, 1, rng);
        }

        if (!HasRelic(state, RainbowRing) || state.RainbowRingPaidThisTurn)
        {
            return;
        }

        // `ActivationCountThisTurn < 1` -- once a turn, and the counts are only advanced
        // while it has not yet paid.
        switch (def.Type)
        {
            case CardType.Attack:
                state.RainbowRingAttacks++;
                break;
            case CardType.Skill:
                state.RainbowRingSkills++;
                break;
            case CardType.Power:
                state.RainbowRingPowers++;
                break;
        }

        if (
            state.RainbowRingAttacks > 0
            && state.RainbowRingSkills > 0
            && state.RainbowRingPowers > 0
        )
        {
            GainPlayerStrength(state, 1);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
            state.RainbowRingPaidThisTurn = true;
        }
    }

    /// <summary>
    /// `IntimidatingHelmet.BeforeCardPlayed`: 4 unpowered block when the card actually
    /// COST two or more — `cardPlay.Resources.EnergyValue`, so a cost reduction or a free
    /// play stops it paying.
    /// </summary>
    internal static void ApplyBeforeCardPlayedRares(CombatState state, int energySpent, Random? rng)
    {
        if (energySpent >= 2 && HasRelic(state, IntimidatingHelmet))
        {
            CardEffects.GainUnpoweredBlock(state, 4, rng);
        }
    }

    /// <summary>
    /// `UnceasingTop.AfterHandEmptied`: draw a card whenever the hand runs out during the
    /// PLAY phase — not at turn start, and not while a screen is up.
    /// </summary>
    internal static void ApplyAfterHandEmptied(CombatState state, Random? rng)
    {
        if (state.Hand.Count == 0 && HasRelic(state, UnceasingTop) && rng is not null)
        {
            CardEffects.DrawCards(state, 1, rng);
        }
    }

    /// <summary>
    /// `PrayerWheel` and `WhiteStar` each add a whole extra `CardReward` — the Wheel after
    /// a MONSTER room, the Star after an ELITE, and the Star's three come from the BOSS
    /// pool rather than the room's own.
    /// </summary>
    internal static bool AddsExtraCardReward(Run.RunState state, int roomNodeType) =>
        (roomNodeType == Run.RunConstants.NodeNormal && Has(state.Relics, PrayerWheel))
        || (roomNodeType == Run.RunConstants.NodeElite && Has(state.Relics, WhiteStar));

    /// <summary>`TheCourier.ModifyMerchantPrice`: a flat 20% off everything the shop sells.</summary>
    internal static int ModifyMerchantPrice(Run.RunState state, int price) =>
        Has(state.Relics, TheCourier) ? (int)(price * 0.8m) : price;

    /// <summary>
    /// `Hook.AfterRoomEntered`, which the game fires from each room's `Enter()` — Combat,
    /// Merchant, Treasure, Event and RestSite, and nothing else.
    /// </summary>
    /// <remarks>
    /// <paramref name="cameFromUnknown" /> is separate from the room type on purpose.
    /// Planisphere asks about the MAP POINT (`CurrentMapPoint.PointType == Unknown`) and
    /// not about what the point resolved into, so it pays out on whichever room a "?"
    /// turned out to be — including a fight.
    /// </remarks>
    public static void ApplyAfterRoomEntered(
        Run.RunState state,
        bool isRestSite,
        bool cameFromUnknown
    )
    {
        // `EternalFeather.AfterRoomEntered(room is RestSiteRoom)`: HealVar(3) per
        // CardsVar(5) cards in the deck, integer division -- 12 cards is two, not two and
        // a bit.
        if (isRestSite && Has(state.Relics, EternalFeather))
        {
            int heal = state.Deck.Count / 5 * 3;
            // Run-side: `AfterCurrentHpChanged`'s listeners all gate on
            // `CombatManager.IsInProgress`, so a heal between combats dispatches nothing.
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + heal);
        }

        if (cameFromUnknown && Has(state.Relics, Planisphere))
        {
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 5);
        }
    }

    /// <summary>
    /// `Bread.ModifyMaxEnergy`: +1 from turn TWO onwards. Turn one is excluded here and
    /// then charged 2 at its side-turn start, which is the card's whole shape — a bad
    /// first turn bought with a better every turn after.
    /// </summary>
    internal static int ModifyMaxEnergy(CombatState state, int maxEnergy, int turnNumber) =>
        turnNumber > 1 && HasRelic(state, Bread) ? maxEnergy + 1 : maxEnergy;

    /// <summary>
    /// `LavaLamp.TryModifyCardRewardOptionsLate`: every upgradable option on the card
    /// reward screen is upgraded, if the combat took no UNBLOCKED, blockable damage.
    /// </summary>
    /// <remarks>
    /// Its `AfterDamageReceived` ignores `ValueProp.Unblockable` — so a Burn or a curse's
    /// self-damage does not spoil it, and only damage the player could have blocked does.
    /// </remarks>
    public static bool UpgradesCardRewards(Run.RunState state) =>
        Has(state.Relics, LavaLamp) && !state.TookUnblockedDamageThisCombat;

    /// <summary>
    /// `DingyRug.ModifyCardRewardCreationOptions`: the COLOURLESS pool is added to the
    /// pools a card reward rolls from — added, not replaced, so the character's own cards
    /// are still on offer.
    /// </summary>
    public static bool AddsColourlessToCardRewards(Run.RunState state) =>
        Has(state.Relics, DingyRug);

    /// <summary>
    /// `WingCharm.TryModifyCardRewardOptionsLate`: ONE option on the screen, rolled on
    /// `Rng.Niche` from those that can take it, gains the Swift enchantment.
    /// </summary>
    public static bool EnchantsACardReward(Run.RunState state) => Has(state.Relics, WingCharm);

    /// <summary>
    /// `MysticLighter.ModifyDamageAdditive`: 9 more from a powered attack whose card
    /// carries ANY enchantment — `cardSource?.Enchantment == null` is the only filter.
    /// </summary>
    internal static int EnchantedCardDamageBonus(CombatState state) =>
        HasRelic(state, MysticLighter) ? 9 : 0;

    /// <summary>`ChemicalX.ModifyXValue`: every X-cost card resolves two higher.</summary>
    internal static int ModifyXValue(CombatState state, int x) =>
        HasRelic(state, ChemicalX) ? x + 2 : x;

    /// <summary>
    /// `RingingTriangle.ShouldFlush` returns false on turn ONE, so the opening hand is
    /// retained whole rather than discarded.
    /// </summary>
    internal static bool SkipsHandFlush(CombatState state, int turnNumber) =>
        turnNumber <= 1 && HasRelic(state, RingingTriangle);

    /// <summary>
    /// `MiniatureTent.ShouldDisableRemainingRestSiteOptions` returns false — so taking one
    /// rest option leaves the others available instead of ending the visit.
    /// </summary>
    public static bool KeepsRestSiteOpen(Run.RunState state) => Has(state.Relics, MiniatureTent);

    /// <summary>`TheAbacus.AfterShuffle`: 6 unpowered block every time the pile is shuffled.</summary>
    internal static void ApplyAfterShuffle(CombatState state, Random? rng)
    {
        if (HasRelic(state, TheAbacus))
        {
            CardEffects.GainUnpoweredBlock(state, 6, rng);
        }

        // `BiiigHug.AfterShuffle`: a SOOT into the draw pile at a random position, every
        // shuffle, for the whole run. Only its pickup half -- remove four chosen cards --
        // was modelled, which made a hug that costs nothing a hug that only pays.
        if (HasRelic(state, BiiigHug))
        {
            CardEffects.AddCardToDrawPileRandomly(state, ST.Soot, 1, rng ?? new Random());
        }
    }

    /// <summary>
    /// `BurningSticks.AfterCardExhausted`: the first SKILL exhausted each combat is copied
    /// back into hand. Once per combat, and Skills only.
    /// </summary>
    internal static void ApplyBurningSticks(CombatState state, CardInstance card)
    {
        if (
            state.BurningSticksUsed
            || !HasRelic(state, BurningSticks)
            || GeneratedData.Cards.Get(card.DefId).Type != CardType.Skill
        )
        {
            return;
        }

        state.BurningSticksUsed = true;
        if (state.Hand.Count < CardEffects.MaxCardsInHand)
        {
            state.Hand.Add(card with { FreeThisTurn = false });
        }
    }

    /// <summary>
    /// `BeltBuckle`: Dexterity 2 while the owner holds NO potions, applied and removed as
    /// the belt fills and empties rather than checked once.
    /// </summary>
    /// <remarks>
    /// Re-evaluated at every point the game hooks — combat start, and after a potion is
    /// procured, used or discarded — because the whole design is that it toggles. A
    /// once-at-combat-start reading would give the Dexterity to a player who then drinks
    /// their way out of it, and withhold it from one who empties their belt mid-fight.
    /// </remarks>
    internal static void RefreshBeltBuckle(CombatState state)
    {
        if (!HasRelic(state, BeltBuckle))
        {
            return;
        }

        bool shouldHold = !state.PotionSlots.Any(slot => slot != 0);
        if (shouldHold == state.BeltBuckleApplied)
        {
            return;
        }

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, shouldHold ? 2 : -2);
        state.BeltBuckleApplied = shouldHold;
    }

    /// <summary>
    /// `GhostSeed.AfterCardEnteredCombat`: every BASIC Strike or Defend gains Ethereal.
    /// </summary>
    /// <remarks>
    /// The tag stand-in is the same one `Card.IsStrikeOrDefend` uses, and here the caveat
    /// does not bite: the filter is `Rarity == Basic` AND tagged, and among Basic cards
    /// the entry slug and the tag agree for every character.
    /// </remarks>
    internal static bool MakesBasicsEthereal(CombatState state) => HasRelic(state, GhostSeed);

    /// <summary>
    /// `GamblingChip.AfterPlayerTurnStart` on turn one: a discard screen with no upper
    /// bound, and the draw comes after.
    /// </summary>
    /// <summary>
    /// `Toolbox.BeforeHandDraw` on turn one: three distinct COLOURLESS cards offered, one
    /// taken into hand. A choose-a-card screen, not a random grant.
    /// </summary>
    internal static bool OpensToolboxScreen(CombatState state, int turnNumber) =>
        turnNumber <= 1 && HasRelic(state, Toolbox);

    internal static bool OpensGamblingChipScreen(CombatState state, int turnNumber) =>
        turnNumber <= 1 && state.Hand.Count > 0 && HasRelic(state, GamblingChip);

    /// <summary>
    /// `UnsettlingLamp`: the FIRST card of a combat that lands a debuff on an enemy has
    /// its debuffs doubled — all of them, for that one card.
    /// </summary>
    /// <remarks>
    /// The game latches in `BeforePowerAmountChanged` on the first qualifying application
    /// and unlatches in `AfterCardPlayed` for that same card, so a card applying three
    /// debuffs gets all three doubled and the next card gets none. Modelled with a
    /// per-combat spent flag plus a per-card latch for the same reason.
    ///
    /// The game also excludes debuffs whose source is a TEMPORARY power already doubled
    /// (`HasDoubledTemporaryPowerSource`), which stops the internally-applied Strength of
    /// a TemporaryStrengthPower being doubled twice. The emulator applies those two as one
    /// pair through `ApplyTemporaryStrengthDownTo`, so the double lands once either way.
    /// </remarks>
    internal static int ModifyEnemyDebuffMagnitude(CombatState state, BuffId id, int magnitude)
    {
        if (magnitude == 0 || state.UnsettlingLampSpent || !HasRelic(state, UnsettlingLamp))
        {
            return magnitude;
        }

        // `power.GetTypeForAmount(amount) != PowerType.Debuff` -- the SIGN decides for a
        // power that reads both ways. Strength down is a negative magnitude and is very
        // much a debuff; Weak and Vulnerable are positive ones. Reading only positives
        // doubled Malaise's Weak and left its Strength loss alone, which is half a card.
        bool isDebuff = id == BuffId.Strength ? magnitude < 0 : magnitude > 0;
        if (!isDebuff)
        {
            return magnitude;
        }

        state.UnsettlingLampCard = true;
        return magnitude * 2;
    }

    /// <summary>
    /// `AfterCardPlayed` on the latched card: the doubling is finished for the combat.
    /// </summary>
    internal static void FinishUnsettlingLampCard(CombatState state)
    {
        if (state.UnsettlingLampCard)
        {
            state.UnsettlingLampCard = false;
            state.UnsettlingLampSpent = true;
        }
    }

    /// <summary>
    /// `Girya.AfterRoomEntered(CombatRoom)`: Strength equal to the number of times it has
    /// been LIFTED, at the start of every combat. Zero lifts is zero Strength, so a Girya
    /// nobody rested with does nothing at all.
    /// </summary>
    public static int GiryaStrength(Run.RunState state)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == Girya);
        return index < 0 ? 0 : state.Relics[index].Counter;
    }

    /// <summary>
    /// `Pantograph.BeforeCombatStart`: 25 HP, but only when the room is a BOSS. Its
    /// `AfterRoomEntered` sets a display status and nothing else, which is why the heal
    /// lives here rather than in the room hook above.
    /// </summary>
    public static void ApplyBeforeBossCombat(Run.RunState state)
    {
        if (Has(state.Relics, Pantograph))
        {
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 25);
        }
    }

    /// <summary>
    /// `AmethystAubergine.TryModifyRewards`: a GoldVar(15) reward after any COMBAT room,
    /// except the boss of the final act — there is no reward screen to add it to when the
    /// run is over.
    /// </summary>
    /// <remarks>
    /// Returns the RAW 15. The reward's gold is claimed through `RunNonCombatEffects.GainGold`
    /// like any other, and that applies `ModifyGoldGained` -- applying it here too would pay
    /// Bowler Hat twice on this one relic.
    /// </remarks>
    public static int ExtraCombatRewardGold(Run.RunState state, bool isFinalActBoss) =>
        !isFinalActBoss && Has(state.Relics, AmethystAubergine) ? 15 : 0;

    /// <summary>
    /// `JuzuBracelet.ModifyUnknownMapPointRoomTypes`: a "?" can never be a Monster room.
    /// The relic removes the type from the allowed SET before the odds are rolled, so the
    /// probability mass redistributes rather than the roll being re-taken.
    /// </summary>
    public static bool ForbidsUnknownMonsterRooms(Run.RunState state) =>
        Has(state.Relics, JuzuBracelet);

    /// <summary>
    /// `ReptileTrinket.AfterPotionUsed`: 3 Strength, and it is a TemporaryStrengthPower --
    /// handed back at the end of the turn, like Piercing Wail's loss in the other
    /// direction.
    /// </summary>
    internal static void ApplyAfterPotionUsed(CombatState state)
    {
        if (HasRelic(state, ReptileTrinket))
        {
            // The player's restore is `Strength += -TemporaryStrength`, so a grant that
            // should expire records the SAME sign it was given at, not the opposite.
            GainPlayerStrength(state, 3);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryStrength, 3);
        }
    }
}
