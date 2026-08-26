using System.Runtime.InteropServices;
using Sts2Emulator.Core;

namespace Sts2Emulator.Interop;

// Observation vector layout (OBS_SIZE ints):
//   [0]        player_hp
//   [1]        player_max_hp
//   [2]        player_block
//   [3]        energy
//   [4]        max_energy
//   [5]        draw_pile_size
//   [6]        discard_pile_size
//   [7]        exhaust_pile_size
//   [8..17]    hand slots: card_def_id (0 = empty), 5 cards × 2 ints (id, upgraded)
//   [18..23]   potion slots: potion_def_id (0 = empty), 3 slots × 2 ints (id, has_potion)
//   [34..53]   player buffs: 10 slots × 2 ints (buff_id, magnitude)
//   [54..68]   enemy 0: hp, max_hp, block, intent_type, intent_mag, 5 buff slots × 2 ints
//   [69..83]   enemy 1 (same layout)
//   [84..98]   enemy 2 (same layout)
//   [99..113]  enemy 3 (same layout)
//   [114..128] enemy 4 (same layout)
//   [129..143] enemy 5 (same layout)
//   [144..155] secondary enemy intents: 6 enemies × 2 ints (intent_type + 1, intent_mag; 0 = none)
//   [156]      player gold
//   [157..163] reserved
//
// Total: 164 ints. Enemies beyond index 5 are ignored for now.

public static class NativeExports
{
    public const int OBS_SIZE = CombatObservation.ObsSize;
    public const int MAX_HAND = 10;
    public const int MAX_ENEMIES = 6;
    public const int MAX_PLAYER_BUFFS = 10;
    public const int MAX_ENEMY_BUFFS = 5;

    // v17: observation carries an open card selection (kind, count, candidates).
    public const int NATIVE_API_VERSION = 21;
    private static ReadOnlySpan<int> StarterDeckIds =>
        [472, 472, 472, 472, 472, 131, 131, 131, 131, 30, 10001];

    private sealed class NativeCombat
    {
        public readonly int Seed;
        public readonly CombatState State = new();
        public CountingRandom Rng { get; private set; }
        public bool LastPlayerWon { get; set; }

        public NativeCombat(int seed)
        {
            Seed = seed;
            Rng = new CountingRandom(seed);
            CombatFactory.Reset(State, Rng);
        }

