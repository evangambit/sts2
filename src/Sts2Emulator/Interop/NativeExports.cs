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
    public const int OBS_SIZE = 164;
    public const int MAX_HAND = 10;
    public const int MAX_ENEMIES = 6;
    public const int MAX_PLAYER_BUFFS = 10;
    public const int MAX_ENEMY_BUFFS = 5;
    public const int NATIVE_API_VERSION = 11;
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

        public void Reset()
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed);
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng);
        }

        public void Reset(ReadOnlySpan<int> deckIds)
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed);
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId)
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed);
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds, encounterId);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId, int completedCombatRooms)
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed);
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds, encounterId, completedCombatRooms);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId, ReadOnlySpan<int> relicIds)
        {
            Rng = new CountingRandom(Seed);
            State.NicheHpRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "niche").RawSeed);
            State.ShuffleRng = new CountingRandom(
                new Sts2Emulator.Core.Rng.GameRng((uint)Seed, "shuffle").RawSeed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds, encounterId, relicIds);
        }
    }

    private static readonly NativeCombat?[] _pool = new NativeCombat?[256];

    [UnmanagedCallersOnly(EntryPoint = "Sts2_ObsSize")]
    public static int Sts2_ObsSize() => OBS_SIZE;

    [UnmanagedCallersOnly(EntryPoint = "Sts2_MaxEnemies")]
    public static int Sts2_MaxEnemies() => MAX_ENEMIES;

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
        int handle, int encounterId, int completedCombatRooms, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(StarterDeckIds, encounterId, completedCombatRooms);
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
