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
        state.Energy += extraEnergy;

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

        if (HasRelic(state, BloodVial))
        {
            state.PlayerHp = Math.Min(state.PlayerHp + 2, state.PlayerMaxHp);
        }

        if (HasRelic(state, Anchor))
        {
            // BlockVar(10m, ValueProp.Unpowered) — Dexterity does not raise it.
            CardEffects.GainUnpoweredBlock(state, 10, rng);
        }

        if (HasRelic(state, Vajra))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
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

        if (HasRelic(state, BagOfPreparation))
        {
            CardEffects.DrawCards(state, 2, rng);
        }

        if (HasRelic(state, BoomingConch) && state.IsEliteCombat)
        {
            CardEffects.DrawCards(state, 2, rng);
            state.Energy += 1;
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
                state.Energy += 1;
            }

            if (HasRelic(state, VenerableTeaSetActive))
            {
                state.Energy += 2;
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
            state.Energy += 1;
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
            state.Energy += 1;
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
            state.Energy += 1;
        }

        switch (def.Type)
        {
            case CardType.Attack:
                if (CountTowards(state, Shuriken, period: 3))
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
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
                    state.Energy += 1;
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
    public static void ApplyAfterUnblockedDamageReceived(CombatState state, Random? rng = null)
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
        var printedCosted = all.Where(i => PrintedCost(state.Hand[i]) > 0).ToList();
        var stillCosts = all.Where(i => CombatEngine.EffectiveCost(state.Hand[i], state) > 0)
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

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, shouldBeActive ? 3 : -3);
        state.Relics[index] = state.Relics[index] with { Counter = shouldBeActive ? 1 : 0 };
    }

    public static void ApplyEndOfPlayerTurn(CombatState state, Random? rng = null)
    {
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
        if (def.Type != CardType.Attack)
        {
            return 1;
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == PenNib);
        return index >= 0 && state.Relics[index].Counter == 0 && state.PenNibArmed ? 2 : 1;
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
            state.Energy += 2;
        }

        // `VenerableTeaSet.AfterEnergyReset` pays once, on the first energy reset of the
        // combat after a rest site, and clears its own flag. The run arms it on entering
        // the rest site; the existing VenerableTeaSetActive id is that armed marker.
        if (turnNumber == 1 && HasRelic(state, VenerableTeaSetActive))
        {
            state.Energy += 2;
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
    /// `JossPaper.AfterCardExhausted`: every FIVE cards exhausted draws one. An exhaust
    /// caused by Ethereal is banked for the end of the turn instead of counting now.
    /// </summary>
    internal static void ApplyAfterCardExhausted(
        CombatState state,
        bool causedByEthereal,
        Random? rng
    )
    {
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
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 3);
            BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryStrength, 3);
        }
    }
}
