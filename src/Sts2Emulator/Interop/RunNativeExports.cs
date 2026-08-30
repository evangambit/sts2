using System.Runtime.InteropServices;
using System.Text;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;

namespace Sts2Emulator.Interop;

public static class RunNativeExports
{
    // v13: the observation carries the shop's whole board, priced.
    // v15: the map offers a whole row, not four children -- Winged Boots' free travel.
    // v16: state list 17 reports an open card-offer grid.
    // v17: state list 19 names the enemies in the active combat, and 20 says what an open
    // card-select screen is FOR.
    public const int RUN_NATIVE_API_VERSION = 17;
    private static readonly RunEngine?[] _pool = new RunEngine?[256];

    public static int Sts2Run_NativeApiVersion() => RUN_NATIVE_API_VERSION;

    public static int Sts2Run_ObsSize() => RunConstants.RunObsSize;

    public static int Sts2Run_MaxActions() => RunConstants.MaxActions;

    public static int Sts2Run_InfoSize() => RunConstants.RunInfoSize;

    /// <summary>How many numbers <see cref="Sts2Run_ObsLayout"/> writes.</summary>
    public const int RUN_OBS_LAYOUT_SIZE = 13;

    /// <summary>
    /// Where the run observation's variable-length blocks sit, so a consumer does not have
    /// to hard-code offsets that move whenever a block grows:
    ///
    /// <c>[scalars, deck offset, deck slots, ints per card, relic offset, relic slots,
    /// ints per relic, shop offset, shop slots, ints per shop slot, map choices, map node
    /// type offset, map choice offset]</c>
    ///
    /// Offsets are relative to the start of the run block, which itself begins at the
    /// combat observation's own size.
    /// </summary>
    /// <returns>How many numbers were written, or -1 if the buffer is too small.</returns>
    public static unsafe int Sts2Run_ObsLayout(int* buf, int len)
    {
        if (len < RUN_OBS_LAYOUT_SIZE)
        {
            return -1;
        }

        var layout = new Span<int>(buf, len);
        layout[0] = RunConstants.RunScalarObsSize;
        layout[1] = RunConstants.DeckObsOffset;
        layout[2] = RunConstants.MaxObservedDeck;
        layout[3] = RunConstants.DeckSlotSize;
        layout[4] = RunConstants.RelicObsOffset;
        layout[5] = RunConstants.MaxObservedRelics;
        layout[6] = RunConstants.RelicSlotSize;
        layout[7] = RunConstants.ShopObsOffset;
        layout[8] = RunConstants.ShopSlots;
        layout[9] = RunConstants.ShopSlotSize;
        layout[10] = RunConstants.MapChoices;
        layout[11] = RunConstants.MapNodeTypeObsOffset;
        layout[12] = RunConstants.MapChoiceObsOffset;
        return RUN_OBS_LAYOUT_SIZE;
    }

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

