using System.Runtime.InteropServices;
using System.Text;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;

namespace Sts2Emulator.Interop;

public static class RunNativeExports
{
    public const int RUN_NATIVE_API_VERSION = 8;
    private static readonly RunEngine?[] _pool = new RunEngine?[256];

    public static int Sts2Run_NativeApiVersion() => RUN_NATIVE_API_VERSION;

    public static int Sts2Run_ObsSize() => RunConstants.RunObsSize;

    public static int Sts2Run_MaxActions() => RunConstants.MaxActions;

    public static int Sts2Run_InfoSize() => RunConstants.RunInfoSize;

    public static int Sts2Run_Create()
    {
        var run = new RunEngine();
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] is null)
            {
                _pool[i] = run;
                return i;
            }
        }

        return -1;
    }

    public static unsafe int Sts2Run_Reset(int handle, byte* seedPtr, int seedLen, int* obsBuf)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        string seed = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(seedPtr, seedLen));
        run.Reset(seed);
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return 0;
    }

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
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        int status = run.Step(
            action,
            targetEnemyIndex,
            out float reward,
            out bool terminal,
            out bool truncated
        );
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        *rewardOut = reward;
        *terminalOut = terminal ? 1 : 0;
        *truncatedOut = truncated ? 1 : 0;
        return status;
    }

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
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        int status = run.StartCombat(
            new ReadOnlySpan<int>(deckIds, deckLen),
            encounterId,
            new ReadOnlySpan<int>(relicIds, relicLen),
            playerHp,
            playerMaxHp,
            new ReadOnlySpan<int>(potionIds, potionLen),
            playerGold,
            completedCombatRoomsBeforeCurrent
        );
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return status;
    }

    public static unsafe int Sts2Run_ActionMask(int handle, int* maskBuf, int maskLen)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        run.WriteActionMask(new Span<int>(maskBuf, maskLen));
        return 0;
    }

    public static unsafe int Sts2Run_GetInfo(int handle, int* infoBuf, int infoLen)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        run.WriteInfo(new Span<int>(infoBuf, infoLen));
        return 0;
    }

    public static unsafe int Sts2Run_GetStateList(int handle, int listId, int* outBuf, int outLen)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        if (outLen < 0)
        {
            return -2;
        }

        Span<int> output = new(outBuf, outLen);
        return listId switch
        {
            0 => WriteCardList(run.State.Deck, output),
            1 => WriteRelicList(run.State.Relics, output),
            2 => WriteIntArray(run.State.PotionSlots, output),
            3 => WriteIntArray(run.State.NeowOptions, output),
            4 => WriteIntArray(run.State.ShopCosts, output),
            5 => WriteBoolArray(run.State.RewardUpgraded, output),
            6 => WriteRewardList(run.State, output),
            7 => WriteMapOptionCoords(run.State, output),
            8 => WriteIntArray(run.State.ShopCards, output),
            9 => WriteIntArray(run.State.ShopRelics, output),
            10 => WriteIntArray(run.State.ShopPotions, output),
            // 11-14: run-generation data, for differential testing against a live
            // save's acts[].rooms (see scripts/verify_run_generation.py).
            11 => WriteIntArray(run.State.NormalEncounterSequence, output),
            12 => WriteIntArray(run.State.EliteEncounterSequence, output),
            13 => WriteIntArray(run.State.EventSequence, output),
            14 => WriteIntArray(
                [run.State.Act, run.State.BossEncounterId, run.State.MapNodes.Count],
                output
            ),
            // 15: the whole map as (col, row, nodeType) triples, ordered so the
            // readout is stable. Lets the differential test compare map *structure*
            // against a live save's saved_map.points, not just a node count.
            15 => WriteIntArray(
                [
                    .. run
                        .State.MapNodes.Values.OrderBy(n => n.Row)
                        .ThenBy(n => n.Col)
                        .SelectMany(n => new[] { n.Col, n.Row, n.NodeType }),
                ],
                output
            ),
            _ => -3,
        };
    }

    public static int Sts2Run_GetPhase(int handle)
    {
        return TryGet(handle, out var run) ? (int)run.State.Phase : -1;
    }

    public static int Sts2Run_PlayerWon(int handle)
    {
        return TryGet(handle, out var run) && run.State.LastPlayerWon ? 1 : 0;
    }

    public static int Sts2Run_EncounterId(int handle)
    {
        return TryGet(handle, out var run) ? run.ActiveEncounterId : -1;
    }

    public static int Sts2Run_GetShuffleRngCallCount(int handle)
    {
        return TryGet(handle, out var run) ? run.ActiveShuffleRngCallCount : 0;
    }

    public static int Sts2Run_GetNicheRngCallCount(int handle)
    {
        return TryGet(handle, out var run) ? run.ActiveNicheRngCallCount : 0;
    }

    public static void Sts2Run_Destroy(int handle)
    {
        if ((uint)handle < _pool.Length)
        {
            _pool[handle] = null;
        }
    }

    private static int WriteCardList(IReadOnlyList<CardInstance> cards, Span<int> output)
    {
        int count = Math.Min(cards.Count, output.Length);
        for (int i = 0; i < count; i++)
        {
            output[i] = cards[i].Upgraded ? -cards[i].DefId : cards[i].DefId;
        }

        return cards.Count;
    }

    private static int WriteRelicList(IReadOnlyList<RelicInstance> relics, Span<int> output)
    {
        int count = Math.Min(relics.Count, output.Length);
        for (int i = 0; i < count; i++)
        {
            output[i] = relics[i].DefId;
        }

        return relics.Count;
    }

    private static int WriteRewardList(RunState state, Span<int> output)
    {
        Span<int> rewards = stackalloc int[4];
        rewards[0] = state.RewardGold;
        rewards[1] = state.RewardPotion;
        rewards[2] = state.RelicReward;
        rewards[3] = state.RewardCardPending ? 1 : 0;
        int count = Math.Min(rewards.Length, output.Length);
        rewards[..count].CopyTo(output);
        return rewards.Length;
    }

    private static int WriteMapOptionCoords(RunState state, Span<int> output)
    {
        int required = RunConstants.MapChoices * 2;
        int count = Math.Min(required, output.Length);
        for (int i = 0; i < count / 2; i++)
        {
            var coord = state.MapOptionCoords[i];
            output[i * 2] = coord?.Col ?? -1;
            output[i * 2 + 1] = coord?.Row ?? -1;
        }
        return required;
    }

    private static int WriteIntArray(int[] values, Span<int> output)
    {
        int count = Math.Min(values.Length, output.Length);
        values.AsSpan(0, count).CopyTo(output);
        return values.Length;
    }

    private static int WriteBoolArray(bool[] values, Span<int> output)
    {
        int count = Math.Min(values.Length, output.Length);
        for (int i = 0; i < count; i++)
        {
            output[i] = values[i] ? 1 : 0;
        }

        return values.Length;
    }

    private static bool TryGet(
        int handle,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RunEngine? run
    )
    {
        if ((uint)handle < _pool.Length && _pool[handle] is { } existing)
        {
            run = existing;
            return true;
        }

        run = null;
        return false;
    }
}
