namespace Sts2Emulator.Core;

public sealed class CombatState
{
    /// <summary>
    /// The run's ascension level, which is an INPUT to enemy data rather than a
    /// difficulty label: the game picks monster damage with
    /// <c>GetValueIfAscension(level, high, low)</c>, so A8 and A10 are different
    /// numbers for the same enemy. Kept on the state so captures at different levels
    /// can be compared in one process — see Core/Ascension.cs.
    /// </summary>
    public int AscensionLevel = Ascension.DefaultLevel;

    // Player
    public int PlayerHp;
    public int PlayerMaxHp;
    public int PlayerBlock;
    public int Energy;
    public int MaxEnergy;
    public int PlayerGold;

    /// <summary>
    /// Gold a Heist gave back on its owner's death, waiting for the reward screen.
    /// </summary>
    /// <remarks>
    /// <c>HeistPower.BeforeDeath</c> calls <c>combatRoom.AddExtraReward(new GoldReward(
    /// Amount, wasGoldStolenBack: true))</c> — the player has to CLAIM it, and a capture
    /// shows it as its own row reading "80 Gold (stolen back)" beside the fight's ordinary
    /// gold. Handing it straight back mid-combat skipped that row.
    /// </remarks>
    public int StolenBackGold;

    /// <summary>A Fat Gremlin left the fight under its own steam rather than dying.</summary>
    /// <remarks>
    /// The emulator ends its escape by setting <c>Hp = 0</c>, which makes it
    /// indistinguishable from a kill — and the two owe the player opposite things.
    /// <c>GremlinMercNormal.CalculateGoldProportion</c> pays the fight in full when
    /// nothing escaped and nothing at all when a gremlin escaped carrying stolen gold.
    /// </remarks>
    public bool FatGremlinEscaped;

    /// <summary>
    /// <c>GremlinMercNormal.GoldWasStolen</c>: the merc died having taken something.
    /// <c>SurprisePower.AfterDeath</c> only marks it when the total is above zero.
    /// </summary>
    public bool MercGoldWasStolen;

    // Cards
    public List<CardInstance> Hand = [];
    public List<CardInstance> DrawPile = [];
    public List<CardInstance> DiscardPile = [];
    public List<CardInstance> ExhaustPile = [];
    public List<CardInstance> ReturnToHandBeforeDraw = [];

    /// <summary>
    /// Clones queued by <c>NightmarePower</c>, delivered to hand at the start of the next
    /// turn BEFORE the draw and then dropped — the power removes itself once it fires, so
    /// this is a one-shot queue and not a standing effect.
    /// </summary>
    public List<CardInstance> CopiesToHandBeforeDraw = [];

    /// <summary>
    /// Set while a `RetainForNextTurn` screen stands between the player ending their turn
    /// and the turn actually ending — the end turn is owed and runs as soon as the screen
    /// is answered.
    /// </summary>
    public bool EndTurnAwaitingSelection;

    /// <summary>
    /// `AfterimagePower.BeforeCardPlayed` records the power's amount for the card about to
    /// be played, and `AfterCardPlayed` spends THAT rather than whatever the amount has
    /// become. Read before the card resolves for the reason its own Data comment gives:
    /// so an Afterimage does not pay out on its own play.
    /// </summary>
    public int AfterimageBeforePlay;

    /// <summary>
    /// The same reading, for the two powers that pay out AFTER a Power card resolves.
    /// `StormPower` and `SubroutinePower` both keep a
    /// `Dictionary&lt;CardModel, int&gt; amountsForPlayedCards` filled in
    /// `BeforeCardPlayed`, with the same comment on it that `AfterimagePower` has: it
    /// stops the power triggering on its own play, and stops a second copy paying out
    /// twice on the turn it arrives.
    /// </summary>
    public int StormBeforePlay;

    /// <inheritdoc cref="StormBeforePlay" />
    public int SubroutineBeforePlay;

    /// <summary>
    /// Status cards drawn this turn, for `IterationPower` — which fires only on the FIRST
    /// one, and so needs the count rather than a flag per draw.
    /// </summary>
    public int StatusCardsDrawnThisTurn;

