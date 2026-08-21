using Sts2Emulator.Core;
using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public sealed class RunEngine
{
    public RunState State { get; private init; } = new();

    public void Reset(string seed)
    {
        // Store the canonical seed, not the typed one: the game canonicalizes before it
        // hashes, so "abcdefo" and "ABCDEF0" are the same run.
        State.Rng = new RunRngSet(seed);
        State.StringSeed = State.Rng.StringSeed;
        State.PlayerRng = new PlayerRngSet(State.Rng);
        State.PlayerHp = 64;
        State.PlayerMaxHp = 80;
        State.Gold = 99;
        State.Floor = 1;
        State.Act = RunConstants.ActOvergrowth;
        State.Phase = RunPhase.Ancient;
        State.Deck = [];
        foreach (int cardId in RunConstants.StarterDeckIds)
        {
            RunNonCombatEffects.AddCardToDeck(State, new CardInstance(cardId, Upgraded: false));
        }

        State.Relics = [new RelicInstance(RunConstants.RelicBurningBlood)];
        Array.Clear(State.PotionSlots);
        State.CurrentNodeType = RunConstants.NodeNormal;
        Array.Clear(State.NeowOptions);
        Array.Clear(State.RewardCards);
        Array.Clear(State.RewardUpgraded);
        State.RewardGold = 0;
        State.RewardPotion = 0;
        State.RewardCardPending = false;
        State.ReturnToRewardScreenAfterCardReward = false;
        Array.Clear(State.MapNodeTypes);
        Array.Clear(State.MapChoices);
        Array.Clear(State.ShopCards);
        Array.Clear(State.ShopRelics);
        Array.Clear(State.ShopPotions);
        Array.Clear(State.ShopCosts);
        State.RelicReward = 0;
        State.EventId = 0;
        // CardRarityOdds starts at -0.05, not zero: `CardRarityOdds(Rng rng) : base(-0.05f, rng)`,
        // the same value a Rare roll resets it to. Starting at zero made every early
        // reward roll read 0.05 too generous, so cards the game rolled Common came
        // back Uncommon until the offset had grown past the gap.
        State.CardRarityOffset = RunRewardGenerator.CardRarityBaseOffset;
        State.PotionRewardOdds = 0.4;
        State.PendingRelicReward = false;
        State.ShopRemovalsUsed = 0;
        State.TransformSelectedDeckIndex = null;
        State.PendingSelfHelpBookEnchantType = 0;
        State.PendingRestUpgrade = false;
        State.RestResultPending = false;
        State.UnknownMapPointsVisited = 0;
        State.UnknownMapPointMonsterOdds = 0.1;
        State.UnknownMapPointEliteOdds = -1.0;
        State.UnknownMapPointTreasureOdds = 0.02;
        State.UnknownMapPointShopOdds = 0.03;
        State.LastResolvedRoomType = RunConstants.NodeNone;
        State.ActiveCombat = null;
        State.ActiveCombatRng = null;
        State.LastPlayerWon = false;
        State.CompletedCombatRoomsBeforeCurrent = 0;
        GenerateNeowOptions();
        RunMapGenerator.SelectActAndGenerateRooms(State);
        RunMapGenerator.GenerateActMap(State);
    }

    public int StartCombat(
        ReadOnlySpan<int> deckIds,
        int encounterId,
        ReadOnlySpan<int> relicIds,
        int playerHp,
        int playerMaxHp,
        ReadOnlySpan<int> potionIds,
        int playerGold,
        int completedCombatRoomsBeforeCurrent = 0
    )
    {
        int[] startingPotions = potionIds.ToArray();
        var runDeck = deckIds
            .ToArray()
            .Select(id => new CardInstance(Math.Abs(id), id < 0))
            .ToList();
        var runRelics = relicIds.ToArray().Select(id => new RelicInstance(id)).ToList();
        return StartCombatWithDeck(
            runDeck,
            encounterId,
            runRelics,
            playerHp,
            playerMaxHp,
            potionIds,
            playerGold,
            completedCombatRoomsBeforeCurrent
        );
    }

    private int StartCombatWithDeck(
        IReadOnlyList<CardInstance> runDeck,
        int encounterId,
        IReadOnlyList<RelicInstance> runRelics,
        int playerHp,
        int playerMaxHp,
        ReadOnlySpan<int> potionIds,
        int playerGold,
        int completedCombatRoomsBeforeCurrent = 0
    )
    {
        int[] startingPotions = potionIds.ToArray();
        State.Deck = runDeck.ToList();
        State.Relics = runRelics.ToList();
        State.PlayerHp = Math.Clamp(playerHp, 0, Math.Max(1, playerMaxHp));
        State.PlayerMaxHp = Math.Max(1, playerMaxHp);
        State.Gold = Math.Max(0, playerGold);
        Array.Clear(State.PotionSlots);
        for (int i = 0; i < Math.Min(State.PotionSlots.Length, startingPotions.Length); i++)
        {
            State.PotionSlots[i] = startingPotions[i];
        }

        State.CompletedCombatRoomsBeforeCurrent = Math.Max(0, completedCombatRoomsBeforeCurrent);

        var combatDeck = runDeck.ToArray();
        State.Rng.Shuffle.Shuffle(combatDeck);

        var shuffleRng = new CountingRandom(State.Rng.Shuffle.RawSeed);
        for (int i = 0; i < State.Rng.Shuffle.CallCount; i++)
        {
            shuffleRng.Next();
        }

        var combat = new CombatState();
        var combatRng = new CountingRandom(State.Rng.Niche.RawSeed);
        var nicheHpRng = new CountingRandom(State.Rng.Niche.RawSeed);
        for (int i = 0; i < State.Rng.Niche.CallCount; i++)
        {
            nicheHpRng.Next();
        }

        combat.NicheHpRng = nicheHpRng;

        // Monster move selection reads the run's "monster_ai" stream -- FlutterPower
        // reaches for Monster.RunRng.MonsterAi by name -- so it spans the whole run.
        // Restarting it each combat put every branching monster on the wrong draw:
        // Mawler's third choice came up Rip and Tear where the game rolled Claw.
        var aiRng = new CountingRandom(State.Rng.MonsterAi.RawSeed);
        for (int i = 0; i < State.Rng.MonsterAi.CallCount; i++)
        {
            aiRng.Next();
        }

        // Target choice draws from the run's "combat_targets" stream, so combat picks up
        // where the run left off and hands the call count back when it ends.
        var targetRng = new CountingRandom(State.Rng.CombatTargets.RawSeed);
        for (int i = 0; i < State.Rng.CombatTargets.CallCount; i++)
        {
            targetRng.Next();
        }

        combat.TargetRng = targetRng;

        var cardSelectionRng = new CountingRandom(State.Rng.CombatCardSelection.RawSeed);
        for (int i = 0; i < State.Rng.CombatCardSelection.CallCount; i++)
        {
            cardSelectionRng.Next();
        }

        combat.CardSelectionRng = cardSelectionRng;

        var cardGenerationRng = new CountingRandom(State.Rng.CombatCardGeneration.RawSeed);
        for (int i = 0; i < State.Rng.CombatCardGeneration.CallCount; i++)
        {
            cardGenerationRng.Next();
        }

        combat.CardGenerationRng = cardGenerationRng;

        var potionGenerationRng = new CountingRandom(State.Rng.CombatPotionGeneration.RawSeed);
        for (int i = 0; i < State.Rng.CombatPotionGeneration.CallCount; i++)
        {
            potionGenerationRng.Next();
        }

        combat.PotionGenerationRng = potionGenerationRng;

        CombatFactory.Reset(
            combat,
            combatRng,
            combatDeck,
            encounterId,
            runRelics.Select(relic => relic.DefId).ToArray(),
            State.PlayerHp,
            State.PlayerMaxHp,
            startingPotions,
            State.Gold,
            deckPreShuffled: true,
            shuffleRng,
            EncounterRngSeed(encounterId),
            nicheSkipCount: 0,
            aiRng,
            State.CompletedCombatRoomsBeforeCurrent
        );
        Effects.RelicEffects.RestoreUsedUpRelics(combat, State.UsedUpRelics);

        State.ActiveCombat = combat;
        State.ActiveCombatRng = combatRng;
        State.LastPlayerWon = false;
        State.Phase = RunPhase.Combat;
        SyncNicheRngFromCombat();
        return 0;
    }

    private int EnterUnknownMapPoint()
    {
        int resolvedNodeType = RollUnknownMapPointNodeType();
        State.UnknownMapPointsVisited++;
        State.CurrentNodeType = resolvedNodeType;
        State.LastResolvedRoomType = resolvedNodeType;

        switch (resolvedNodeType)
        {
            case RunConstants.NodeNormal:
                State.NormalEncountersVisited++;
                return StartCombatWithDeck(
                    State.Deck,
                    State.NormalEncounterSequence[
                        (State.NormalEncountersVisited - 1) % State.NormalEncounterSequence.Length
                    ],
                    State.Relics,
                    State.PlayerHp,
                    State.PlayerMaxHp,
                    State.PotionSlots,
                    State.Gold,
                    Math.Max(0, State.NormalEncountersVisited + State.EliteEncountersVisited - 1)
                );
            case RunConstants.NodeElite:
                State.EliteEncountersVisited++;
                return StartCombatWithDeck(
                    State.Deck,
                    State.EliteEncounterSequence[
                        (State.EliteEncountersVisited - 1) % State.EliteEncounterSequence.Length
                    ],
                    State.Relics,
                    State.PlayerHp,
                    State.PlayerMaxHp,
                    State.PotionSlots,
                    State.Gold,
                    Math.Max(0, State.NormalEncountersVisited + State.EliteEncountersVisited - 1)
                );
            case RunConstants.NodeShop:
                RunRewardGenerator.EnterShop(State);
                return 0;
            case RunConstants.NodeRelic:
                RunRewardGenerator.EnterTreasureRoom(State);
                return 0;
            case RunConstants.NodeEvent:
                return EnterEventRoom();
            default:
                EnterMapPhase();
                return 0;
        }
    }

    private int EnterEventRoom()
    {
        if (State.Act == RunConstants.ActUnderdocks && State.Floor == 13)
        {
            State.CurrentNodeType = RunConstants.NodeNormal;
            State.LastResolvedRoomType = RunConstants.NodeNormal;
            State.NormalEncountersVisited++;
            return StartCombatWithDeck(
                State.Deck,
                9,
                State.Relics,
                State.PlayerHp,
                State.PlayerMaxHp,
                State.PotionSlots,
                State.Gold,
                Math.Max(0, State.NormalEncountersVisited + State.EliteEncountersVisited - 1)
            );
        }

        RunNonCombatEffects.EnterEvent(State);

        return 0;
    }

    private int RollUnknownMapPointNodeType()
    {
        var allowedRoomTypes = new HashSet<int>
        {
            RunConstants.NodeNormal,
            RunConstants.NodeElite,
            RunConstants.NodeRelic,
            RunConstants.NodeShop,
            RunConstants.NodeEvent,
        };
        if (IsUnknownShopBlacklisted())
        {
            allowedRoomTypes.Remove(RunConstants.NodeShop);
        }

        int roomType = allowedRoomTypes.Contains(RunConstants.NodeEvent)
            ? RunConstants.NodeEvent
            : allowedRoomTypes.Min();
        double roll = State.Rng.UnknownMapPoint.NextDouble();
        double cumulative = 0.0;
        foreach (
            var (candidateRoomType, odds) in new (int RoomType, double Odds)[]
            {
                (RunConstants.NodeNormal, State.UnknownMapPointMonsterOdds),
                (RunConstants.NodeElite, State.UnknownMapPointEliteOdds),
                (RunConstants.NodeRelic, State.UnknownMapPointTreasureOdds),
                (RunConstants.NodeShop, State.UnknownMapPointShopOdds),
            }
        )
        {
            if (!allowedRoomTypes.Contains(candidateRoomType) || odds < 0.0)
            {
                continue;
            }

            cumulative += odds;
            if (roll <= cumulative)
            {
                roomType = candidateRoomType;
                break;
            }
        }

        UpdateUnknownMapPointOdds(roomType, allowedRoomTypes);
        return roomType;
    }

    private bool IsUnknownShopBlacklisted()
    {
        if (State.LastResolvedRoomType == RunConstants.NodeShop)
        {
            return true;
        }

        if (
            !State.MapNodes.TryGetValue(State.CurrentMapCoord, out var current)
            || current.Children.Count == 0
        )
        {
            return false;
        }

        return current.Children.All(child =>
            State.MapNodes.TryGetValue(child, out var node)
            && node.NodeType == RunConstants.NodeShop
        );
    }

    private void UpdateUnknownMapPointOdds(int rolledRoomType, IReadOnlySet<int> allowedRoomTypes)
    {
        UpdateUnknownMapPointOdd(
            rolledRoomType,
            allowedRoomTypes,
            RunConstants.NodeNormal,
            0.1,
            odds => State.UnknownMapPointMonsterOdds = odds,
            () => State.UnknownMapPointMonsterOdds
        );
        UpdateUnknownMapPointOdd(
            rolledRoomType,
            allowedRoomTypes,
            RunConstants.NodeElite,
            -1.0,
            odds => State.UnknownMapPointEliteOdds = odds,
            () => State.UnknownMapPointEliteOdds
        );
        UpdateUnknownMapPointOdd(
            rolledRoomType,
            allowedRoomTypes,
            RunConstants.NodeRelic,
            0.02,
            odds => State.UnknownMapPointTreasureOdds = odds,
            () => State.UnknownMapPointTreasureOdds
        );
        UpdateUnknownMapPointOdd(
            rolledRoomType,
            allowedRoomTypes,
            RunConstants.NodeShop,
            0.03,
            odds => State.UnknownMapPointShopOdds = odds,
            () => State.UnknownMapPointShopOdds
        );
    }

    private static void UpdateUnknownMapPointOdd(
        int rolledRoomType,
        IReadOnlySet<int> allowedRoomTypes,
        int roomType,
        double baseOdds,
        Action<double> setOdds,
        Func<double> getOdds
    )
    {
        if (rolledRoomType == roomType)
        {
            setOdds(baseOdds);
        }
        else if (allowedRoomTypes.Contains(roomType))
        {
            setOdds(getOdds() + baseOdds);
        }
    }

    public void WriteActionMask(Span<int> mask)
    {
        mask.Clear();
        switch (State.Phase)
        {
            case RunPhase.Combat:
                if (State.ActiveCombat is not null)
                {
                    foreach (int action in CombatEngine.ValidActions(State.ActiveCombat))
                    {
                        SetMask(mask, action);
                    }
                }

                break;

            case RunPhase.CardReward:
                for (int i = 0; i <= RunConstants.RewardSkipAction; i++)
                {
                    SetMask(mask, i);
                }

                break;

            case RunPhase.Map:
                for (int i = 0; i < State.MapNodeTypes.Length; i++)
                {
                    if (State.MapNodeTypes[i] != RunConstants.NodeNone)
                    {
                        SetMask(mask, i);
                    }
                }

                break;

            case RunPhase.Rest:
                SetMask(mask, RunConstants.RestHealAction);
                if (State.Deck.Any(RunConstants.IsRunCardUpgradable))
                {
                    SetMask(mask, RunConstants.RestUpgradeAction);
                }

                SetMask(mask, RunConstants.RewardSkipAction);
                break;

            case RunPhase.Shop:
                for (int i = 0; i < State.ShopCards.Length; i++)
                {
                    if (State.ShopCards[i] != 0 && State.Gold >= State.ShopCosts[i])
                    {
                        SetMask(mask, i);
                    }
                }

                for (int action = 7; action < 10; action++)
                {
                    if (State.ShopRelics[action - 7] != 0 && State.Gold >= State.ShopCosts[action])
                    {
                        SetMask(mask, action);
                    }
                }

                bool hasPotionSlot = State.PotionSlots.Any(potion => potion == 0);
                for (int action = 10; action < 13; action++)
                {
                    if (
                        State.ShopPotions[action - 10] != 0
                        && State.Gold >= State.ShopCosts[action]
                        && hasPotionSlot
                    )
                    {
                        SetMask(mask, action);
                    }
                }

                if (
                    State.Gold >= State.ShopCosts[RunConstants.ShopRemoveAction]
                    && State.Deck.Count > 1
                )
                {
                    SetMask(mask, RunConstants.ShopRemoveAction);
                }

                SetMask(mask, RunConstants.ShopSkipAction);
                break;

            case RunPhase.RelicReward:
                WriteRewardActionMask(mask);
                SetMask(mask, RunConstants.RewardSkipAction);
                break;

            case RunPhase.Event:
                WriteEventActionMask(mask);
                break;

            case RunPhase.Ancient when State.NeowAwaitingProceed:
                SetMask(mask, 0);
                break;

            case RunPhase.Ancient:
                for (int i = 0; i < State.NeowOptions.Length; i++)
                {
                    if (State.NeowOptions[i] != 0)
                    {
                        SetMask(mask, i);
                    }
                }

                break;

            case RunPhase.TransformSelect:
                if (State.PendingRestUpgrade)
                {
                    for (int i = 0; i < State.Deck.Count; i++)
                    {
                        if (RunConstants.IsRunCardUpgradable(State.Deck[i]))
                        {
                            SetMask(mask, i);
                        }
                    }
                }
                else if (State.PendingSelfHelpBookEnchantType != 0)
                {
                    for (int i = 0; i < State.Deck.Count; i++)
                    {
                        if (RunNonCombatEffects.CanApplySelfHelpBookEnchantment(State, i))
                        {
                            SetMask(mask, i);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < State.Deck.Count; i++)
                    {
                        SetMask(mask, i);
                    }
                }
                break;

            case RunPhase.Treasure:
                SetMask(mask, RunConstants.RewardSkipAction);
                break;
        }
    }

    /// <summary>
    /// A deep copy of this run, for a search that wants to fork a position instead of
    /// replaying to reach it.
    /// </summary>
    /// <param name="resampleSeed">
    /// When given, everything the run has not yet paid out is resampled off this seed:
    /// future rewards, shop stock, encounter composition, shuffles, and the part of the
    /// draw pile the player has not been shown. What has already happened is untouched.
    /// Omit it for a faithful copy -- which is an oracle, and must not be searched with.
    /// See docs/agent-interface.md.
    /// </param>
    public RunEngine Clone(int? resampleSeed = null)
    {
        var copy = new RunEngine
        {
            State = State.Clone(
                resampleSeed is null ? null : ResampledSeedString(resampleSeed.Value)
            ),
        };
        if (resampleSeed is not null)
        {
            copy.ResampleHiddenState(new Random(resampleSeed.Value));
        }

        return copy;
    }

    /// <summary>The resampled run's seed, distinct from any seed a player could type.</summary>
    private static string ResampledSeedString(int resampleSeed) =>
        $"RESAMPLE{unchecked((uint)resampleSeed):X8}";

    /// <summary>
    /// Point the in-combat streams at the resampled run's streams, and reshuffle the
    /// region of the draw pile the player has not seen.
    /// </summary>
    /// <remarks>
    /// The combat streams keep their call counts: SyncAfterCombat hands those counts
    /// back to the run streams when the fight ends, so the two have to stay in step
    /// even though the values behind them have changed.
    /// </remarks>
    private void ResampleHiddenState(Random rng)
    {
        var combat = State.ActiveCombat;
        if (combat is null)
        {
            return;
        }

        combat.ShuffleRng = Restream(combat.ShuffleRng, State.Rng.Shuffle);
        combat.NicheHpRng = Restream(combat.NicheHpRng, State.Rng.Niche);
        combat.TargetRng = Restream(combat.TargetRng, State.Rng.CombatTargets);
        combat.CardSelectionRng = Restream(combat.CardSelectionRng, State.Rng.CombatCardSelection);
        combat.CardGenerationRng = Restream(
            combat.CardGenerationRng,
            State.Rng.CombatCardGeneration
        );
        combat.PotionGenerationRng = Restream(
            combat.PotionGenerationRng,
            State.Rng.CombatPotionGeneration
        );
        combat.AiRng = Restream(combat.AiRng as CountingRandom, State.Rng.MonsterAi);
        State.ActiveCombatRng = Restream(State.ActiveCombatRng, State.Rng.Niche);

        ReshuffleUnknownDrawPile(combat, rng);
    }

    private static CountingRandom? Restream(CountingRandom? current, GameRng stream)
    {
        if (current is null)
        {
            return null;
        }

        var replacement = new CountingRandom(stream.RawSeed);
        for (int i = 0; i < current.CallCount; i++)
        {
            replacement.Next();
        }

        return replacement;
    }

    /// <summary>
    /// Shuffle the part of the draw pile between the known top and the known bottom.
    /// Deliberately a plain uniform shuffle, not the game's StableShuffle: the point is
    /// to sample a world the player cannot rule out, not to reproduce the game's order.
    /// </summary>
    private static void ReshuffleUnknownDrawPile(CombatState combat, Random rng)
    {
        int start = combat.KnownTopCount;
        int end = combat.DrawPile.Count - combat.KnownBottomCount;
        for (int i = end - 1; i > start; i--)
        {
            int j = start + rng.Next(i - start + 1);
            (combat.DrawPile[i], combat.DrawPile[j]) = (combat.DrawPile[j], combat.DrawPile[i]);
        }
    }

    public void WriteObservation(Span<int> obs)
    {
        if (obs.Length < RunConstants.RunObsSize)
        {
            throw new ArgumentException("Run observation buffer is too small.", nameof(obs));
        }

        obs[..RunConstants.RunObsSize].Clear();
        CombatState? activeCombat = State.Phase == RunPhase.Combat ? State.ActiveCombat : null;
        if (activeCombat is not null)
        {
            CombatObservation.Write(activeCombat, obs);
        }

        int offset = RunConstants.CombatObsSize;
        int playerHp = activeCombat?.PlayerHp ?? State.PlayerHp;
        int playerMaxHp = activeCombat?.PlayerMaxHp ?? State.PlayerMaxHp;
        int gold = activeCombat?.PlayerGold ?? State.Gold;
        int[] potionSlots = activeCombat?.PotionSlots ?? State.PotionSlots;
        obs[offset + 0] = (int)State.Phase;
        obs[offset + 1] = State.Floor;
        obs[offset + 2] = State.Act;
        obs[offset + 3] = State.Deck.Count;
        obs[offset + 4] = gold;
        obs[offset + 5] = playerHp;
        obs[offset + 6] = playerMaxHp;
        obs[offset + 7] = State.Relics.Count;
        obs[offset + 8] = State.CurrentNodeType;
        obs[offset + 9] = State.RewardCards[0];
        obs[offset + 10] = State.RewardCards[1];
        obs[offset + 11] = State.RewardCards[2];
        obs[offset + 12] = State.MapNodeTypes[0];
        obs[offset + 13] = State.MapNodeTypes[1];
        obs[offset + 14] = State.MapNodeTypes[2];
        obs[offset + 15] = State.MapNodeTypes[3];
        obs[offset + 16] = State.MapChoices[0];
        obs[offset + 17] = State.MapChoices[1];
        obs[offset + 18] = State.MapChoices[2];
        obs[offset + 19] = State.MapChoices[3];
        obs[offset + 20] = State.ShopCards[0];
        obs[offset + 21] = State.ShopCards[1];
        obs[offset + 22] = State.ShopCards[2];
        obs[offset + 23] = State.RelicReward;
        obs[offset + 24] = State.EventId;
        obs[offset + 25] = potionSlots[0];
        obs[offset + 26] = potionSlots[1];
        obs[offset + 27] = potionSlots[2];
        obs[offset + 28] = State.ShopRelics[0];
        obs[offset + 29] = State.ShopRelics[1];
        obs[offset + 30] = State.ShopRelics[2];
        obs[offset + 31] = State.ShopPotions[0];
        obs[offset + 32] = State.ShopPotions[1];
        obs[offset + 33] = State.ShopPotions[2];
        obs[offset + 34] = State.ShopCosts[RunConstants.ShopRemoveAction];
    }

    public void WriteInfo(Span<int> info)
    {
        if (info.Length < RunConstants.RunInfoSize)
        {
            throw new ArgumentException("Run info buffer is too small.", nameof(info));
        }

        info[..RunConstants.RunInfoSize].Clear();
        CombatState? activeCombat = State.Phase == RunPhase.Combat ? State.ActiveCombat : null;
        int playerHp = activeCombat?.PlayerHp ?? State.PlayerHp;
        int playerMaxHp = activeCombat?.PlayerMaxHp ?? State.PlayerMaxHp;
        int gold = activeCombat?.PlayerGold ?? State.Gold;
        info[0] = (int)State.Phase;
        info[1] = State.Floor;
        info[2] = State.Act;
        info[3] = State.Deck.Count;
        info[4] = gold;
        info[5] = playerHp;
        info[6] = playerMaxHp;
        info[7] = State.Relics.Count;
        info[8] = State.CurrentNodeType;
        info[9] = State.EventId;
        info[10] = State.RelicReward;
    }

    public int Step(
        int action,
        int targetEnemyIndex,
        out float reward,
        out bool terminal,
        out bool truncated
    )
    {
        reward = 0.0f;
        terminal = false;
        truncated = false;

        if (State.Phase == RunPhase.Ancient)
        {
            if (State.NeowAwaitingProceed)
            {
                if (action != 0)
                {
                    return -1;
                }

                State.NeowAwaitingProceed = false;
                EnterMapPhase();
                return 0;
            }

            if (action is < 0 or >= 3 || State.NeowOptions[action] == 0)
            {
                return -1;
            }

            ApplyAncientChoice(State.NeowOptions[action]);
            if (State.Phase == RunPhase.Ancient)
            {
                EnterMapPhase();
            }

            return 0;
        }

        if (State.Phase == RunPhase.Map)
        {
            if (
                !RunMapGenerator.ChooseMapNode(State, action, out int nodeType, out int encounterId)
            )
            {
                return -1;
            }

            switch (nodeType)
            {
                case RunConstants.NodeNormal:
                case RunConstants.NodeElite:
                case RunConstants.NodeBoss:
                    State.LastResolvedRoomType = nodeType;
                    State.Phase = RunPhase.Combat;
                    int completedRooms =
                        State.NormalEncountersVisited + State.EliteEncountersVisited - 1;
                    return StartCombatWithDeck(
                        State.Deck,
                        encounterId,
                        State.Relics,
                        State.PlayerHp,
                        State.PlayerMaxHp,
                        State.PotionSlots,
                        State.Gold,
                        Math.Max(0, completedRooms)
                    );
                case RunConstants.NodeRest:
                    State.LastResolvedRoomType = RunConstants.NodeRest;
                    State.Phase = RunPhase.Rest;
                    break;
                case RunConstants.NodeShop:
                    State.LastResolvedRoomType = RunConstants.NodeShop;
                    RunRewardGenerator.EnterShop(State);
                    break;
                case RunConstants.NodeRelic:
                    State.LastResolvedRoomType = RunConstants.NodeRelic;
                    RunRewardGenerator.EnterTreasureRoom(State);
                    break;
                case RunConstants.NodeEvent:

                    return EnterUnknownMapPoint();
                default:
                    EnterMapPhase();
                    break;
            }
            return 0;
        }

        if (State.Phase == RunPhase.Combat)
        {
            if (State.ActiveCombat is null || State.ActiveCombatRng is null)
            {
                return -1;
            }

            var result =
                targetEnemyIndex >= 0
                    ? CombatEngine.Step(
                        State.ActiveCombat,
                        action,
                        State.ActiveCombatRng,
                        targetEnemyIndex
                    )
                    : CombatEngine.Step(State.ActiveCombat, action, State.ActiveCombatRng);
            reward = result.Reward;
            terminal = result.Terminal;
            State.LastPlayerWon = result.Terminal && result.PlayerWon;
            if (result.Terminal)
            {
                SyncAfterCombat();
                if (result.PlayerWon)
                {
                    RunRewardGenerator.GenerateCombatRewards(State);
                    terminal = false;
                }
                else
                {
                    State.Phase = RunPhase.Complete;
                }
            }
            return 0;
        }

        if (State.Phase == RunPhase.CardReward)
        {
            return StepCardReward(action, out terminal);
        }

        if (State.Phase == RunPhase.RelicReward)
        {
            return StepRelicReward(action, out terminal);
        }

        if (State.Phase == RunPhase.Shop)
        {
            return StepShop(action, out terminal);
        }

        if (State.Phase == RunPhase.Rest)
        {
            return StepRest(action, out terminal);
        }

        if (State.Phase == RunPhase.Event)
        {
            return StepEvent(action, out terminal);
        }

        if (State.Phase == RunPhase.TransformSelect)
        {
            return StepTransformSelect(action, out terminal);
        }

        if (State.Phase == RunPhase.Treasure)
        {
            if (action != RunConstants.RewardSkipAction)
            {
                return -1;
            }

            int status = AdvanceAfterNode(out terminal);
            return status;
        }

        if (State.Phase == RunPhase.Complete)
        {
            terminal = true;
            return 0;
        }

        return -1;
    }

    public int ActiveEncounterId => State.ActiveCombat?.EncounterId ?? -1;

    public int ActiveShuffleRngCallCount => State.ActiveCombat?.ShuffleRng?.CallCount ?? 0;

    public int ActiveNicheRngCallCount => State.ActiveCombat?.NicheHpRng?.CallCount ?? 0;

    private void GenerateNeowOptions()
    {
        var rng = State.Rng.NeowRng();
        int[] curseOptions = RunConstants.NeowCurseOptions.ToArray();
        int cursed = curseOptions[rng.NextInt(curseOptions.Length)];

        List<int> positive = RunConstants.NeowPositiveOptions.ToArray().ToList();
        if (cursed == RunConstants.RelicCursedPearl)
        {
            positive.Remove(RunConstants.RelicGoldenPearl);
        }
        else if (cursed == RunConstants.RelicHeftyTablet)
        {
            positive.Remove(RunConstants.RelicArcaneScroll);
        }
        else if (cursed == RunConstants.RelicLeafyPoultice)
        {
            positive.Remove(RunConstants.RelicNewLeaf);
        }
        else if (cursed == RunConstants.RelicPrecariousShears)
        {
            positive.Remove(RunConstants.RelicPreciseScissors);
        }

        if (cursed != RunConstants.RelicLargeCapsule)
        {
            positive.Add(
                rng.NextBool() ? RunConstants.RelicLavaRock : RunConstants.RelicSmallCapsule
            );
        }

        positive.Add(
            rng.NextBool() ? RunConstants.RelicNutritiousOyster : RunConstants.RelicStoneHumidifier
        );
        positive.Add(rng.NextBool() ? RunConstants.RelicNeowsTalisman : RunConstants.RelicPomander);
        rng.Shuffle(positive);

        State.NeowOptions[0] = positive[0];
        State.NeowOptions[1] = positive[1];
        State.NeowOptions[2] = cursed;
    }

    private void EnterMapPhase()
    {
        State.Phase = RunPhase.Map;
        RunMapGenerator.RefreshMapOptions(State);
    }

    private void ApplyAncientChoice(int relicId)
    {
        Array.Clear(State.NeowOptions);
        RunFollowUp followUp = RunNonCombatEffects.ApplyRelicPickup(State, relicId);
        if (relicId == RunConstants.RelicLostCoffer)
        {
            RunRewardGenerator.EnterCardReward(State);
            RunRewardGenerator.AddPotion(
                State,
                RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
            );
            State.PotionRewardOdds -= 0.1;
            return;
        }
        if (followUp == RunFollowUp.TransformSelect)
        {
            State.Phase = RunPhase.TransformSelect;
            return;
        }
        if (followUp == RunFollowUp.CardReward)
        {
            RunRewardGenerator.EnterCardReward(State);
            return;
        }
        if (relicId == RunConstants.RelicKaleidoscope)
        {
            // "Obtain 2 card rewards from other characters". RewardsCmd.OfferCustom puts
            // both on the rewards screen at once; the player claims one, picks a card,
            // lands back on the screen and claims the other.
            State.PendingOtherCharacterCardRewards = 2;
            State.NeowAwaitingProceed = true;
            State.Phase = RunPhase.RelicReward;
            return;
        }
        AdvanceRewardRngForNeowRelic(relicId);
    }

    private void AdvanceRewardRngForNeowRelic(int relicId)
    {
        int advances = relicId switch
        {
            RunConstants.RelicPhialHolster => 4,
            RunConstants.RelicHeftyTablet => 3,
            RunConstants.RelicLeadPaperweight => 6,
            _ => 0,
        };
        for (int i = 0; i < advances; i++)
        {
            State.PlayerRng.Rewards.NextDouble();
        }
    }

    /// <summary>
    /// Seed for the encounter's own stream, the one EncounterModel builds from the run
    /// seed, TotalFloor and a hash of the encounter's entry id. Every encounter that
    /// rolls its own roster reads it, so handing back a seed for Slimes alone left the
    /// rest -- Slithering Strangler's secondary enemy, Flyconid's slime, the raiders --
    /// rolling off a constant.
    /// </summary>
    private int? EncounterRngSeed(int encounterId)
    {
        // The first three normal rooms of an act draw from the weak pool, so a shared
        // encounter id is the weak variant exactly that long. Only Corpse Slugs cares,
        // and it never appears as an elite or boss.
        bool weakVariant =
            State.CurrentNodeType == RunConstants.NodeNormal
            && State.NormalEncountersVisited <= RunConstants.WeakEncountersPerAct;

        return EncounterRng.SeedFor((int)State.Rng.Seed, State.Floor, encounterId, weakVariant);
    }

    private void SyncAfterCombat()
    {
        if (State.ActiveCombat is null)
        {
            return;
        }

        Effects.RelicEffects.CollectUsedUpRelics(State.ActiveCombat, State.UsedUpRelics);
        State.PlayerHp = Math.Max(0, State.ActiveCombat.PlayerHp);
        State.PlayerMaxHp = Math.Max(1, State.ActiveCombat.PlayerMaxHp);
        State.Gold = Math.Max(0, State.ActiveCombat.PlayerGold);
        for (int i = 0; i < State.PotionSlots.Length; i++)
        {
            State.PotionSlots[i] = State.ActiveCombat.PotionSlots[i];
        }

        if (State.ActiveCombat.ShuffleRng is not null)
        {
            State.Rng.Shuffle.AdvanceToCallCount(State.ActiveCombat.ShuffleRng.CallCount);
        }

        if (State.ActiveCombat.TargetRng is not null)
        {
            State.Rng.CombatTargets.AdvanceToCallCount(State.ActiveCombat.TargetRng.CallCount);
        }

        if (State.ActiveCombat.AiRng is CountingRandom aiRng)
        {
            State.Rng.MonsterAi.AdvanceToCallCount(aiRng.CallCount);
        }

        if (State.ActiveCombat.CardSelectionRng is not null)
        {
            State.Rng.CombatCardSelection.AdvanceToCallCount(
                State.ActiveCombat.CardSelectionRng.CallCount
            );
        }

        if (State.ActiveCombat.CardGenerationRng is not null)
        {
            State.Rng.CombatCardGeneration.AdvanceToCallCount(
                State.ActiveCombat.CardGenerationRng.CallCount
            );
        }

        if (State.ActiveCombat.PotionGenerationRng is not null)
        {
            State.Rng.CombatPotionGeneration.AdvanceToCallCount(
                State.ActiveCombat.PotionGenerationRng.CallCount
            );
        }

        SyncNicheRngFromCombat();
    }

    private void SyncNicheRngFromCombat()
    {
        if (State.ActiveCombat?.NicheHpRng is not null)
        {
            State.Rng.Niche.AdvanceToCallCount(State.ActiveCombat.NicheHpRng.CallCount);
        }
    }

    private int StepCardReward(int action, out bool terminal)
    {
        terminal = false;
        if (0 <= action && action < State.RewardCards.Length)
        {
            int cardId = State.RewardCards[action];
            if (cardId == 0)
            {
                return -1;
            }

            RunNonCombatEffects.AddCardToDeck(
                State,
                new CardInstance(cardId, State.RewardUpgraded[action])
            );
        }
        else if (action != RunConstants.RewardSkipAction)
        {
            return -1;
        }

        Array.Clear(State.RewardCards);
        Array.Clear(State.RewardUpgraded);
        if (State.ReturnToRewardScreenAfterCardReward)
        {
            State.ReturnToRewardScreenAfterCardReward = false;
            if (State.NeowAwaitingProceed && !RunRewardGenerator.HasPendingRewards(State))
            {
                State.Phase = RunPhase.Ancient;
                return 0;
            }

            // Back to the rewards screen even when nothing is left on it. The game keeps
            // it open and waits to be dismissed — the player still has to proceed — so
            // advancing straight to the map skips an action the run actually takes.
            State.Phase = RunPhase.RelicReward;
            return 0;
        }

        if (State.EventId == RunConstants.EventBrainLeech)
        {
            State.EventId = RunConstants.EventResultPending;
            State.Phase = RunPhase.Event;
            return 0;
        }

        if (State.PendingRelicReward)
        {
            State.PendingRelicReward = false;
            RunRewardGenerator.EnterRelicReward(State);
            return 0;
        }
        return AdvanceAfterNode(out terminal);
    }

    private int StepRelicReward(int action, out bool terminal)
    {
        terminal = false;
        if (State.RewardPotion != 0 && action is >= 4 and <= 6)
        {
            int slot = action - 4;
            if ((uint)slot >= State.PotionSlots.Length || State.PotionSlots[slot] == 0)
            {
                return -1;
            }

            State.PotionSlots[slot] = 0;
            return 0;
        }

        if (action == RunConstants.RewardSkipAction)
        {
            // Skipping the screen abandons everything still on it, queued potions included.
            State.PendingRestPotions = 0;
            return AdvanceAfterRelicReward(out terminal);
        }

        if (!RunRewardGenerator.HasPendingRewards(State))
        {
            if (action is 0 or RunConstants.RewardSkipAction)
            {
                return AdvanceAfterRelicReward(out terminal);
            }

            return -1;
        }

        if (!RunRewardGenerator.ClaimRewardAtIndex(State, action))
        {
            return -1;
        }

        OfferNextRestPotion();
        return 0;
    }

    /// <summary>Moves the next queued rest potion onto the reward screen, if the screen is free.</summary>
    private void OfferNextRestPotion()
    {
        if (State.PendingRestPotions <= 0 || State.RewardPotion != 0)
        {
            return;
        }

        State.PendingRestPotions--;
        State.RewardPotion = RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards);
    }

    private int StepShop(int action, out bool terminal)
    {
        terminal = false;
        bool shouldAdvance = action == RunConstants.ShopSkipAction;
        if (0 <= action && action < State.ShopCards.Length)
        {
            int cardId = State.ShopCards[action];
            int cost = State.ShopCosts[action];
            if (cardId == 0 || State.Gold < cost)
            {
                return -1;
            }

            State.Gold -= cost;
            RunNonCombatEffects.AddCardToDeck(State, new CardInstance(cardId, Upgraded: false));
            State.ShopCards[action] = 0;
        }
        else if (7 <= action && action < 10)
        {
            int index = action - 7;
            int relicId = State.ShopRelics[index];
            int cost = State.ShopCosts[action];
            if (relicId == 0 || State.Gold < cost)
            {
                return -1;
            }

            State.Gold -= cost;
            if (State.Relics.All(relic => relic.DefId != relicId))
            {
                State.Relics.Add(new RelicInstance(relicId));
            }

            State.ShopRelics[index] = 0;
        }
        else if (10 <= action && action < 13)
        {
            int index = action - 10;
            int potionId = State.ShopPotions[index];
            int cost = State.ShopCosts[action];
            if (
                potionId == 0
                || State.Gold < cost
                || !RunRewardGenerator.AddPotion(State, potionId)
            )
            {
                return -1;
            }

            State.Gold -= cost;
            State.ShopPotions[index] = 0;
        }
        else if (action == RunConstants.ShopRemoveAction)
        {
            int cost = State.ShopCosts[RunConstants.ShopRemoveAction];
            if (State.Gold < cost || State.Deck.Count <= 1)
            {
                return -1;
            }

            State.Gold -= cost;
            RunNonCombatEffects.RemoveLowestPriorityCard(State);
            State.ShopRemovalsUsed++;
        }
        else if (action != RunConstants.ShopSkipAction)
        {
            return -1;
        }

        return shouldAdvance ? AdvanceAfterNode(out terminal) : 0;
    }

    private int AdvanceAfterRelicReward(out bool terminal)
    {
        if (State.CurrentNodeType == RunConstants.NodeBoss)
        {
            State.Phase = RunPhase.Complete;
            terminal = true;
            return 0;
        }
        return AdvanceAfterNode(out terminal);
    }

    private int AdvanceAfterNode(out bool terminal)
    {
        terminal = false;
        int terminalFloor =
            State.Act == RunConstants.ActUnderdocks
                ? RunConstants.MapBossRow * 2 + 1
                : RunConstants.MapBossRow + 1;
        if (State.Floor >= terminalFloor)
        {
            State.Phase = RunPhase.Complete;
            terminal = true;
            return 0;
        }
        EnterMapPhase();
        return 0;
    }

    private int StepRest(int action, out bool terminal)
    {
        terminal = false;
        if (State.RestResultPending)
        {
            State.RestResultPending = false;
            return AdvanceAfterNode(out terminal);
        }

        if (action == RunConstants.RestHealAction)
        {
            State.PlayerHp = Math.Min(State.PlayerMaxHp, State.PlayerHp + RestHealAmount());
            // Tiny Mailbox's TryModifyRestSiteHealRewards adds two PotionRewards to the
            // rest, and a reward is offered rather than given: the player chooses whether
            // to take it, and may drop a held potion to make room.
            if (Effects.RelicEffects.Has(State.Relics, Effects.RelicEffects.TinyMailbox))
            {
                State.PendingRestPotions = 2;
                OfferNextRestPotion();
                State.Phase = RunPhase.RelicReward;
                return 0;
            }

            State.RestResultPending = true;
            return 0;
        }
        if (action == RunConstants.RestUpgradeAction)
        {
            if (!State.Deck.Any(RunConstants.IsRunCardUpgradable))
            {
                return -1;
            }

            State.PendingRestUpgrade = true;
            State.Phase = RunPhase.TransformSelect;
            return 0;
        }
        if (action == RunConstants.RewardSkipAction)
        {
            return AdvanceAfterNode(out terminal);
        }

        return -1;
    }

    private int StepEvent(int action, out bool terminal)
    {
        terminal = false;
        if (State.EventId == RunConstants.EventResultPending)
        {
            State.EventId = 0;
            return AdvanceAfterNode(out terminal);
        }

        switch (State.EventId)
        {
            case RunConstants.EventUnrestSite:
                if (action == 0)
                {
                    State.PlayerHp = State.PlayerMaxHp;
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                    );
                }
                else if (action == 1)
                {
                    State.PlayerMaxHp = Math.Max(1, State.PlayerMaxHp - 8);
                    State.PlayerHp = Math.Min(State.PlayerHp, State.PlayerMaxHp);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }
                break;
            case RunConstants.EventAromaOfChaos:
                if (action == 0)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action == 1)
                {
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventJungleMazeAdventure:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 18);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(150)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(50)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventMorphicGrove:
                if (action == 0)
                {
                    State.Gold = 0;
                    RunNonCombatEffects.TransformFirstCard(State);
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.GainMaxHp(State, 5);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSelfHelpBook:
                if (action is >= 0 and <= 2)
                {
                    State.PendingSelfHelpBookEnchantType = action + 1;
                    if (
                        !State
                            .Deck.Where(
                                (_, i) =>
                                    RunNonCombatEffects.CanApplySelfHelpBookEnchantment(State, i)
                            )
                            .Any()
                    )
                    {
                        State.PendingSelfHelpBookEnchantType = 0;
                        return -1;
                    }

                    State.Phase = RunPhase.TransformSelect;
                    return 0;
                }
                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDoorsOfLightAndDark:
                if (action == 0)
                {
                    RunNonCombatEffects.UpgradeTwoRandomCardsWithNiche(State);
                    State.EventId = RunConstants.EventResultPending;
                    return 0;
                }
                if (action == 1)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                    State.EventId = RunConstants.EventResultPending;
                    return 0;
                }
                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSunkenTreasury:
                if (action == 0)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.SunkenTreasurySmallChestGold(State)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.SunkenTreasuryLargeChestGold(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventBrainLeech:
                if (action == 0)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(
                            State.Rng.UpFront.NextItem(
                                RunRewardGenerator.IroncladRewardPool.ToArray()
                            ),
                            Upgraded: false
                        )
                    );
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 5);
                    State.RewardGold = 0;
                    State.RewardPotion = 0;
                    State.RelicReward = 0;
                    State.RewardCardPending = true;
                    State.RewardCards[0] = 455;
                    State.RewardCards[1] = 521;
                    State.RewardCards[2] = 396;
                    State.RewardUpgraded[0] = false;
                    State.RewardUpgraded[1] = false;
                    State.RewardUpgraded[2] = false;
                    State.Phase = RunPhase.RelicReward;
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventByrdonisNest:
                if (action == 0)
                {
                    State.PlayerMaxHp += 7;
                    State.PlayerHp = Math.Min(State.PlayerMaxHp, State.PlayerHp + 7);
                    State.EventId = RunConstants.EventResultPending;
                    return 0;
                }
                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTheLegendsWereTrue:
                if (action == 0)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.SpoilsMapCard, Upgraded: false)
                    );
                }
                else if (action == 1)
                {
                    if (
                        State.PlayerHp <= 8
                        || !RunRewardGenerator.AddPotion(
                            State,
                            RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                        )
                    )
                    {
                        return -1;
                    }

                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDenseVegetation:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 80);
                }
                else if (action == 1)
                {
                    HealPlayer(RestHealAmount());
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventLuminousChoir:
                if (action == 0)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                    );
                }
                else if (action == 1)
                {
                    if (State.Gold < 149)
                    {
                        return -1;
                    }

                    State.Gold -= 149;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSapphireSeed:
                if (action == 0)
                {
                    HealPlayer(9);
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSunkenStatue:
                if (action == 0)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 111);
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 12);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTabletOfTruth:
                if (action == 0)
                {
                    State.PlayerMaxHp = Math.Max(1, State.PlayerMaxHp - 3);
                    State.PlayerHp = Math.Min(State.PlayerHp, State.PlayerMaxHp);
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action == 1)
                {
                    HealPlayer(20);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWellspring:
                if (action == 0)
                {
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWhisperingHollow:
                if (action == 0)
                {
                    if (State.Gold < 35)
                    {
                        return -1;
                    }

                    State.Gold -= 35;
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 9);
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWoodCarvings:
                if (action is >= 0 and <= 2)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventAbyssalBaths:
                if (action == 0)
                {
                    RunNonCombatEffects.GainMaxHp(State, 2);
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 1);
                }
                else if (action == 1)
                {
                    HealPlayer(10);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDrowningBeacon:
                if (action == 0)
                {
                    if (State.PlayerHp <= 13)
                    {
                        return -1;
                    }

                    State.PlayerHp -= 13;
                    RunRewardGenerator.AddPotion(State, 29);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventEndlessConveyor:
                if (action == 0)
                {
                    if (State.Gold < 40)
                    {
                        return -1;
                    }

                    State.Gold -= 40;
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action == 1)
                {
                    HealPlayer(10);
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.GainMaxHp(State, 4);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventPunchOff:
                if (action == 0)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 50);
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSpiralingWhirlpool:
                if (action == 0)
                {
                    HealPlayer(RestHealAmount());
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTrashHeap:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 100);
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWaterloggedScriptorium:
                if (action == 0)
                {
                    if (State.Gold < 55)
                    {
                        return -1;
                    }

                    State.Gold -= 55;
                    RunNonCombatEffects.UpgradeFirstCard(State);
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.GainMaxHp(State, 6);
                }
                else if (action == 2)
                {
                    State.RewardGold = 0;
                    State.RewardPotion = 0;
                    State.RelicReward = 0;
                    RunRewardGenerator.EnterCardReward(State);
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventCrystalSphere:
                if (action == 0)
                {
                    if (State.Gold < 100)
                    {
                        return -1;
                    }

                    State.Gold -= 100;
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDollRoom:
                if (action == 0)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 5);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 2)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 15);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventFakeMerchant:
                if (action == 0)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventPotionCourier:
                if (action == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        RunRewardGenerator.AddPotion(
                            State,
                            RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                        );
                    }
                }
                else if (action == 1)
                {
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRanwidTheElder:
                if (action == 0)
                {
                    int slot = Array.FindIndex(State.PotionSlots, potion => potion != 0);
                    if (slot < 0)
                    {
                        return -1;
                    }

                    State.PotionSlots[slot] = 0;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    if (State.Gold < 100)
                    {
                        return -1;
                    }

                    State.Gold -= 100;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 2)
                {
                    if (State.Relics.Count <= 1)
                    {
                        return -1;
                    }

                    State.Relics.RemoveAt(1);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRelicTrader:
                if (action is >= 0 and <= 2)
                {
                    if (State.Relics.Count <= action + 1)
                    {
                        return -1;
                    }

                    State.Relics.RemoveAt(action + 1);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRoomFullOfCheese:
                if (action == 0)
                {
                    AddEventRewardCard();
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 14);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSlipperyBridge:
                if (action == 0)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 10);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventStoneOfAllTime:
                if (action == 0)
                {
                    int slot = Array.FindIndex(State.PotionSlots, potion => potion != 0);
                    if (slot < 0)
                    {
                        return -1;
                    }

                    State.PotionSlots[slot] = 0;
                    RunNonCombatEffects.GainMaxHp(State, 5);
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 10);
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSymbiote:
                if (action == 0)
                {
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTeaMaster:
                if (action == 0)
                {
                    if (State.Gold < 150)
                    {
                        return -1;
                    }

                    State.Gold -= 150;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    if (State.Gold < 150)
                    {
                        return -1;
                    }

                    State.Gold -= 150;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTheFutureOfPotions:
                if (action is >= 0 and <= 2)
                {
                    int seen = -1;
                    int slot = -1;
                    for (int i = 0; i < State.PotionSlots.Length; i++)
                    {
                        if (State.PotionSlots[i] == 0)
                        {
                            continue;
                        }

                        seen++;
                        if (seen == action)
                        {
                            slot = i;
                            break;
                        }
                    }

                    if (slot < 0)
                    {
                        return -1;
                    }

                    State.PotionSlots[slot] = 0;
                    AddEventRewardCard(upgraded: true);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventThisOrThat:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 7);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(55)
                    );
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWarHistorianRepy:
                if (action == 0)
                {
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWelcomeToWongos:
                if (action == 0)
                {
                    if (State.Gold < 100)
                    {
                        return -1;
                    }

                    State.Gold -= 100;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    if (State.Gold < 100)
                    {
                        return -1;
                    }

                    State.Gold -= 100;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 2)
                {
                    if (State.Gold < 100)
                    {
                        return -1;
                    }

                    State.Gold -= 100;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventAmalgamator:
                if (action == 0)
                {
                    TransformTwoMatchingCards(Effects.IC.StrikeIronclad);
                }
                else if (action == 1)
                {
                    TransformTwoMatchingCards(Effects.IC.DefendIronclad);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventBugslayer:
                if (action == 0)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(75)
                    );
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                    AddEventRewardCard(upgraded: true);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventColorfulPhilosophers:
                if (action is >= 0 and <= 2)
                {
                    AddEventRewardCard(upgraded: action == 2);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventColossalFlower:
                if (action == 0)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(125)
                    );
                }
                else if (action == 1)
                {
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventFieldOfManSizedHoles:
                if (action == 0)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 12);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventInfestedAutomaton:
                if (action == 0)
                {
                    RunRewardGenerator.EnterCardReward(State);
                    return 0;
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 10);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventLostWisp:
                if (action == 0)
                {
                    HealPlayer(RestHealAmount());
                }
                else if (action == 1)
                {
                    AddEventRewardCard(upgraded: true);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSpiritGrafter:
                if (action == 0)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                    RunNonCombatEffects.GainMaxHp(State, 3);
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                    RunNonCombatEffects.UpgradeFirstCard(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTheLanternKey:
                if (action == 0)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, false)
                    );
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(99)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventZenWeaver:
                if (action == 0)
                {
                    HealPlayer(12);
                }
                else if (action == 1)
                {
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.GainMaxHp(State, 5);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventBattlewornDummy:
                if (action is >= 0 and <= 2)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(40 + action * 20)
                    );
                    AddEventRewardCard(upgraded: action == 2);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventGraveOfTheForgotten:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 13);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunConstants.CursePlaceholderCard, false)
                    );
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(150)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventHungryForMushrooms:
                if (action == 0)
                {
                    RunNonCombatEffects.GainMaxHp(State, 7);
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 9);
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventReflections:
                if (action == 0)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action == 1)
                {
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRoundTeaParty:
                if (action == 0)
                {
                    HealPlayer(18);
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(80)
                    );
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTrial:
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 10);
                    AddEventRewardCard(upgraded: true);
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        EventGoldAmount(100)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTinkerTime:
                if (action == 0)
                {
                    if (!RunNonCombatEffects.UpgradeFirstCard(State))
                    {
                        return -1;
                    }
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.TransformFirstCard(State);
                }
                else if (action == 2)
                {
                    RunRewardGenerator.AddPotion(
                        State,
                        RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            default:
                if (action == 0)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 50);
                    RunRewardGenerator.AddPotion(State, 1);
                }
                else if (action == 1)
                {
                    if (State.PlayerHp >= State.PlayerMaxHp)
                    {
                        return -1;
                    }

                    State.PlayerHp = Math.Min(State.PlayerMaxHp, State.PlayerHp + 15);
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(
                            State.Rng.UpFront.NextItem(
                                RunRewardGenerator.IroncladRewardPool.ToArray()
                            ),
                            Upgraded: false
                        )
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
        }

        State.EventId = RunConstants.EventResultPending;
        return 0;
    }

    private int StepTransformSelect(int action, out bool terminal)
    {
        terminal = false;
        if (State.PendingRestUpgrade)
        {
            if (
                (uint)action >= (uint)State.Deck.Count
                || !RunConstants.IsRunCardUpgradable(State.Deck[action])
            )
            {
                return -1;
            }

            State.Deck[action] = State.Deck[action] with { Upgraded = true };
            State.PendingRestUpgrade = false;
            State.RestResultPending = true;
            State.Phase = RunPhase.Rest;
            return 0;
        }

        if (State.PendingSelfHelpBookEnchantType != 0)
        {
            if (!RunNonCombatEffects.ApplySelfHelpBookEnchantment(State, action))
            {
                return -1;
            }

            State.PendingSelfHelpBookEnchantType = 0;
            State.EventId = RunConstants.EventResultPending;
            State.Phase = RunPhase.Event;
            return 0;
        }

        if (State.TransformSelectedDeckIndex < 0)
        {
            int count = Math.Abs(State.TransformSelectedDeckIndex.Value);
            int relicId = State.Relics.LastOrDefault().DefId;
            if (relicId == RunConstants.RelicAstrolabe)
            {
                for (int i = 0; i < count && State.Deck.Count > 0; i++)
                {
                    RunNonCombatEffects.TransformCardAt(State, 0, State.Rng.Niche);
                    State.Deck[^1] = State.Deck[^1] with { Upgraded = true };
                }
            }
            else if (relicId == RunConstants.RelicEmptyCage)
            {
                for (int i = 0; i < count; i++)
                {
                    RunNonCombatEffects.RemoveLowestPriorityCard(State);
                }
            }
            State.TransformSelectedDeckIndex = null;
            return AdvanceAfterNode(out terminal);
        }

        if (State.TransformSelectedDeckIndex is null)
        {
            if ((uint)action >= (uint)State.Deck.Count)
            {
                return -1;
            }

            State.TransformSelectedDeckIndex = action;
            return 0;
        }

        RunNonCombatEffects.TransformCardAt(
            State,
            State.TransformSelectedDeckIndex.Value,
            State.Rng.Niche
        );
        State.TransformSelectedDeckIndex = null;
        return AdvanceAfterNode(out terminal);
    }

    private int RestHealAmount() =>
        Math.Max(1, (int)(State.PlayerMaxHp * 0.3))
        // Regal Pillow's ModifyRestSiteHealAmount adds its HealVar(15m) to whatever the
        // site was going to heal.
        + (
            Effects.RelicEffects.Has(State.Relics, Effects.RelicEffects.RegalPillow)
                ? Effects.RelicEffects.RegalPillowRestHeal
                : 0
        );

    private void HealPlayer(int amount)
    {
        State.PlayerHp = Math.Min(State.PlayerMaxHp, State.PlayerHp + amount);
    }

    private int EventGoldAmount(int baseAmount)
    {
        return Math.Max(0, baseAmount + State.Rng.UpFront.NextInt(-15, 16));
    }

    private void AddEventRewardCard(bool upgraded = false)
    {
        RunNonCombatEffects.AddCardToDeck(
            State,
            new CardInstance(
                State.Rng.UpFront.NextItem(RunRewardGenerator.IroncladRewardPool.ToArray()),
                upgraded
            )
        );
    }

    private void TransformTwoMatchingCards(int cardId)
    {
        for (int count = 0; count < 2; count++)
        {
            int index = State.Deck.FindIndex(card => Math.Abs(card.DefId) == cardId);
            if (index < 0)
            {
                return;
            }

            RunNonCombatEffects.TransformCardAt(State, index, State.PlayerRng.Transformations);
        }
    }

    private static void SetMask(Span<int> mask, int action)
    {
        if ((uint)action < (uint)mask.Length)
        {
            mask[action] = 1;
        }
    }

    private void WriteEventActionMask(Span<int> mask)
    {
        SetMask(mask, RunConstants.EventSkipAction);
        switch (State.EventId)
        {
            case RunConstants.EventUnrestSite:
                if (State.PlayerHp < State.PlayerMaxHp)
                {
                    SetMask(mask, 0);
                }

                if (State.PlayerMaxHp > 8)
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventAromaOfChaos:
                if (State.Deck.Count > 0)
                {
                    SetMask(mask, 0);
                }

                if (State.Deck.Any(RunConstants.IsRunCardUpgradable))
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventJungleMazeAdventure:
                if (State.PlayerHp > 18)
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventMorphicGrove:
                if (State.Gold > 0 && State.Deck.Count >= 2)
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventSelfHelpBook:
                SetSelfHelpBookMask(mask, 1, 0);
                SetSelfHelpBookMask(mask, 2, 1);
                SetSelfHelpBookMask(mask, 3, 2);
                break;
            case RunConstants.EventBrainLeech:
                SetMask(mask, 0);
                if (State.PlayerHp > 5)
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventTheLegendsWereTrue:
                SetMask(mask, 0);
                if (State.PlayerHp > 8 && State.PotionSlots.Any(potion => potion == 0))
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventDoorsOfLightAndDark:
                if (State.Deck.Any(RunConstants.IsRunCardUpgradable))
                {
                    SetMask(mask, 0);
                }

                if (State.Deck.Count > 0)
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventSunkenTreasury:
                SetMask(mask, 0);
                SetMask(mask, 1);
                break;
            case RunConstants.EventSymbiote:
                // Approach needs a card Corrupted can enchant, which it limits to
                // Attacks. Kill with Fire is always on the table.
                if (
                    State.Deck.Any(card =>
                        GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
                    )
                )
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventTeaMaster:
                if (State.Gold >= RunConstants.BoneTeaCost)
                {
                    SetMask(mask, 0);
                }

                if (State.Gold >= RunConstants.EmberTeaCost)
                {
                    SetMask(mask, 1);
                }

                // Tea of Discourtesy is free.
                SetMask(mask, 2);
                break;
            case RunConstants.EventRelicTrader:
                SetTradeMask(mask, State.Relics.Count(relic => IsTradableRelic(relic)));
                break;
            case RunConstants.EventTheFutureOfPotions:
                SetTradeMask(mask, State.PotionSlots.Count(potion => potion != 0));
                break;
            case RunConstants.EventLuminousChoir:
                SetMask(mask, 0);
                if (State.Gold >= RunNonCombatEffects.LuminousChoirTributeCost(State))
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventStoneOfAllTime:
                // Lift needs a potion to drink; Push needs a card Vigorous can enchant,
                // which Vigorous.CanEnchantCardType limits to Attacks. The emulator does
                // not model Vigorous itself, so "already enchanted" is not checked --
                // pinned approximation, and it only ever over-offers a card the game
                // would have skipped past.
                if (State.PotionSlots.Any(potion => potion != 0))
                {
                    SetMask(mask, 0);
                }

                if (
                    State.Deck.Any(card =>
                        GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
                    )
                )
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventRanwidTheElder:
                // Give a potion, give gold, give a relic. Gold is always on the table;
                // the other two need something to give, and the relic has to be one the
                // game will take -- Burning Blood is a Starter relic, so a fresh run can
                // only ever pick the gold.
                if (State.PotionSlots.Any(potion => potion != 0))
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                if (State.Relics.Any(relic => IsTradableRelic(relic)))
                {
                    SetMask(mask, 2);
                }

                break;
            case RunConstants.EventWelcomeToWongos:
                // A shop with three price tags and a door. The costs are the event's own
                // DynamicVars, not the shop's pricing.
                if (State.Gold >= RunConstants.WongosBargainBinCost)
                {
                    SetMask(mask, 0);
                }

                if (State.Gold >= RunConstants.WongosFeaturedItemCost)
                {
                    SetMask(mask, 1);
                }

                if (State.Gold >= RunConstants.WongosMysteryBoxCost)
                {
                    SetMask(mask, 2);
                }

                // Leave, which is action 3 and therefore already set above.
                SetMask(mask, 3);
                break;
            case RunConstants.EventResultPending:
                SetMask(mask, 0);
                break;
            default:
            {
                // An event with no bespoke case above has no option gating either, so
                // every option it offers is takeable. What it must not do is offer
                // options the event does not have: the old fallback set a flat 0..3,
                // which let an agent choose a third option at the many Act 1 events
                // that only have two, and a fourth at every event that is not Welcome
                // to Wongos.
                int optionCount = GeneratedData.EventOptions.CountFor(State.EventId);
                for (
                    int i = 0;
                    i < (optionCount > 0 ? optionCount : RunConstants.EventSkipAction);
                    i++
                )
                {
                    SetMask(mask, i);
                }

                break;
            }
        }
    }

    /// <summary>
    /// The game's <c>RelicModel.IsTradable</c> for a relic actually in the run: the
    /// definition's own answer, plus the two run-state exclusions it also applies.
    /// </summary>
    /// <summary>
    /// Both trading events offer one option per thing they can take, capped at three,
    /// and fall back to a single Proceed when there is nothing to trade.
    /// </summary>
    private static void SetTradeMask(Span<int> mask, int tradableCount)
    {
        int offered = Math.Min(3, tradableCount);
        if (offered == 0)
        {
            SetMask(mask, 0);
            return;
        }

        for (int i = 0; i < offered; i++)
        {
            SetMask(mask, i);
        }
    }

    private bool IsTradableRelic(RelicInstance relic) =>
        GeneratedData.Relics.Get(relic.DefId).IsTradable
        && !State.UsedUpRelics.Contains(relic.DefId);

    private void SetSelfHelpBookMask(Span<int> mask, int enchantType, int action)
    {
        if (
            State.Deck.Any(card =>
                RunNonCombatEffects.CanApplySelfHelpBookEnchantment(card, enchantType)
            )
        )
        {
            SetMask(mask, action);
        }
    }

    private void WriteRewardActionMask(Span<int> mask)
    {
        int action = 0;
        if (State.RewardGold != 0)
        {
            SetMask(mask, action++);
        }

        if (State.RewardPotion != 0)
        {
            SetMask(mask, action++);
        }

        if (State.RelicReward != 0)
        {
            SetMask(mask, action++);
        }

        if (State.RewardCardPending)
        {
            SetMask(mask, action++);
        }

        for (int i = 0; i < State.PendingOtherCharacterCardRewards; i++)
        {
            SetMask(mask, action++);
        }
    }
}