    /// <summary>
    /// Fork a run into a new handle. <paramref name="resampleHidden" /> non-zero
    /// resamples everything the agent has not been shown -- future rewards, shop stock,
    /// encounter composition, and the unseen part of the draw pile -- off
    /// <paramref name="resampleSeed" />. A clone taken without it is a faithful copy,
    /// which for a tree search is an oracle. See docs/agent-interface.md.
    /// </summary>
    /// <returns>The new handle, or -1 if the source is unknown or the pool is full.</returns>
    public static unsafe int Sts2Run_Clone(
        int handle,
        int resampleHidden,
        int resampleSeed,
        int* obsBuf
    )
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        var copy = run.Clone(resampleHidden != 0 ? resampleSeed : null);
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] is null)
            {
                _pool[i] = copy;
                copy.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
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
            // 16: every edge as a (col, row, childCol, childRow) quadruple. Node
            // positions alone do not pin a map — the same 62 dots can be wired
            // differently — and the live save records each point's `children`, so the
            // differential test can check connectivity rather than assume it.
            16 => WriteIntArray(
                [
                    .. run
                        .State.MapNodes.Values.OrderBy(n => n.Row)
                        .ThenBy(n => n.Col)
                        .SelectMany(n =>
                            n.Children.OrderBy(c => c.Row)
                                .ThenBy(c => c.Col)
                                .SelectMany(c => new[] { n.Col, n.Row, c.Col, c.Row })
                        ),
                ],
                output
            ),
            // 17: the cards on an OFFER grid, if one is open. A card-select phase is two
            // different screens wearing one phase -- a grid of cards the run has rolled
            // and is offering (CardSelectCmd.FromChooseACardScreen, which the game calls
            // `card_select` and resolves on the click) versus a selection over the deck
            // (which toggles and needs a confirm). A replay has to tell them apart to know
            // whether an answer needs a confirm after it, and guessing from a screen
            // message is not the same as asking the run.
            17 => WriteIntArray(run.State.PendingOfferCards, output),
            // 18: Scroll Boxes' bundles, flat -- bundle 0's three cards then bundle 1's.
            // The screen offers a WHOLE bundle, so an agent needs all six to choose
            // between them, and a replay needs them to map the live screen's indexes.
            18 => WriteIntArray(run.State.BundleOffer, output),
            // 19: the DefId of every enemy in the active combat, in the engine's own enemy
            // order -- the DEAD included, because the enemy list keeps its dead where the
            // game removes them, and slot i here has to name the same creature as enemy
            // slot i of the observation. The observation carries an enemy's hp, block,
            // intent and buffs but never says WHICH enemy it is, so nothing outside the
            // engine could put a name to what it was fighting.
            19 => WriteIntArray(
                run.State.ActiveCombat is { } combat
                    ? [.. combat.Enemies.Select(enemy => enemy.DefId)]
                    : [],
                output
            ),
            // 20: what an open card-select screen is FOR, as
            // (DeckSelection, its argument, whether it is a rest-site upgrade). The
            // card-select phase offers a list of the deck and says nothing about what
            // answering it does -- removal, upgrade, transform and Dolly's Mirror are one
            // screen and four different decisions, and the difference is not recoverable
            // from the cards on offer.
            20 => WriteIntArray(
                [
                    (int)run.State.PendingSelectionKind,
                    run.State.PendingSelectionArg,
                    run.State.PendingRestUpgrade ? 1 : 0,
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

    /// <summary>
    /// Hand a run extra HP, for soaking only. A random or greedy policy dies around
    /// floor six and never exercises the back half of the act, so scripts/soak_act_one.py
    /// uses this to reach the boss. It is a DEBUG hook and not a game rule: anything a
    /// boosted soak turns up has to be reproduced on an untouched run before it counts.
    /// </summary>
    public static unsafe int Sts2Run_DebugSetHp(int handle, int hp, int maxHp, int* obsBuf)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        run.State.PlayerMaxHp = Math.Max(1, maxHp);
        run.State.PlayerHp = Math.Clamp(hp, 1, run.State.PlayerMaxHp);
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return 0;
    }

    /// <summary>
    /// <c>CreatureCmd.GainMaxHp</c>: raise the maximum and heal by the same amount.
    /// </summary>
    /// <remarks>
    /// The mirror of the mod's <c>debug_gain_max_hp</c>, and it exists so a BUFFED live
    /// capture can be replayed. Not the same as <c>DebugSetHp</c>, which sets absolutes:
    /// the game's command heals as it raises, so a replay that only moved the maximum
    /// would diverge on HP one step after the buff. It routes through the same
    /// <c>RunNonCombatEffects.GainMaxHp</c> that every relic uses, which is the version
    /// captures have already checked.
    /// </remarks>
    public static unsafe int Sts2Run_DebugGainMaxHp(int handle, int amount, int* obsBuf)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        Core.Run.RunNonCombatEffects.GainMaxHp(run.State, amount);
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return 0;
    }

    /// <summary>
    /// <c>RunManager.EnterNextAct</c> on demand — the mod's <c>debug_enter_next_act</c>.
    /// </summary>
    /// <remarks>
    /// Not a shortcut around the rules: it calls the same <c>RunEngine.EnterNextAct</c>
    /// the boss reward does. What it skips is having to WIN act 1 first, which is several
    /// minutes of buffed run per act-2 data point and a boss fight that can lose.
    /// Returns 1 when an act was entered and 0 when the run was already in its last.
    /// </remarks>
    public static unsafe int Sts2Run_DebugEnterNextAct(int handle, int* obsBuf)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        bool entered = run.EnterNextAct();
        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return entered ? 1 : 0;
    }

    /// <summary>Upgrade every upgradable card in the deck. Debug hook, as above.</summary>
    /// <remarks>
    /// Every mutating export refreshes the observation before returning, and these three
    /// did not — which is invisible for a soak, because it steps immediately afterwards
    /// and the next step rewrites it anyway, and fatal for a replay, whose snapshot for
    /// the buff step IS the buffed state. The deck is read out of the observation buffer
    /// while HP is read from the live info struct, so an unrefreshed buffer showed the
    /// max HP moving and the upgrades not happening at all.
    /// </remarks>
    public static unsafe int Sts2Run_DebugUpgradeDeck(int handle, int* obsBuf)
    {
        if (!TryGet(handle, out var run))
        {
            return -1;
        }

        for (int i = 0; i < run.State.Deck.Count; i++)
        {
            if (Core.Run.RunConstants.IsRunCardUpgradable(run.State.Deck[i]))
            {
                run.State.Deck[i] = run.State.Deck[i] with { Upgraded = true };
            }
        }

        run.WriteObservation(new Span<int>(obsBuf, RunConstants.RunObsSize));
        return 0;
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
        // How many CARD items the screen holds, not whether it holds one: Kaleidoscope
        // puts two there at once.
        rewards[3] = (state.RewardCardPending ? 1 : 0) + state.PendingOtherCharacterCardRewards;
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