    /// <summary>
    /// `Rng.CombatOrbGeneration`: the stream Chaos rolls its orb type on. Kept apart from
    /// the card-generation and target streams for the same reason those are kept apart
    /// from each other — sharing one desynchronises everything downstream.
    /// </summary>
    public CountingRandom? OrbGenerationRng;

    /// <summary>
    /// Whether the card currently resolving is tagged <c>CardTag.Defend</c>, which is what
    /// `FastenPower.ModifyBlockAdditive` asks about the block's `cardSource`. Set for the
    /// duration of the card so any block it gains carries the tag, rather than every arm
    /// that blocks having to remember to say so.
    /// </summary>
    public bool ResolvingDefendCard;

    /// <summary>
    /// `EnergyCost.SetThisCombat(n)` on the card being played — Momentum Strike zeroes its
    /// own cost for the rest of the combat. `int.MinValue` means the card said nothing.
    /// </summary>
    /// <remarks>
    /// Handed back through the state for the same reason `PlayedCardCostBump` is:
    /// `CardEffects.Apply` takes the card BY VALUE, so an arm that assigns to its own
    /// parameter changes nothing at all. Momentum Strike did exactly that and had been
    /// free-after-one-play only in the local variable.
    /// </remarks>
    public int PlayedCardCostForCombat = int.MinValue;
    public List<CardInstance> AutoPlayQueue = [];

    // Defect-style orb queue.
    public List<OrbState> Orbs = [];
    public int OrbCapacity = 3;

    // Necrobinder pet state.
    public int OstyHp;
    public int OstyMaxHp;

    // Regent star resource.
    public int Stars;

    /// <summary>
    /// Energy spent since Orbit was played, for `OrbitPower`'s every-fourth payout. Its
    /// own counter rather than `EnergySpentThisTurn` because the game's is per POWER
    /// INSTANCE and cumulative over the combat, not per turn.
    /// </summary>
    public int OrbitEnergySpent;

    /// <summary>
    /// Each enemy's Strangle amount as it stood BEFORE the current card resolved, so a
    /// card that applies or stacks Strangle does not make the enemy pay for itself.
    /// </summary>
    public List<int> StrangleBeforePlay = [];

    // Potions: slot index → potion def ID, 0 = empty
    public int[] PotionSlots = new int[3];

    /// <summary>Every card the fight is holding, wherever it sits.</summary>
    public IEnumerable<CardInstance> AllCards() =>
        Hand.Concat(DrawPile).Concat(DiscardPile).Concat(ExhaustPile);

    public int MaxPotionSlots = 3;

    // Relics
    public List<RelicInstance> Relics = [];

    // Buffs/debuffs on the player
    public List<BuffState> PlayerBuffs = [];

    /// <summary>
    /// The player's duration debuffs as the round began, for the one-tick grace the game
    /// gives anything applied to a player-side creature (PowerCmd sets
    /// SkipNextDurationTick). See CombatEngine.TickDurationDebuffs.
    /// </summary>
    public List<BuffState> PlayerDebuffsAtRoundStart = [];

    // Enemies
    public List<EnemyState> Enemies = [];
    public int EncounterId;
    public bool IsEliteCombat;

    // Shuffle RNG (RunRngSet.shuffle subsystem) — used for mid-combat discard reshuffles.
    // Null falls back to the combat RNG (only valid when no pre-shuffle was done).
    // CountingRandom tracks total Next() calls so RunEngine can sync its shuffle RNG.
    public CountingRandom? ShuffleRng;

    // Target RNG (RunRngSet.combat_targets subsystem) — used whenever an effect picks
    // which enemy to hit (Juggernaut, Volley, Sword Boomerang). Null falls back to the
    // combat RNG (only valid in single-combat tests).
    public CountingRandom? TargetRng;

    // Card-selection RNG (RunRngSet.combat_card_selection subsystem) — used whenever an
    // effect picks WHICH existing card to exhaust, transform or otherwise act on (Cinder,
    // Thrash, unupgraded True Grit, Entropy). Distinct from combat_card_generation, which
    // rolls up a NEW card (Infernal Blade, Splash, Stoke). Null falls back to the combat
    // RNG (only valid in single-combat tests).
    public CountingRandom? CardSelectionRng;