        /// <summary>
        /// Re-seeds every per-subsystem stream from the handle's seed and clears the
        /// win flag. Every Reset overload below opens with exactly this, so it lives in
        /// one place: six copies is six chances for a new stream to be added to five of
        /// them.
        /// </summary>
        private void SeedStreams()
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed
            );
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed
            );
            // Enemy intent rolls come off the run's "monster_ai" stream. Leaving this
            // unset silently fell back to the combat rng, so every enemy whose opening
            // move is a random branch (LeafSlimeS, SludgeSpinner, Exoskeleton...) drew
            // from the wrong generator — invisible for the many enemies whose opening
            // move is deterministic, wrong for the ones that roll.
            State.AiRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "monster_ai").RawSeed
            );
            // Which enemy a random-target effect hits comes off "combat_targets"
            // (JuggernautPower, Volley, Sword Boomerang). Same failure mode as AiRng
            // above: unset, it silently drew from the combat rng.
            State.TargetRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "combat_targets").RawSeed
            );
            // Picking WHICH card to exhaust or transform comes off
            // "combat_card_selection" (Cinder, Thrash, True Grit, Entropy).
            State.CardSelectionRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "combat_card_selection").RawSeed
            );
            // Rolling up a NEW card comes off "combat_card_generation".
            State.CardGenerationRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "combat_card_generation").RawSeed
            );
            // Alchemize rolls its potion off "combat_potion_generation".
            State.PotionGenerationRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "combat_potion_generation").RawSeed
            );
            LastPlayerWon = false;
        }

        public void Reset()
        {
            SeedStreams();
            CombatFactory.Reset(State, Rng);
        }

        public void Reset(ReadOnlySpan<int> deckIds)
        {
            SeedStreams();
            CombatFactory.Reset(State, Rng, deckIds);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId)
        {
            SeedStreams();
            CombatFactory.Reset(State, Rng, deckIds, encounterId);
        }

        // Same as above plus the run's TotalFloor, which is the missing term in the
        // per-encounter RNG seed (run seed + floor + hash of the encounter id). Slime
        // rosters and Corpse Slug starting moves are rolled from that stream, so
        // without a floor the direct combat env cannot reproduce them — see
        // Core/Run/EncounterRng.cs.
        public void ResetAtFloor(
            ReadOnlySpan<int> deckIds,
            int encounterId,
            int completedCombatRooms,
            int totalFloor,
            int ascension
        )
        {
            State.AscensionLevel = ascension;
            SeedStreams();
            int? encounterRngSeed = Sts2Emulator.Core.Run.EncounterRng.SeedFor(
                Seed,
                totalFloor,
                encounterId,
                completedCombatRooms is >= 0 and < 3
            );
            CombatFactory.Reset(
                State,
                Rng,
                deckIds,
                encounterId,
                completedCombatRooms,
                encounterRngSeed
            );
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId, int completedCombatRooms)
        {
            SeedStreams();
            CombatFactory.Reset(State, Rng, deckIds, encounterId, completedCombatRooms);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId, ReadOnlySpan<int> relicIds)
        {
            SeedStreams();
            CombatFactory.Reset(State, Rng, deckIds, encounterId, relicIds);
        }

        /// <summary>
        /// The full combat entry point: an arbitrary deck, relic set, potion set, HP and
        /// ascension against a chosen encounter. This is what a deck-conditioned value
        /// function needs — every other overload starts from the fixed starter deck at
        /// default HP with no relics, which is one point in the space the agent has to
        /// evaluate over.
        /// </summary>
        /// <remarks>
        /// A negative card id means UPGRADED, matching <c>CombatFactory.Reset</c>'s own
        /// <c>new CardInstance(Math.Abs(id), id &lt; 0)</c>. Enchantments are not
        /// expressible in a flat id list and are not carried here; a deck sampled for
        /// training therefore has none, which is honest for act 1 and a gap in acts 2-3
        /// where the ancients hand them out.
        /// </remarks>
        public void ResetArena(
            ReadOnlySpan<int> deckIds,
            ReadOnlySpan<int> enchantIds,
            ReadOnlySpan<int> enchantAmounts,
            int encounterId,
            ReadOnlySpan<int> relicIds,
            ReadOnlySpan<int> potionIds,
            int playerHp,
            int playerMaxHp,
            int playerGold,
            int ascension,
            int totalFloor,
            int completedCombatRooms
        )
        {
            // The enchantment arrays run parallel to the deck and may be empty, which
            // reads as Enchantment.None on every card. An id list alone cannot carry
            // them, and ACT 1 hands them out: Self-Help Book, Stone of All Time and
            // Symbiote sit in both act-1 event pools, Sapphire Seed and Wood Carvings in
            // Underdocks', and six Shop-rarity relics grant one apiece.
            var deck = new CardInstance[deckIds.Length];
            for (int i = 0; i < deckIds.Length; i++)
            {
                int cardId = deckIds[i];
                deck[i] = new CardInstance(
                    Math.Abs(cardId),
                    cardId < 0,
                    Enchantment: i < enchantIds.Length
                        ? (Enchantment)enchantIds[i]
                        : Enchantment.None,
                    EnchantAmount: i < enchantAmounts.Length ? enchantAmounts[i] : 0
                );
            }

            State.AscensionLevel = ascension;
            SeedStreams();
            // -1 means "no forced encounter": the factory rolls the seeded first combat.
            // It has to become null rather than travel as -1, which would be looked up as
            // an encounter id and found missing.
            int? encounter = encounterId >= 0 ? encounterId : null;
            int? encounterRngSeed = encounter is { } id
                ? Sts2Emulator.Core.Run.EncounterRng.SeedFor(
                    Seed,
                    totalFloor,
                    id,
                    completedCombatRooms is >= 0 and < 3
                )
                : null;
            CombatFactory.Reset(
                State,
                Rng,
                (ReadOnlySpan<CardInstance>)deck,
                encounter,
                relicIds,
                playerHp,
                playerMaxHp,
                potionIds,
                playerGold,
                deckPreShuffled: false,
                shuffleRng: null,
                encounterRngSeed: encounterRngSeed,
                nicheSkipCount: 0,
                aiRng: null,
                completedCombatRoomsBeforeCurrent: completedCombatRooms
            );
        }
    }

    private static readonly NativeCombat?[] _pool = new NativeCombat?[256];

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsSize")]
    public static int Sts2_ObsSize() => OBS_SIZE;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_MaxEnemies")]
    public static int Sts2_MaxEnemies() => MAX_ENEMIES;

    // The combat observation's layout, so the Python side can read it rather than
    // restate it. scripts/trace.py used to carry the offsets as magic numbers (8, 34, 54,
    // 15, 144) and went silently wrong the moment a card slot grew from two fields to
    // four -- it decoded zero enemies and 77 live-fixture tests failed at once.
    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsCardSlotSize")]
    public static int Sts2_ObsCardSlotSize() => CombatObservation.CardSlotSize;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsHandOffset")]
    public static int Sts2_ObsHandOffset() => CombatObservation.HandOffset;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsMaxHand")]
    public static int Sts2_ObsMaxHand() => CombatObservation.MaxHand;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsPlayerBuffOffset")]
    public static int Sts2_ObsPlayerBuffOffset() => CombatObservation.PlayerBuffOffset;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsMaxPlayerBuffs")]
    public static int Sts2_ObsMaxPlayerBuffs() => CombatObservation.MaxPlayerBuffs;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsMaxEnemyBuffs")]
    public static int Sts2_ObsMaxEnemyBuffs() => CombatObservation.MaxEnemyBuffs;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsEnemyOffset")]
    public static int Sts2_ObsEnemyOffset() => CombatObservation.EnemyOffset;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsEnemySlotSize")]
    public static int Sts2_ObsEnemySlotSize() => CombatObservation.EnemySlotSize;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsSecondaryIntentOffset")]
    public static int Sts2_ObsSecondaryIntentOffset() => CombatObservation.SecondaryIntentOffset;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_NativeApiVersion")]
    public static int Sts2_NativeApiVersion() => NATIVE_API_VERSION;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_Create")]
    public static int Sts2_Create(int seed)
    {
        var combat = new NativeCombat(seed);
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] is null)
            {
                _pool[i] = combat;
                return i;
            }
        }
        return -1; // pool exhausted
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_Reset")]
    public static unsafe void Sts2_Reset(int handle, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset();
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetEncounter")]
    public static unsafe void Sts2_ResetEncounter(int handle, int encounterId, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(StarterDeckIds, encounterId);
        WriteObs(combat.State, obsBuf);
    }

    // Like Sts2_ResetEncounter but with the weak-combat context:
    // completedCombatRooms in [0,3) selects weak encounter variants (fewer/weaker
    // enemies on early floors); -1 keeps the normal variant.
    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetEncounterWeak")]
    public static unsafe void Sts2_ResetEncounterWeak(
        int handle,
        int encounterId,
        int completedCombatRooms,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        combat.Reset(StarterDeckIds, encounterId, completedCombatRooms);
        WriteObs(combat.State, obsBuf);
    }

    // Like Sts2_ResetEncounterWeak but also passing the run's TotalFloor, which is what
    // seeds the per-encounter RNG (EncounterModel.Rng). Required for any encounter that
    // rolls its own composition — Slimes rosters, Corpse Slug starting moves.
    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetEncounterAtFloor")]
    public static unsafe void Sts2_ResetEncounterAtFloor(
        int handle,
        int encounterId,
        int completedCombatRooms,
        int totalFloor,
        int ascension,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        combat.ResetAtFloor(
            StarterDeckIds,
            encounterId,
            completedCombatRooms,
            totalFloor,
            ascension
        );
        WriteObs(combat.State, obsBuf);
    }

    // Sts2_ResetEncounterAtFloor with the deck spelled out, for a capture that stacks
    // it. Some states can only be reached by winning — Phrog Parasite's Wrigglers spawn
    // when it dies, Terror Eel's second phase when an unblocked hit drops it to its
    // threshold — and a starter deck cannot get there before the player is dead. The
    // live side adds the same cards with debug_add_card, so both decks must be built the
    // same way: appended in the same order, then shuffled.
    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetEncounterAtFloorWithExtraCards")]
    public static unsafe void Sts2_ResetEncounterAtFloorWithExtraCards(
        int handle,
        int* extraCardIds,
        int extraLen,
        int encounterId,
        int completedCombatRooms,
        int totalFloor,
        int ascension,
        int* obsBuf
    )
    {
        // Appended to the starter deck rather than replacing it, and appended HERE so
        // the starter deck keeps one definition: the live side adds the same cards to
        // the same run deck, and both sides have to shuffle the same list.
        var deck = new int[StarterDeckIds.Length + extraLen];
        StarterDeckIds.CopyTo(deck);
        new ReadOnlySpan<int>(extraCardIds, extraLen).CopyTo(deck.AsSpan(StarterDeckIds.Length));

        var combat = _pool[handle]!;
        combat.ResetAtFloor(deck, encounterId, completedCombatRooms, totalFloor, ascension);
        WriteObs(combat.State, obsBuf);
    }

    // The mod's debug_add_card, mirrored: put a card on top of the hand mid-combat.
    // A differential capture needs this to reach states the starter deck cannot — the
    // Phrog Parasite's Wrigglers only spawn when it dies, and Terror Eel's second phase
    // only when an unblocked hit drops it to its threshold. Adding to the HAND rather
    // than the deck keeps it deterministic: no shuffle is involved, so both sides can
    // place the same card in the same slot without having to agree on a reshuffle.
    [UnmanagedCallersOnly(EntryPoint = "Sts2_DebugAddCardToHand")]
    public static unsafe void Sts2_DebugAddCardToHand(
        int handle,
        int cardId,
        int upgraded,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        // CardPilePosition.Top, which for the hand is index 0.
        combat.State.Hand.Insert(0, new CardInstance(cardId, upgraded != 0));
        WriteObs(combat.State, obsBuf);
    }

    /// <summary>
    /// Raise the player's maximum HP and heal by the same amount, mid-combat.
    /// </summary>
    /// <remarks>
    /// The mod's `debug_gain_max_hp` for a FIGHT rather than a run. A boss capture is
    /// only worth as much as the number of turns it survives: the Kaiser Crab kills a
    /// starter deck on turn four, two moves short of walking either half's table, and a
    /// capture that stops short of a move can never put that move under test.
    /// </remarks>
    /// <summary>
    /// The kind of card selection the combat is waiting on, or 0 for none.
    /// </summary>
    /// <remarks>
    /// A differential capture has to answer the emulator's screen at the same point the
    /// live game answers its own, and the two do not open at the same INSTANT: the live
    /// game acts when `end_turn` is posted, the emulator when `step` is called. Counting
    /// the live screens and replaying that many steps looks equivalent and is not — a
    /// poll can see one screen twice, and the extra step lands with nothing open, where
    /// it means "play card 0". Asking is the only thing that cannot double-count.
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "Sts2_PendingSelectionKind")]
    public static int Sts2_PendingSelectionKind(int handle) =>
        (int)(_pool[handle]!.State.PendingSelection?.Kind ?? CardSelectionKind.None);

    [UnmanagedCallersOnly(EntryPoint = "Sts2_DebugGainMaxHp")]
    public static unsafe void Sts2_DebugGainMaxHp(int handle, int amount, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.State.PlayerMaxHp += amount;
        combat.State.PlayerHp += amount;
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetWithDeck")]
    public static unsafe void Sts2_ResetWithDeck(int handle, int* deckIds, int deckLen, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(new ReadOnlySpan<int>(deckIds, deckLen));
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetWithDeckAndEncounter")]
    public static unsafe void Sts2_ResetWithDeckAndEncounter(
        int handle,
        int* deckIds,
        int deckLen,
        int encounterId,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        combat.Reset(new ReadOnlySpan<int>(deckIds, deckLen), encounterId);
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetWithDeckEncounterAndRelics")]
    public static unsafe void Sts2_ResetWithDeckEncounterAndRelics(
        int handle,
        int* deckIds,
        int deckLen,
        int encounterId,
        int* relicIds,
        int relicLen,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        combat.Reset(
            new ReadOnlySpan<int>(deckIds, deckLen),
            encounterId,
            new ReadOnlySpan<int>(relicIds, relicLen)
        );
        WriteObs(combat.State, obsBuf);
    }

    /// <summary>
    /// Start a combat from an arbitrary run position: any deck, relics, potions, HP and
    /// ascension against any encounter. The deck-conditioned value function trains on
    /// this — nothing else in the native surface can express a deck that is not the
    /// starter deck plus appended cards at full HP with no relics.
    /// </summary>
    /// <param name="encounterId">-1 rolls the seeded first-combat encounter instead.</param>
    [UnmanagedCallersOnly(EntryPoint = "Sts2_ResetArena")]
    public static unsafe void Sts2_ResetArena(
        int handle,
        int* deckIds,
        int deckLen,
        int* enchantIds,
        int enchantLen,
        int* enchantAmounts,
        int enchantAmountLen,
        int encounterId,
        int* relicIds,
        int relicLen,
        int* potionIds,
        int potionLen,
        int playerHp,
        int playerMaxHp,
        int playerGold,
        int ascension,
        int totalFloor,
        int completedCombatRooms,
        int* obsBuf
    )
    {
        var combat = _pool[handle]!;
        combat.ResetArena(
            new ReadOnlySpan<int>(deckIds, deckLen),
            new ReadOnlySpan<int>(enchantIds, enchantLen),
            new ReadOnlySpan<int>(enchantAmounts, enchantAmountLen),
            encounterId,
            new ReadOnlySpan<int>(relicIds, relicLen),
            new ReadOnlySpan<int>(potionIds, potionLen),
            playerHp,
            playerMaxHp,
            playerGold,
            ascension,
            totalFloor,
            completedCombatRooms
        );
        WriteObs(combat.State, obsBuf);
    }

    /// <summary>
    /// Whether <paramref name="enchantment"/> may be applied to <paramref name="cardId"/>,
    /// i.e. <c>Card.CanEnchant</c>.
    /// </summary>
    /// <remarks>
    /// Exposed so a position sampler asks the engine rather than restating the rule. The
    /// arena reset applies whatever enchantment it is handed WITHOUT filtering — it has
    /// to, since it rebuilds a position the run already produced — so nothing downstream
    /// catches an illegal pairing, and a sampler that re-implements the switch drifts
    /// silently the moment an enchantment is added.
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "Sts2_CanEnchant")]
    public static int Sts2_CanEnchant(int cardId, int enchantment) =>
        Enchantments.CanEnchant(
            new CardInstance(Math.Abs(cardId), cardId < 0),
            (Enchantment)enchantment
        )
            ? 1
            : 0;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_Step")]
    public static unsafe int Sts2_Step(int handle, int action, int* obsBuf, float* rewardOut)
    {
        var combat = _pool[handle]!;
        var result = CombatEngine.Step(combat.State, action, combat.Rng);
        combat.LastPlayerWon = result.Terminal && result.PlayerWon;
        WriteObs(combat.State, obsBuf);
        *rewardOut = result.Reward;
        return result.Terminal ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_StepTargeted")]
    public static unsafe int Sts2_StepTargeted(
        int handle,
        int action,
        int targetEnemyIdx,
        int* obsBuf,
        float* rewardOut
    )
    {
        var combat = _pool[handle]!;
        var result = CombatEngine.Step(combat.State, action, combat.Rng, targetEnemyIdx);
        combat.LastPlayerWon = result.Terminal && result.PlayerWon;
        WriteObs(combat.State, obsBuf);
        *rewardOut = result.Reward;
        return result.Terminal ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_PlayerWon")]
    public static int Sts2_PlayerWon(int handle)
    {
        return _pool[handle]!.LastPlayerWon ? 1 : 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_EncounterId")]
    public static int Sts2_EncounterId(int handle)
    {
        return _pool[handle]!.State.EncounterId;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ActionCount")]
    public static int Sts2_ActionCount(int handle)
    {
        return CombatEngine.ValidActions(_pool[handle]!.State).Length;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ValidActions")]
    public static unsafe void Sts2_ValidActions(int handle, int* maskBuf, int maxActions)
    {
        var valid = CombatEngine.ValidActions(_pool[handle]!.State);
        for (int i = 0; i < maxActions; i++)
        {
            maskBuf[i] = 0;
        }

        foreach (int a in valid)
        {
            if (a < maxActions)
            {
                maskBuf[a] = 1;
            }
        }
    }

    /// <summary>
    /// Dump one combat pile in true order (index 0 = top of pile), for differential
    /// testing against the live game. The observation vector only carries pile
    /// *counts*, and the STS2MCP mod sorts its `draw_pile` for display, so neither
    /// side exposed an ordered readout before this.
    /// </summary>
    /// <param name="pileId">0 = draw, 1 = hand, 2 = discard, 3 = exhaust.</param>
    /// <param name="buf">Receives 2 ints per card: def id, then upgraded (0/1).</param>
    /// <param name="maxCards">Capacity of <paramref name="buf"/> in cards, not ints.</param>
    /// <returns>
    /// The pile's true card count, which may exceed <paramref name="maxCards"/> — only
    /// the first <paramref name="maxCards"/> are written, so callers can size and retry.
    /// Returns -1 for an unknown <paramref name="pileId"/>.
    /// </returns>
    [UnmanagedCallersOnly(EntryPoint = "Sts2_GetPile")]
    public static unsafe int Sts2_GetPile(int handle, int pileId, int* buf, int maxCards)
    {
        var state = _pool[handle]!.State;
        var pile = pileId switch
        {
            0 => state.DrawPile,
            1 => state.Hand,
            2 => state.DiscardPile,
            3 => state.ExhaustPile,
            _ => null,
        };

        if (pile is null)
        {
            return -1;
        }

        int written = Math.Min(pile.Count, maxCards);
        for (int i = 0; i < written; i++)
        {
            buf[i * 2] = pile[i].DefId;
            buf[i * 2 + 1] = pile[i].Upgraded ? 1 : 0;
        }

        return pile.Count;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2_Destroy")]
    public static void Sts2_Destroy(int handle)
    {
        _pool[handle] = null;
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_NativeApiVersion")]
    public static int Sts2Run_NativeApiVersion() => RunNativeExports.Sts2Run_NativeApiVersion();

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_ObsSize")]
    public static int Sts2Run_ObsSize() => RunNativeExports.Sts2Run_ObsSize();

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_MaxActions")]
    public static int Sts2Run_MaxActions() => RunNativeExports.Sts2Run_MaxActions();

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_InfoSize")]
    public static int Sts2Run_InfoSize() => RunNativeExports.Sts2Run_InfoSize();

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_ObsLayout")]
    public static unsafe int Sts2Run_ObsLayout(int* buf, int len) =>
        RunNativeExports.Sts2Run_ObsLayout(buf, len);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_Create")]
    public static int Sts2Run_Create() => RunNativeExports.Sts2Run_Create();

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_Reset")]
    public static unsafe int Sts2Run_Reset(int handle, byte* seedPtr, int seedLen, int* obsBuf)
    {
        return RunNativeExports.Sts2Run_Reset(handle, seedPtr, seedLen, obsBuf);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_Step")]
    public static unsafe int Sts2Run_Step(
        int handle,
        int action,
        int targetEnemyIndex,
        int* obsBuf,
        float* rewardOut,
        int* terminalOut,
        int* truncatedOut
    )
    {
        return RunNativeExports.Sts2Run_Step(
            handle,
            action,
            targetEnemyIndex,
            obsBuf,
            rewardOut,
            terminalOut,
            truncatedOut
        );
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_StartCombat")]
    public static unsafe int Sts2Run_StartCombat(
        int handle,
        int* deckIds,
        int deckLen,
        int encounterId,
        int* relicIds,
        int relicLen,
        int playerHp,
        int playerMaxHp,
        int* potionIds,
        int potionLen,
        int playerGold,
        int completedCombatRoomsBeforeCurrent,
        int* obsBuf
    )
    {
        return RunNativeExports.Sts2Run_StartCombat(
            handle,
            deckIds,
            deckLen,
            encounterId,
            relicIds,
            relicLen,
            playerHp,
            playerMaxHp,
            potionIds,
            potionLen,
            playerGold,
            completedCombatRoomsBeforeCurrent,
            obsBuf
        );
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_ActionMask")]
    public static unsafe int Sts2Run_ActionMask(int handle, int* maskBuf, int maskLen)
    {
        return RunNativeExports.Sts2Run_ActionMask(handle, maskBuf, maskLen);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_GetInfo")]
    public static unsafe int Sts2Run_GetInfo(int handle, int* infoBuf, int infoLen)
    {
        return RunNativeExports.Sts2Run_GetInfo(handle, infoBuf, infoLen);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_GetStateList")]
    public static unsafe int Sts2Run_GetStateList(int handle, int listId, int* outBuf, int outLen)
    {
        return RunNativeExports.Sts2Run_GetStateList(handle, listId, outBuf, outLen);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_GetPhase")]
    public static int Sts2Run_GetPhase(int handle) => RunNativeExports.Sts2Run_GetPhase(handle);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_DebugSetHp")]
    public static unsafe int Sts2Run_DebugSetHp(int handle, int hp, int maxHp, int* obsBuf) =>
        RunNativeExports.Sts2Run_DebugSetHp(handle, hp, maxHp, obsBuf);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_DebugGainMaxHp")]
    public static unsafe int Sts2Run_DebugGainMaxHp(int handle, int amount, int* obsBuf) =>
        RunNativeExports.Sts2Run_DebugGainMaxHp(handle, amount, obsBuf);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_DebugEnterNextAct")]
    public static unsafe int Sts2Run_DebugEnterNextAct(int handle, int* obsBuf) =>
        RunNativeExports.Sts2Run_DebugEnterNextAct(handle, obsBuf);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_DebugUpgradeDeck")]
    public static unsafe int Sts2Run_DebugUpgradeDeck(int handle, int* obsBuf) =>
        RunNativeExports.Sts2Run_DebugUpgradeDeck(handle, obsBuf);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_PlayerWon")]
    public static int Sts2Run_PlayerWon(int handle) => RunNativeExports.Sts2Run_PlayerWon(handle);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_EncounterId")]
    public static int Sts2Run_EncounterId(int handle) =>
        RunNativeExports.Sts2Run_EncounterId(handle);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_GetShuffleRngCallCount")]
    public static int Sts2Run_GetShuffleRngCallCount(int handle)
    {
        return RunNativeExports.Sts2Run_GetShuffleRngCallCount(handle);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_GetNicheRngCallCount")]
    public static int Sts2Run_GetNicheRngCallCount(int handle)
    {
        return RunNativeExports.Sts2Run_GetNicheRngCallCount(handle);
    }

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_Clone")]
    public static unsafe int Sts2Run_Clone(
        int handle,
        int resampleHidden,
        int resampleSeed,
        int* obsBuf
    ) => RunNativeExports.Sts2Run_Clone(handle, resampleHidden, resampleSeed, obsBuf);

    [UnmanagedCallersOnly(EntryPoint = "Sts2Run_Destroy")]
    public static void Sts2Run_Destroy(int handle)
    {
        RunNativeExports.Sts2Run_Destroy(handle);
    }

    // ── observation serialisation ─────────────────────────────────────────────

    private static unsafe void WriteObs(CombatState s, int* o)
    {
        CombatObservation.Write(s, new Span<int>(o, OBS_SIZE));
    }
}