    // Card-generation RNG (RunRngSet.combat_card_generation subsystem) — used when an
    // effect rolls up a NEW card (Stoke, Splash, Infernal Blade, Discovery). Distinct
    // from combat_card_selection, which picks among cards that already exist. Null falls
    // back to the combat RNG (only valid in single-combat tests).
    public CountingRandom? CardGenerationRng;

    // Potion-generation RNG (RunRngSet.combat_potion_generation subsystem) — used when a
    // card rolls up a potion in combat (Alchemize). Null falls back to the combat RNG
    // (only valid in single-combat tests).
    public CountingRandom? PotionGenerationRng;

    // AI RNG (RunRngSet.monster_ai subsystem) — used for enemy intent selection.
    // Null falls back to the combat RNG (used in single-combat tests).
    public Random? AiRng;

    // Niche HP RNG — used ONLY for SetUniqueMonsterHpValue (CreateEnemy HP calls).
    // When non-null, CreateEnemy uses this instead of the main combat RNG for HP.
    // CountingRandom.CallCount tracks how many HP values were drawn (= enemy count).
    public CountingRandom? NicheHpRng;

    // A card-selection screen the play is waiting on. Non-null blocks every other
    // action until it is answered — see PendingCardSelection.
    public PendingCardSelection? PendingSelection;

    // True while the engine is playing a card the player did not choose to play
    // (Havoc, Hellraiser, Stampede, Mayhem). The game still prompts for choices there,
    // but the emulator has no way to hand an auto-play back to the caller mid-queue, so
    // those resolve with the old automatic pick.
    public bool AutoPlaying;

    // Damage to add to the copy currently being played, before it lands in a pile. Set by
    // CardEffects during Apply and consumed by CombatEngine.PlayCard, because Apply takes
    // the CardInstance by value and cannot hand a mutation back any other way.
    public int PlayedCardBonusDamage;

    /// <summary>The same channel for block. Genetic Algorithm is the only card using it.</summary>
    public int PlayedCardBonusBlock;

    /// <summary>
    /// Energy SPENT this turn, which Helix Drill counts its hits from — the sum of the
    /// turn's `EnergySpentEntry` amounts. Not the energy REMAINING, which is what the
    /// emulator was reading and is very nearly its opposite.
    /// </summary>
    public int EnergySpentThisTurn;

    /// <summary>`PenNib`: whether the card now resolving is the tenth Attack, and doubled.</summary>
    public bool PenNibArmed;

    /// <summary>
    /// `JossPaper` banks Ethereal exhausts and folds them in at `AfterSideTurnEnd` rather
    /// than counting them as they happen — the relic says so explicitly.
    /// </summary>
    public int EtherealExhaustsThisTurn;

    /// <summary>`Vambrace` doubles the FIRST card block of a combat; this latches it.</summary>
    public bool VambraceSpent;

    /// <summary>
    /// `BeatingRemnant.DamageReceivedThisTurn` — the running total its 20-per-turn cap is
    /// measured against, reset at the owner's side-turn start.
    /// </summary>
    public int UnblockedDamageThisTurn;

    /// <summary>`RainbowRing`'s three counters and its once-a-turn latch.</summary>
    public int RainbowRingAttacks;

    /// <inheritdoc cref="RainbowRingAttacks" />
    public int RainbowRingSkills;

    /// <inheritdoc cref="RainbowRingAttacks" />
    public int RainbowRingPowers;

    /// <inheritdoc cref="RainbowRingAttacks" />
    public bool RainbowRingPaidThisTurn;

    /// <summary>
    /// `UnsettlingLamp`: whether the doubling has been used up for this combat, and
    /// whether the card CURRENTLY resolving is the one that claimed it. Two flags because
    /// the card that claims it keeps the doubling for the rest of its own resolution.
    /// </summary>
    public bool UnsettlingLampSpent;

    /// <inheritdoc cref="UnsettlingLampSpent" />
    public bool UnsettlingLampCard;

    /// <summary>
    /// `GamblingChip`: how many cards its turn-one screen has discarded so far, which is
    /// how many are drawn when the screen closes.
    /// </summary>
    public int GamblingChipDiscarded;

    /// <summary>
    /// Whether Gambling Chip's screen is owed but could not be raised because another one
    /// was already up. Only Toolbox does that, and only on turn one.
    /// </summary>
    public bool GamblingChipOwed;

    /// <summary>`BurningSticks`: whether its once-per-combat copy has been spent.</summary>
    public bool BurningSticksUsed;

    /// <summary>
    /// `BeltBuckle`: whether its Dexterity is currently applied. Tracked rather than
    /// recomputed because the grant has to be REMOVED when the belt fills again.
    /// </summary>
    public bool BeltBuckleApplied;

    /// <summary>Whether this combat is an ELITE room's, which Sling of Courage reads.</summary>
    public bool IsEliteRoom;

    /// <summary>
    /// Whether the player has taken UNBLOCKED, blockable damage this combat — Lava Lamp's
    /// condition.
    /// </summary>
    /// <remarks>
    /// Set from `DealDamageToPlayer`, which is the blockable path: block is subtracted
    /// there, so anything left is damage the player could have blocked and did not.
    /// Unblockable HP loss goes through `LoseHp` instead and correctly does not count,
    /// which is what the relic's `props.HasFlag(ValueProp.Unblockable)` check is for.
    /// </remarks>
    public bool TookUnblockedDamage;

    /// <summary>
    /// A once-per-combat enchantment on the card being played has just fired, so the copy
    /// that lands in its result pile must carry the spent flag. CardEffects takes the card
    /// by value and cannot hand a mutation back, which is the same reason
    /// PlayedCardBonusDamage exists.
    /// </summary>
    public bool PlayedCardEnchantSpent;

    /// <summary>
    /// The card just played carries an enchantment that GROWS on play — Goopy's.
    /// </summary>
    /// <remarks>
    /// Handed back through the state for the same reason <see cref="PlayedCardEnchantSpent"/>
    /// is: CardEffects takes the card by value, so the played copy has to be rebuilt by
    /// the caller that files it away.
    /// </remarks>
    public bool PlayedCardEnchantGrew;

    /// <summary>
    /// How much the card being resolved raised its OWN cost for the rest of the combat.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="PlayedCardEnchantGrew"/> and for the same reason: the
    /// effect runs while the card is out of every pile, so what it did to itself has to
    /// be carried until the card is written back. Frantic Escape's
    /// `EnergyCost.AddThisCombat(1)` is the only user so far.
    /// </remarks>
    public int PlayedCardCostBump;

    /// <summary>
    /// The run's <c>combat_energy_costs</c> stream, which Slither re-rolls its cost from
    /// every time it is drawn. Null falls back to the combat rng, as the other streams do.
    /// </summary>
    public CountingRandom? EnergyCostRng;

    // Turn tracking
    public int Turn;
    public bool PlayerTurn = true;
    public bool SkillPlayedWhileSmoggy;

    /// <summary>Cards of any kind played this turn, which SlothPower caps.</summary>
    public int CardsPlayedThisTurn;

    public int AttackCardsPlayedThisTurn;
    public int AttackOrSkillCardsPlayedThisTurn;
    public int CardPlaysThisTurn;
    public int CardsPlayedThisCombat;
    public int DrawnCardsSinceAutomationProc;

    /// <summary>
    /// Every card drawn this combat. Murder's damage is one per entry —
    /// `CalculatedDamageVar.WithMultiplier(count of CardDrawnEntry for this player)`,
    /// read off the combat HISTORY, so it counts the whole fight and not the turn.
    /// </summary>
    public int CardsDrawnThisCombat;

    /// <summary>
    /// Shivs whose play has FINISHED this turn. Phantom Blades' bonus lands on the
    /// first Shiv of the turn only — the power counts `CardPlayFinishedEntry` rows
    /// tagged Shiv and pays nothing once any exist.
    /// </summary>
    public int ShivsPlayedThisTurn;

    /// <summary>
    /// Extra card-reward rows this combat has earned. The Hunt adds one for every
    /// enemy its attack KILLS — `combatRoom.AddExtraReward(new CardReward(..., 3, ...))`.
    /// Carried here rather than pushed straight at the run, exactly as a Heist's
    /// `StolenBackGold` is, because a combat cannot reach the reward generator.
    /// </summary>
    public int ExtraCardRewards;

    /// <summary>
    /// The enemy an auto-play was given EXPLICITLY, or -1 when it has to pick its own.
    /// </summary>
    /// <remarks>
    /// `CardCmd.AutoPlay` takes a target parameter. Almost everything that auto-plays
    /// passes null and the card rolls `Rng.CombatTargets` for itself — but Knife Trap
    /// hands each Shiv it replays the target the TRAP was aimed at, so those plays must
    /// not roll at all. Rolling for them would hit the wrong creature and move the
    /// stream for everything after it.
    /// </remarks>
    public int AutoPlayTargetIndex = -1;
    public int CardsPlayedSincePanacheProc;
    public int BlockGainsThisTurn;
    public int PlayerHpLostThisTurn;
    public int CardsExhaustedThisTurn;
    public int LightningOrbsChanneledThisCombat;
    public int EtherealExhaustCount; // number of cards exhausted by Ethereal this turn (Dark Embrace)
    public int UnblockedDamageHitCount; // times player took unblocked damage this combat (TearAsunder)
    public int TargetEnemyIndex = -1; // -1 = auto (first living enemy), >=0 = specific index

    // ── What the player knows about draw-pile order ──────────────────────────
    // The pile is one ordered list, but the player is not entitled to all of it.
    // They know its composition, and they know where a card they deliberately
    // placed went -- and nothing else, until the next shuffle takes even that away.
    // These two counters carry that distinction so an observation can expose the
    // known part without leaking the rest, and so a determinization knows which
    // region it is allowed to resample. See docs/agent-interface.md.

    /// <summary>The first N cards of <see cref="DrawPile" /> are known, in order.</summary>
    public int KnownTopCount;

    /// <summary>The last M cards of <see cref="DrawPile" /> are known, in order.</summary>
    public int KnownBottomCount;

    /// <summary>Every shuffle takes the whole order away.</summary>
    public void ForgetDrawOrder()
    {
        KnownTopCount = 0;
        KnownBottomCount = 0;
    }

    /// <summary>Place a card on top of the draw pile, where the player can see it.</summary>
    public void TopDeck(CardInstance card)
    {
        DrawPile.Insert(0, card);
        KnownTopCount++;
        ClampKnownOrder();
    }

    /// <summary>Place a card on the bottom of the draw pile.</summary>
    public void BottomDeck(CardInstance card)
    {
        DrawPile.Add(card);
        KnownBottomCount++;
        ClampKnownOrder();
    }

    /// <summary>
    /// Put a card somewhere the player does not get to see -- a shuffle-in. Anything
    /// it lands inside stops being known, because an unknown card now sits in it.
    /// </summary>
    public void InsertIntoDrawPile(int index, CardInstance card)
    {
        DrawPile.Insert(index, card);
        if (index < KnownTopCount)
        {
            KnownTopCount = index;
        }

        int indexFromBottom = DrawPile.Count - 1 - index;
        if (indexFromBottom < KnownBottomCount)
        {
            KnownBottomCount = indexFromBottom;
        }

        ClampKnownOrder();
    }

    /// <summary>
    /// Take a card out of the draw pile, shrinking whichever known region held it.
    /// Drawing is this with index 0.
    /// </summary>
    public CardInstance RemoveFromDrawPileAt(int index)
    {
        int before = DrawPile.Count;
        var card = DrawPile[index];
        DrawPile.RemoveAt(index);
        if (index < KnownTopCount)
        {
            KnownTopCount--;
        }
        else if (index >= before - KnownBottomCount)
        {
            KnownBottomCount--;
        }

        ClampKnownOrder();
        return card;
    }

    /// <summary>Neither region may run past the pile, and they may not overlap.</summary>
    private void ClampKnownOrder()
    {
        KnownTopCount = Math.Clamp(KnownTopCount, 0, DrawPile.Count);
        KnownBottomCount = Math.Clamp(KnownBottomCount, 0, DrawPile.Count - KnownTopCount);
    }
}
