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
        State.ShopRemovalUsedThisVisit = false;
        State.TransformSelectedDeckIndex = null;
        RunNonCombatEffects.ClearDeckSelection(State);
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
        // Neow's room is a RoomType.Event (MapPointType.Ancient maps to it), and
        // RunManager marks EVERY room it enters -- so RoomSet.eventsVisited is already 1
        // by the time the run reaches its first real event, without anything having been
        // pulled from the list. NextEvent is events[eventsVisited % count], so the first
        // event a run actually meets is events[1]. Starting the cursor at 0 handed out
        // the entry the game had already stepped past.
        State.EventSequenceIndex = 1;
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

        // Slither re-rolls its card's cost off Rng.CombatEnergyCosts every time the card
        // is drawn, and that stream spans the RUN like every other named one. This was the
        // only stream built without fast-forwarding to the run's position and the only one
        // never written back after the combat (see SyncRunRngFromCombat), so it restarted
        // at zero for every fight: two Slither draws in two different combats read the
        // same value, where the game reads consecutive ones. A live capture whose Wood
        // Carvings enchanted a Bash paid 1 energy for it where the emulator paid 2.
        var energyCostRng = new CountingRandom(State.Rng.CombatEnergyCosts.RawSeed);
        for (int i = 0; i < State.Rng.CombatEnergyCosts.CallCount; i++)
        {
            energyCostRng.Next();
        }

        combat.EnergyCostRng = energyCostRng;

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

        // `Rng.CombatOrbGeneration` -- Chaos and Trash to Treasure roll their orb type on
        // it. `RunRngSet` has carried the stream since it was written; nothing had reached
        // for it, because the one card that needed it was rolling on the combat rng.
        var orbGenerationRng = new CountingRandom(State.Rng.CombatOrbs.RawSeed);
        for (int i = 0; i < State.Rng.CombatOrbs.CallCount; i++)
        {
            orbGenerationRng.Next();
        }

        combat.OrbGenerationRng = orbGenerationRng;

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

        // `Planisphere.AfterRoomEntered` asks about `CurrentMapPoint.PointType`, not about
        // the room -- so it pays out on whatever the "?" turned out to be, a fight
        // included. Fired here, before the room is set up, because every branch below
        // either starts a combat or changes phase and there is no single point after them.
        Effects.RelicEffects.ApplyAfterRoomEntered(
            State,
            isRestSite: resolvedNodeType == RunConstants.NodeRest,
            cameFromUnknown: true
        );

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
            case RunConstants.NodeAncient:
                // The act's ancient, which behaves exactly as Neow does: a screen of
                // blessings, then one more Proceed before the map.
                State.LastResolvedRoomType = RunConstants.NodeEvent;
                ApplyAncientHeal();
                EnterActAncient();
                return 0;
            default:
                EnterMapPhase();
                return 0;
        }
    }

    private int EnterEventRoom()
    {
        // An event room is not a Monster room, and anything a fight-shaped event starts
        // from here inherits that. Left unset, the type stayed at whatever the last map
        // node resolved to, so Punch Off's fight could be counted as an ordinary combat.
        State.LastResolvedRoomType = RunConstants.NodeEvent;
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

        // `JuzuBracelet.ModifyUnknownMapPointRoomTypes` drops Monster from the SET before
        // the odds are rolled, so its probability mass redistributes across what is left
        // rather than the roll being taken again.
        if (Effects.RelicEffects.ForbidsUnknownMonsterRooms(State))
        {
            allowedRoomTypes.Remove(RunConstants.NodeNormal);
        }

        int roomType = allowedRoomTypes.Contains(RunConstants.NodeEvent)
            ? RunConstants.NodeEvent
            : allowedRoomTypes.Min();
        // UnknownMapPointOdds.Roll draws a FLOAT and accumulates the odds as floats:
        // `float num2 = _rng.NextFloat()` compared against a float running total. The
        // draw is the same value either way, but the comparison is not -- a roll sitting
        // within a float's precision of a threshold falls one side in float arithmetic
        // and the other in double, which resolves a "?" to a different room entirely.
        float roll = State.Rng.UnknownMapPoint.NextFloat();
        float cumulative = 0f;
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

            cumulative += (float)odds;
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

                if (State.Relics.Any(relic => relic.DefId == RunConstants.RelicPaelsGrowth))
                {
                    SetMask(mask, RunConstants.RestCloneAction);
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
                    !State.ShopRemovalUsedThisVisit
                    && State.Gold >= State.ShopCosts[RunConstants.ShopRemoveAction]
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

            case RunPhase.BundleSelect:
                for (int i = 0; i < BundleCount; i++)
                {
                    SetMask(mask, i);
                }

                // Confirm is only offered once something is highlighted, which is what
                // the screen does -- its button is dead until a bundle is picked.
                if (State.SelectedBundle >= 0)
                {
                    SetMask(mask, RunConstants.BundleConfirmAction);
                }

                break;

            case RunPhase.TransformSelect:
                if (State.PendingOfferCards.Length > 0)
                {
                    // The grid is the offer, so the action indexes the offer, not the
                    // deck.
                    for (int i = 0; i < State.PendingOfferCards.Length; i++)
                    {
                        SetMask(mask, i);
                    }
                }
                else if (State.PendingRestUpgrade)
                {
                    for (int i = 0; i < State.Deck.Count; i++)
                    {
                        if (RunConstants.IsRunCardUpgradable(State.Deck[i]))
                        {
                            SetMask(mask, i);
                        }
                    }
                }
                else if (State.PendingSelectionKind != DeckSelection.None)
                {
                    for (int i = 0; i < State.Deck.Count; i++)
                    {
                        if (RunNonCombatEffects.CanSelectCard(State, i))
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

            case RunPhase.CrystalSphere when State.CrystalSphere is not null:
                // Cells that would uncover nothing are left out. The game lets a
                // divination be spent on clear ground and simply wastes it; nothing an
                // agent could learn makes that worth offering.
                for (int cell = 0; cell < RunConstants.CrystalSphereCells; cell++)
                {
                    int x = cell / RunConstants.CrystalSphereSize;
                    int y = cell % RunConstants.CrystalSphereSize;
                    if (State.CrystalSphere.WouldUncover(x, y, CrystalSphereTool.Big))
                    {
                        SetMask(mask, cell);
                    }

                    if (State.CrystalSphere.WouldUncover(x, y, CrystalSphereTool.Small))
                    {
                        SetMask(mask, RunConstants.CrystalSphereSmallToolAction + cell);
                    }
                }

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
    /// Point the in-combat streams at the resampled run's streams, reshuffle the region of
    /// the draw pile the player has not seen, and move whatever the Crystal Sphere's fog is
    /// still hiding.
    /// </summary>
    /// <remarks>
    /// The combat streams keep their call counts: SyncAfterCombat hands those counts
    /// back to the run streams when the fight ends, so the two have to stay in step
    /// even though the values behind them have changed.
    /// </remarks>
    private void ResampleHiddenState(Random rng)
    {
        State.CrystalSphere?.ResampleUnseenItems(rng);

        var combat = State.ActiveCombat;
        if (combat is null)
        {
            return;
        }

        combat.ShuffleRng = Restream(combat.ShuffleRng, State.Rng.Shuffle);
        combat.NicheHpRng = Restream(combat.NicheHpRng, State.Rng.Niche);
        combat.TargetRng = Restream(combat.TargetRng, State.Rng.CombatTargets);
        combat.CardSelectionRng = Restream(combat.CardSelectionRng, State.Rng.CombatCardSelection);
        combat.EnergyCostRng = Restream(combat.EnergyCostRng, State.Rng.CombatEnergyCosts);
        combat.CardGenerationRng = Restream(
            combat.CardGenerationRng,
            State.Rng.CombatCardGeneration
        );
        combat.PotionGenerationRng = Restream(
            combat.PotionGenerationRng,
            State.Rng.CombatPotionGeneration
        );
        combat.OrbGenerationRng = Restream(combat.OrbGenerationRng, State.Rng.CombatOrbs);
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
        for (int i = 0; i < RunConstants.MapChoices; i++)
        {
            obs[offset + RunConstants.MapNodeTypeObsOffset + i] = State.MapNodeTypes[i];
            obs[offset + RunConstants.MapChoiceObsOffset + i] = State.MapChoices[i];
        }

        obs[offset + RunConstants.RelicRewardObsOffset] = State.RelicReward;
        obs[offset + RunConstants.CurrentEventObsOffset] = State.EventId;
        for (int i = 0; i < RunConstants.PotionObsSlots; i++)
        {
            obs[offset + RunConstants.PotionObsOffset + i] = potionSlots[i];
        }

        WriteDeckObservation(obs[(offset + RunConstants.DeckObsOffset)..]);
        WriteRelicObservation(obs[(offset + RunConstants.RelicObsOffset)..]);
        WriteShopObservation(obs[(offset + RunConstants.ShopObsOffset)..]);
    }

    /// <summary>
    /// The deck, card by card. Everything before this reported <c>Deck.Count</c> and
    /// nothing else, which left every decision the run is actually about -- which card
    /// reward to take, what to buy, what to upgrade at a fire, what to transform --
    /// unlearnable: the agent chose between three cards without being told what the other
    /// twenty in its deck were.
    ///
    /// Persistent identity only. <c>CostForCombat</c>, <c>BonusDamage</c>, <c>Retain</c>
    /// and <c>FreeThisTurn</c> are combat-local and belong to the combat observation; what
    /// survives a fight is the card, whether it is upgraded, and its one enchantment with
    /// the amount it was applied at.
    /// </summary>
    private void WriteDeckObservation(Span<int> obs)
    {
        int slots = Math.Min(State.Deck.Count, RunConstants.MaxObservedDeck);
        for (int i = 0; i < slots; i++)
        {
            var card = State.Deck[i];
            int at = i * RunConstants.DeckSlotSize;
            obs[at] = card.DefId;
            obs[at + 1] = card.Upgraded ? 1 : 0;
            obs[at + 2] = (int)card.Enchantment;
            obs[at + 3] = card.EnchantAmount;
        }
    }

    /// <summary>
    /// The relics, in the order they were picked up. The counter is on the slot because
    /// half of what a relic is worth is how much of it is left -- a Silver Crucible with no
    /// charges is a different relic from one with three -- and a used-up relic is reported
    /// as such, since the run keeps carrying it after it stops doing anything.
    /// </summary>
    /// <summary>
    /// The merchant's board, slot by slot, priced. The scalars used to carry three of the
    /// seven cards and none of the prices at all -- so an agent could buy shop slot 5
    /// without ever being shown what was on it, and could not tell a 50-gold card from a
    /// 300-gold one on any slot. Every purchase decision a shop is for turns on the price.
    ///
    /// Indexed by the action that buys the slot, so this reads straight against the shop's
    /// action mask. The removal service is slot 13 and has no item, so its id stays 0.
    /// </summary>
    private void WriteShopObservation(Span<int> obs)
    {
        for (int action = 0; action < RunConstants.ShopSlots; action++)
        {
            int at = action * RunConstants.ShopSlotSize;
            obs[at] = action switch
            {
                < 7 => State.ShopCards[action],
                < 10 => State.ShopRelics[action - 7],
                < 13 => State.ShopPotions[action - 10],
                _ => 0,
            };
            obs[at + 1] = State.ShopCosts[action];
        }
    }

    private void WriteRelicObservation(Span<int> obs)
    {
        int slots = Math.Min(State.Relics.Count, RunConstants.MaxObservedRelics);
        for (int i = 0; i < slots; i++)
        {
            var relic = State.Relics[i];
            int at = i * RunConstants.RelicSlotSize;
            obs[at] = relic.DefId;
            obs[at + 1] = relic.Counter;
            obs[at + 2] = State.UsedUpRelics.Contains(relic.DefId) ? 1 : 0;
        }
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
        int status = StepInner(action, targetEnemyIndex, out reward, out terminal, out truncated);

        // A run ends the moment the player runs out of HP, wherever that happens. Combat
        // had its own path; nothing outside combat did, so an event that took the last of
        // the player's HP left the run walking around at 0 and an agent learned that a
        // lethal option was free. Guarding here rather than at each site means no new
        // effect can forget it.
        if (status == 0 && !terminal && State.PlayerHp <= 0)
        {
            State.PlayerHp = 0;
            State.LastPlayerWon = false;
            State.Phase = RunPhase.Complete;
            terminal = true;
        }

        return status;
    }

    private int StepInner(
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
                // Every blessing ends in AncientEventModel.Done(), which is a
                // SetEventFinished -- a page whose only option is Proceed. Only
                // Kaleidoscope was given one here, because it was the one whose reward
                // screen made the missing page visible; the rest went straight to the
                // map and swallowed an action the run really takes.
                State.NeowAwaitingProceed = true;
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
                    // `Pantograph.BeforeCombatStart` reads the ROOM type, so the heal is a
                    // boss-room thing rather than a boss-encounter one.
                    if (nodeType == RunConstants.NodeBoss)
                    {
                        Effects.RelicEffects.ApplyBeforeBossCombat(State);
                    }

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
                    Effects.RelicEffects.ApplyAfterRoomEntered(
                        State,
                        isRestSite: true,
                        cameFromUnknown: false
                    );
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
                case RunConstants.NodeAncient:
                    // The act's ancient, which behaves as Neow does: a screen of
                    // blessings, then one more Proceed before the map. This is the
                    // dispatch the map TRAVEL goes through -- there is a second one for
                    // rooms entered another way, and the Ancient needs both.
                    State.LastResolvedRoomType = RunConstants.NodeEvent;
                    ApplyAncientHeal();
                    EnterActAncient();
                    break;
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
                    RunNonCombatEffects.TriggerFishingRod(State);
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

        if (State.Phase == RunPhase.CrystalSphere)
        {
            return StepCrystalSphere(action, out terminal);
        }

        if (State.Phase == RunPhase.BundleSelect)
        {
            return StepBundleSelect(action, out terminal);
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

    /// <summary>
    /// <c>AncientEventModel.BeforeEventStarted</c>: entering an ancient heals the player
    /// toward full, and at A8's <c>WearyTraveler</c> only 80% of what they are missing.
    /// </summary>
    /// <remarks>
    /// This is the rule behind the run's own opening HP. Neow calls
    /// <c>SetCurrentHpInternal(0)</c> first, so the heal is 80% of the whole 80 max — 64,
    /// which the emulator has always hardcoded in <c>Reset</c> as a starting constant
    /// without knowing where it came from. Every act after the first walks onto its own
    /// ancient at whatever HP it finished the last one with, and is topped up the same
    /// way: a capture crosses into act 2 on 264/280 and stands on Pael at 276.
    /// </remarks>
    /// <summary>
    /// Put the act's own ancient on screen, with the three blessings it offers.
    /// </summary>
    /// <remarks>
    /// Neow keeps the options generated at run start — it has its own generator and its
    /// own shape. Every act after draws one of Orobas, Pael or Tezcatara (or Darv, the
    /// one shared ancient) and offers three, one from each of its pools.
    /// </remarks>
    private void EnterActAncient()
    {
        if (State.Ancient != RunConstants.AncientNeow)
        {
            Array.Clear(State.NeowOptions);
            int[] options = RunNonCombatEffects.GenerateAncientOptions(State, State.Ancient);
            for (int i = 0; i < options.Length && i < State.NeowOptions.Length; i++)
            {
                State.NeowOptions[i] = options[i];
            }
        }

        State.Phase = RunPhase.Ancient;
    }

    private void ApplyAncientHeal()
    {
        int missing = State.PlayerMaxHp - State.PlayerHp;
        if (missing <= 0)
        {
            return;
        }

        State.PlayerHp += (int)(missing * WearyTravelerHealFraction);
    }

    /// <summary>A8 has WearyTraveler, which pays 80% of a full heal.</summary>
    private const double WearyTravelerHealFraction = 0.8;

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
            // LostCoffer.AfterObtained is a RewardsCmd.OfferCustom of TWO rewards -- a
            // three-card CardReward and a PotionReward -- so both sit on a rewards SCREEN
            // for the player to claim, the same shape as Kaleidoscope. Handing the card
            // reward straight over skipped the screen, and with it the claim the run
            // really makes.
            RunRewardGenerator.PopulateCardReward(State);
            State.RewardCardPending = true;
            State.RewardPotion = RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards);
            State.NeowAwaitingProceed = true;
            State.Phase = RunPhase.RelicReward;
            return;
        }
        if (followUp == RunFollowUp.PreRolledCardReward)
        {
            // The grid is already up; Neow stays on screen for its Proceed once the choice
            // is made, the way it does after every other blessing.
            State.NeowAwaitingProceed = true;
            return;
        }
        if (followUp == RunFollowUp.BonusRelicRewards)
        {
            // Neow's Bones: two relic rewards on one screen, claimed one at a time, and
            // the curse only once both are gone. Neow itself stays on screen afterwards
            // the way it does for every other blessing.
            RunRewardGenerator.OfferNextBonusRelic(State);
            State.NeowAwaitingProceed = true;
            State.Phase = RunPhase.RelicReward;
            return;
        }
        if (followUp == RunFollowUp.BundleSelect)
        {
            // Neow stays on screen for its Proceed once the bundle is taken, the way it
            // does after every other blessing.
            State.NeowAwaitingProceed = true;
            State.Phase = RunPhase.BundleSelect;
            return;
        }
        if (followUp == RunFollowUp.BonusCardOffers)
        {
            // The first offer goes onto the screen and the rest queue behind it; Neow
            // stays up for its Proceed once they are all answered, the way it does after
            // every other blessing.
            RunRewardGenerator.OfferFirstPendingCardOffer(State);
            State.RewardCardPending = true;
            State.NeowAwaitingProceed = true;
            State.Phase = RunPhase.RelicReward;
            return;
        }
        if (followUp == RunFollowUp.TransformSelect)
        {
            // Neow stays on screen for its Proceed once the selection is answered, the way
            // it does after every other blessing.
            State.NeowAwaitingProceed = true;
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

    /// <summary>
    /// Rewards draws a Neow relic's pickup makes in the game that the emulator's own
    /// model of it does not.
    /// </summary>
    /// <remarks>
    /// A standing debt, not a design. Hefty Tablet and Lead Paperweight both build their
    /// offer with <c>CardFactory.CreateForReward</c>, which draws from the Rewards stream;
    /// the emulator rolls their cards off <c>Rng.UpFront</c> instead, so the count here is
    /// what keeps everything downstream aligned. Modelling either properly means rolling
    /// its cards off Rewards and deleting its row -- and neither has a live capture yet,
    /// so neither has a way to be checked.
    ///
    /// <para>
    /// Phial Holster used to have a row of 4 and no longer does: its potions come off
    /// <c>Rng.CombatPotionGeneration</c> and its slot grant is free, so the game makes NO
    /// Rewards draws for it. Those four put the next combat's gold reward at stream
    /// position 5 where the game had it at 1 -- 7 gold against the capture's 15 -- and
    /// every card reward after it read from the wrong place too.
    /// </para>
    /// </remarks>
    private void AdvanceRewardRngForNeowRelic(int relicId)
    {
        int advances = relicId switch
        {
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

    /// <summary>
    /// Goopy grows on play and the game bumps the DECK version's amount as well as the
    /// combat card's — so the block it gains is permanent for the rest of the run.
    /// </summary>
    /// <remarks>
    /// The emulator has no link from a combat card back to the deck card it was copied
    /// from, so the amounts are matched by card identity: for each (id, upgraded) the
    /// combat's Goopy amounts are written onto the run deck's, largest first. The combat
    /// deck IS the run deck copied, so the two multisets agree unless a card was added or
    /// removed mid-fight — and a card added mid-fight is not in the run deck to grow.
    /// </remarks>
    private void CarryGoopyGrowthToTheDeck(CombatState combat)
    {
        var amounts = combat
            .AllCards()
            .Where(card => card.Enchantment == Enchantment.Goopy)
            .GroupBy(card => (card.DefId, card.Upgraded))
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(
                    group.Select(card => card.EnchantAmount).OrderByDescending(a => a)
                )
            );
        if (amounts.Count == 0)
        {
            return;
        }

        foreach (
            int index in Enumerable
                .Range(0, State.Deck.Count)
                .Where(i => State.Deck[i].Enchantment == Enchantment.Goopy)
                .OrderByDescending(i => State.Deck[i].EnchantAmount)
        )
        {
            var card = State.Deck[index];
            if (amounts.TryGetValue((card.DefId, card.Upgraded), out var queue) && queue.Count > 0)
            {
                State.Deck[index] = card with { EnchantAmount = queue.Dequeue() };
            }
        }
    }

    /// <summary>The post-combat sync, exposed so a test can drive it without a fight.</summary>
    internal void SyncAfterCombatForTest() => SyncAfterCombat();

    private void SyncAfterCombat()
    {
        if (State.ActiveCombat is null)
        {
            return;
        }

        Effects.RelicEffects.CollectUsedUpRelics(State.ActiveCombat, State.UsedUpRelics);
        CarryGoopyGrowthToTheDeck(State.ActiveCombat);
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

        if (State.ActiveCombat.EnergyCostRng is not null)
        {
            State.Rng.CombatEnergyCosts.AdvanceToCallCount(
                State.ActiveCombat.EnergyCostRng.CallCount
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

        // Prayer Wheel and White Star each add a WHOLE extra CardReward, so the screen
        // comes back rather than the run moving on. Rolled here, when the previous offer
        // is answered, because `CardReward.Populate()` draws its three when the screen is
        // BUILT -- not when the relic added it.
        if (State.ExtraCardRewardsOwed > 0)
        {
            State.ExtraCardRewardsOwed--;
            RunRewardGenerator.PopulateCardReward(State);
            return 0;
        }

        if (State.ReturnToRewardScreenAfterCardReward)
        {
            State.ReturnToRewardScreenAfterCardReward = false;
            if (State.NeowAwaitingProceed && !RunRewardGenerator.HasPendingRewards(State))
            {
                State.Phase = RunPhase.Ancient;
                return 0;
            }

            // Brain Leech's colourless reward belongs to the EVENT, the same way Neow's
            // does: once the screen is empty the run goes back to the event's finished
            // page, not to an empty rewards screen. The check for this sat below, after
            // the return here, so it never ran.
            if (
                State.EventId == RunConstants.EventBrainLeech
                && !RunRewardGenerator.HasPendingRewards(State)
            )
            {
                State.EventId = RunConstants.EventResultPending;
                State.Phase = RunPhase.Event;
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
            // It still LEAVES the screen the same way answering it does: this used to go
            // straight to AdvanceAfterRelicReward and so walked past the returns below,
            // which is invisible for Neow (its rewards are WithSkippingDisallowed) and
            // wrong for every event, whose second queued potion is declined far more often
            // than it is taken.
            State.PendingPotionRewards.Clear();
            return LeaveRewardScreen(out terminal);
        }

        if (!RunRewardGenerator.HasPendingRewards(State))
        {
            if (action is 0 or RunConstants.RewardSkipAction)
            {
                // Neow stays on screen once its rewards are answered, waiting for one
                // more Proceed -- the same return the card-reward path already made, and
                // the only one a relic-only screen (Neow's Bones) could not. Advancing
                // straight to the map from here skips a decision the run really makes.
                return LeaveRewardScreen(out terminal);
            }

            return -1;
        }

        if (!RunRewardGenerator.ClaimRewardAtIndex(State, action))
        {
            return -1;
        }

        OfferNextRestPotion();
        // The claim that empties the screen returns to Neow by itself -- a live capture's
        // second relic lands and the very same snapshot is already back on the ancient,
        // with no separate action between them. Only when the claim did not open a screen
        // of its own: a card reward sets its own phase and owns the return.
        if (
            State.Phase == RunPhase.RelicReward
            && State.NeowAwaitingProceed
            && !RunRewardGenerator.HasPendingRewards(State)
        )
        {
            State.Phase = RunPhase.Ancient;
        }
        else if (
            State.Phase == RunPhase.RelicReward
            && State.EventAwaitingProceed
            && !RunRewardGenerator.HasPendingRewards(State)
        )
        {
            State.EventAwaitingProceed = false;
            State.EventId = RunConstants.EventResultPending;
            State.Phase = RunPhase.Event;
        }

        return 0;
    }

    /// <summary>
    /// RewardsCmd.OfferCustom with potions: the run goes to the reward screen and the
    /// player may decline, or drop a held potion to make room. Anything past the first is
    /// queued, because the screen carries one at a time. A 0 is rolled when it reaches
    /// the screen.
    /// </summary>
    /// <summary>
    /// The <c>AfterRestSiteHeal</c> hook. Stone Humidifier is its only holder here:
    /// <c>MaxHpVar(5)</c> through <c>CreatureCmd.GainMaxHp</c>, and it ignores the hook's
    /// <c>isMimicked</c> flag, so an event that heals you like a rest pays it too.
    /// </summary>
    /// <summary>
    /// Leave the rewards screen for wherever opened it.
    /// </summary>
    /// <remarks>
    /// Neow stays on screen once its rewards are answered, waiting for one more Proceed.
    /// An event does the same for its own reason: every event that hands out rewards
    /// awaits <c>RewardsCmd.OfferCustom</c> and calls <c>SetEventFinished</c> on the line
    /// below, so the result page is shown once the screen is done with. Only a screen
    /// nobody is waiting on advances to the map.
    /// </remarks>
    private int LeaveRewardScreen(out bool terminal)
    {
        terminal = false;
        if (State.NeowAwaitingProceed)
        {
            State.Phase = RunPhase.Ancient;
            return 0;
        }

        if (State.EventAwaitingProceed)
        {
            State.EventAwaitingProceed = false;
            State.EventId = RunConstants.EventResultPending;
            State.Phase = RunPhase.Event;
            return 0;
        }

        return AdvanceAfterRelicReward(out terminal);
    }

    private void ApplyAfterRestSiteHeal()
    {
        if (State.Relics.Any(relic => relic.DefId == RunConstants.RelicStoneHumidifier))
        {
            RunNonCombatEffects.GainMaxHp(State, 5);
        }
    }

    private void OfferPotionRewards(params int[] potionIds)
    {
        // Every caller is an event branch, and every one of those events awaits the offer
        // and calls SetEventFinished on the next line -- so the screen owes the event a
        // return. Read off the phase rather than passed in, so a new event that offers
        // rewards gets the return without having to remember to ask for it.
        State.EventAwaitingProceed = State.Phase == RunPhase.Event;
        State.RewardGold = 0;
        State.RelicReward = 0;
        State.RewardPotion = 0;
        State.PendingPotionRewards.Clear();
        State.PendingPotionRewards.AddRange(potionIds);
        OfferNextRestPotion();
        State.Phase = RunPhase.RelicReward;
    }

    /// <summary>
    /// Moves the next queued potion onto the reward screen, if the screen is free. A
    /// queued 0 is rolled here rather than when it was queued, which is where
    /// PotionReward.Populate rolls it.
    /// </summary>
    private void OfferNextRestPotion()
    {
        if (State.PendingPotionRewards.Count == 0 || State.RewardPotion != 0)
        {
            return;
        }

        int potionId = State.PendingPotionRewards[0];
        State.PendingPotionRewards.RemoveAt(0);
        State.RewardPotion =
            potionId != 0
                ? potionId
                : RunRewardGenerator.NextPotion(State, State.PlayerRng.Rewards);
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
            if (State.ShopRemovalUsedThisVisit || State.Gold < cost || State.Deck.Count <= 1)
            {
                return -1;
            }

            // The merchant's service opens a removal SCREEN — which card goes is the
            // player's decision, and the whole of what the gold buys. This used to call
            // RemoveLowestPriorityCard, an emulator-invented preference order that starts
            // with Ascender's Bane, a card the game will not even offer.
            //
            // Open BEFORE charging: a deck with nothing the screen would take is not a
            // sale, and taking the gold for a screen that never appears is worse than
            // refusing the click.
            if (
                !RunNonCombatEffects.BeginDeckSelection(
                    State,
                    DeckSelection.Remove,
                    0,
                    count: 1,
                    returnTo: SelectionReturn.Shop
                )
            )
            {
                return -1;
            }

            State.Gold -= cost;
            State.ShopRemovalsUsed++;
            State.ShopRemovalUsedThisVisit = true;
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
            // RunManager.EnterNextAct: a boss is the end of the RUN only in the last act.
            // Anywhere else it is the end of an ACT, and the run walks onto the next act's
            // map — a live capture proceeds from the act 1 boss reward straight onto act
            // 2's map, still on floor 17. Treating every boss as terminal is what made act
            // 2 unreachable.
            if (EnterNextAct())
            {
                terminal = false;
                return 0;
            }

            State.Phase = RunPhase.Complete;
            State.LastPlayerWon = true;
            terminal = true;
            return 0;
        }
        return AdvanceAfterNode(out terminal);
    }

    /// <summary>
    /// Move to the next act, if there is one, and land on its map.
    /// </summary>
    /// <remarks>
    /// Shared by the real boss path and the debug hook, deliberately: reaching act 2 the
    /// honest way costs a buffed run that wins a boss fight, several minutes a go, so
    /// every act-2 test would otherwise be gated behind completing act 1. The debug hook
    /// makes it a couple of seconds — and because both routes are this one function, the
    /// shortcut cannot drift from the thing it is standing in for.
    /// </remarks>
    public bool EnterNextAct()
    {
        if (!RunMapGenerator.AdvanceToNextAct(State))
        {
            return false;
        }

        RunRewardGenerator.ClearRewardScreen(State);
        // Through EnterMapPhase, not by setting the phase: the options have to be
        // refreshed too, and on a new act the only one is the act's starting ancient.
        EnterMapPhase();
        return true;
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
            ApplyAfterRestSiteHeal();
            // Tiny Mailbox's TryModifyRestSiteHealRewards adds two PotionRewards to the
            // rest, and a reward is offered rather than given: the player chooses whether
            // to take it, and may drop a held potion to make room.
            if (Effects.RelicEffects.Has(State.Relics, Effects.RelicEffects.TinyMailbox))
            {
                State.PendingPotionRewards.AddRange([0, 0]);
                OfferNextRestPotion();
                State.Phase = RunPhase.RelicReward;
                return 0;
            }

            State.RestResultPending = true;
            return 0;
        }
        if (
            action == RunConstants.RestCloneAction
            && State.Relics.Any(relic => relic.DefId == RunConstants.RelicPaelsGrowth)
        )
        {
            // CloneRestSiteOption: one copy of every Clone-enchanted card, added to the
            // deck. This is the whole of what the Clone enchantment does — it overrides
            // nothing in a fight — so a run holding Pael's Growth and never resting gets
            // nothing from it at all.
            var copies = State.Deck.Where(card => card.Enchantment == Enchantment.Clone).ToList();
            foreach (var card in copies)
            {
                RunNonCombatEffects.AddCardToDeck(State, card);
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
                        new CardInstance(
                            RunNonCombatEffects.NamedCard("PoorSleep"),
                            Upgraded: false
                        )
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
                // Both options are a choice: which card is transformed, which upgraded.
                if (action is 0 or 1)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        action == 0 ? DeckSelection.TransformToRandom : DeckSelection.Upgrade,
                        0,
                        eventEntry: action == 0 ? "AROMA_OF_CHAOS" : null
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventJungleMazeAdventure:
                // Both purses are rolled with NextFloat, not fixed: 150 and 50, each
                // shifted by NextFloat(-15, 15) off the event's own stream.
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 18);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.JungleMazeSoloGold(State)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.JungleMazeJoinForcesGold(State)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventMorphicGrove:
                // Group transforms TWO cards the player picks.
                if (action == 0)
                {
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.TransformToRandom,
                            0,
                            count: 2,
                            eventEntry: "MORPHIC_GROVE"
                        )
                    )
                    {
                        return -1;
                    }

                    State.Gold = 0;
                    return 0;
                }

                if (action == 1)
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
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)RunNonCombatEffects.SelfHelpBookEnchantment(action)
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDoorsOfLightAndDark:
                if (action == 0)
                {
                    RunNonCombatEffects.UpgradeTwoRandomCardsForLightDoor(State);
                    State.EventId = RunConstants.EventResultPending;
                    return 0;
                }
                if (action == 1)
                {
                    // The Dark door removes one card the player picks. Light stays a
                    // roll: it StableShuffles the upgradable cards and takes two.
                    return RunNonCombatEffects.BeginDeckSelection(State, DeckSelection.Remove, 0)
                        ? 0
                        : -1;
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
                    // The big chest is paid for with Greed.
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.SunkenTreasuryLargeChestGold(State)
                    );
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunNonCombatEffects.NamedCard("Greed"), Upgraded: false)
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
                    // Share Knowledge rolls five cards through the reward machinery and
                    // opens a grid to keep one. The emulator used to pick a card itself,
                    // off the map-generation stream at that, which is neither the right
                    // choice nor the right stream.
                    State.PendingOfferCards = RunRewardGenerator.GenerateEventOfferCards(
                        State,
                        RunConstants.BrainLeechCardChoices,
                        RunRewardGenerator.IroncladRewardPool
                    );
                    State.PendingOfferPicks = 1;
                    State.Phase = RunPhase.TransformSelect;
                    return 0;
                }
                else if (action == 1)
                {
                    // Rip offers a COLORLESS card reward -- three cards rolled from the
                    // colourless pool at the usual odds. Three card ids stood here,
                    // hard-written, which is the same reward whatever the seed.
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 5);
                    State.RewardGold = 0;
                    State.RewardPotion = 0;
                    State.RelicReward = 0;
                    State.PendingPotionRewards.Clear();
                    var offered = RunRewardGenerator.GenerateEventOfferCards(
                        State,
                        State.RewardCards.Length,
                        GeneratedData.CardPools.Colorless
                    );
                    for (int i = 0; i < State.RewardCards.Length; i++)
                    {
                        State.RewardCards[i] = offered[i];
                        State.RewardUpgraded[i] = false;
                    }

                    State.RewardCardPending = true;
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
                    RunNonCombatEffects.GainMaxHp(State, 7);
                    State.EventId = RunConstants.EventResultPending;
                    return 0;
                }

                // Take the Egg adds the Byrdonis Egg quest card, which had no id until
                // the negative-cost cards were extracted -- so the option was refused.
                if (action == 1)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(
                            RunNonCombatEffects.NamedCard("ByrdonisEgg"),
                            Upgraded: false
                        )
                    );
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
                    if (State.PlayerHp <= 8)
                    {
                        return -1;
                    }

                    State.PlayerHp -= 8;
                    OfferPotionRewards(0);
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDenseVegetation when State.EventPage == 1:
                // The page the Rest answers with: one option, and it is the fight.
                if (action != 0)
                {
                    return -1;
                }

                return StartCombatWithDeck(
                    State.Deck,
                    RunConstants.DenseVegetationEncounterId,
                    State.Relics,
                    State.PlayerHp,
                    State.PlayerMaxHp,
                    State.PotionSlots,
                    State.Gold,
                    Math.Max(0, State.NormalEncountersVisited + State.EliteEncountersVisited - 1)
                );

            case RunConstants.EventDenseVegetation:
                if (action == 0)
                {
                    // The gold is rolled -- NextInt(61, 100) on the event's own stream --
                    // and RunNonCombatEffects already rolled it. The handler was paying a
                    // flat 80, which is inside the range and so looked right in every
                    // capture that never checked the number.
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.DenseVegetationGold(State)
                    );
                }
                else if (action == 1)
                {
                    // Rest mimics a rest site and then wakes something up: the page it
                    // answers with has one option, and that option is the fight.
                    HealPlayer(RestHealAmount());
                    ApplyAfterRestSiteHeal();
                    State.EventPage = 1;
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventLuminousChoir:
                // The player picks the two cards that go; the Spore Mind arrives after
                // they are gone. RemoveLowestPriorityCard was the emulator choosing for
                // them, which is the whole cost of the option.
                if (action == 0)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Remove,
                        0,
                        count: 2,
                        followUpCard: RunNonCombatEffects.NamedCard("SporeMind"),
                        followUpCount: 1
                    )
                        ? 0
                        : -1;
                }

                if (action == 1)
                {
                    // The tribute is the ROLLED price -- 149 minus NextInt(0, 50) -- which
                    // is what the action mask already read. Charging the flat 149 here
                    // meant a run holding, say, 120 gold was offered the tribute and then
                    // refused when it took it: the same mask/step disagreement the Tea
                    // Master had.
                    int tribute = RunNonCombatEffects.LuminousChoirTributeCost(State);
                    if (State.Gold < tribute)
                    {
                        return -1;
                    }

                    State.Gold -= tribute;
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
                // Consume heals and then asks WHICH card to upgrade; Plant enchants with
                // Sown. The emulator healed and upgraded whatever came first, and had no
                // Plant at all, so the game's second option was refused.
                if (action == 0)
                {
                    HealPlayer(9);
                    RunNonCombatEffects.BeginDeckSelection(State, DeckSelection.Upgrade, 0);
                    return 0;
                }

                if (action == 1)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)Enchantment.Sown
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSunkenStatue:
                // GrabSword obtains one named relic -- RelicCmd.Obtain<SwordOfStone> --
                // not a roll from the reward pool.
                if (action == 0)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.SwordOfStoneRelic
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.SunkenStatueGold(State)
                    );
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 7);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTabletOfTruth:
                // The tablet is five secrets, not one, and each costs more than the last:
                // 3, 6, 12, 24, and then everything but a single point of Max HP. The
                // emulator read exactly one of them for a flat 3 and upgraded whichever
                // card came first, so the decision the event is built around -- how far to
                // read before it takes the run -- did not exist.
                if (action == 0)
                {
                    RunNonCombatEffects.Decipher(State);
                    if (RunNonCombatEffects.TabletHasMoreToSay(State))
                    {
                        return 0;
                    }
                }
                else if (action == 1)
                {
                    // Smash heals 20 on the first page; on a later one the second option
                    // is Give Up, which does nothing at all.
                    if (State.EventPage == 0)
                    {
                        HealPlayer(20);
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWellspring:
                if (action == 0)
                {
                    OfferPotionRewards(0);
                    return 0;
                }

                if (action == 1)
                {
                    // Bathe removes one card the PLAYER picks, then adds BatheCurses (1)
                    // Guilty.
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Remove,
                        0,
                        count: 1,
                        followUpCard: RunNonCombatEffects.NamedCard("Guilty"),
                        followUpCount: 1
                    )
                        ? 0
                        : -1;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWhisperingHollow:
                // Exchange Gold SPENDS the rolled amount for two potions on a reward
                // screen -- the emulator was paying it out instead. Hug transforms a card
                // the player picks and only then charges the 9 HP, which is why a capture
                // taken at the selector shows full health.
                if (action == 0)
                {
                    int cost = RunNonCombatEffects.WhisperingHollowGold(State);
                    if (State.Gold < cost)
                    {
                        return -1;
                    }

                    State.Gold -= cost;
                    OfferPotionRewards(0, 0);
                    return 0;
                }

                if (action == 1)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.TransformToRandom,
                        0,
                        count: 1,
                        followUpHpLoss: 9,
                        eventEntry: "WHISPERING_HOLLOW"
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWoodCarvings:
                // Three carvings, three different things: a Basic card becomes a Peck, or
                // is enchanted with Slither, or becomes a Toric Toughness. All three used
                // to transform the first card in the deck into a rolled one.
                if (action is 0 or 2)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.TransformTo,
                        action == 0
                            ? RunNonCombatEffects.NamedCard("Peck")
                            : RunNonCombatEffects.NamedCard("ToricToughness")
                    )
                        ? 0
                        : -1;
                }

                if (action == 1)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)Enchantment.Slither
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventAbyssalBaths:
                // Immersing is not one choice but the first of a series. OnImmerse gains
                // MaxHpVar(2), takes DamageVar and then raises that damage by one, and
                // the page it answers with offers Linger -- the same immersion again, a
                // point dearer every time. The emulator modelled a single dip for a flat
                // 1 damage, which is the NET of the first one rather than the hit, and
                // never offered the page at all: the whole decision the event is about,
                // how deep to go before the escalation catches you, did not exist.
                //
                // EventPage counts immersions, which is what sets the price of the next.
                if (action == 0)
                {
                    RunNonCombatEffects.Immerse(State);
                    return 0;
                }

                // On the second page the options are Linger and Exit; on the first they
                // are Immerse and Abstain, and only Abstain heals.
                if (action == 1)
                {
                    if (State.EventPage > 0)
                    {
                        State.EventId = RunConstants.EventResultPending;
                        return 0;
                    }

                    HealPlayer(10);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDrowningBeacon:
                // Bottle offers the Glowwater Potion on a reward screen
                // (RewardsCmd.OfferCustom), so the player can decline it or make room.
                // Climb is the option that costs Max HP -- the emulator had the cost on
                // Bottle and no Climb at all.
                if (action == 0)
                {
                    State.RewardGold = 0;
                    State.RelicReward = 0;
                    State.RewardPotion = RunNonCombatEffects.GlowwaterPotion;
                    State.Phase = RunPhase.RelicReward;
                    return 0;
                }

                if (action == 1)
                {
                    RunNonCombatEffects.LoseMaxHp(State, 13);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.FresnelLensRelic
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventEndlessConveyor:
                // The belt keeps turning: grabbing pays for whatever dish RollDish
                // landed on and then offers the next one, so the event is a loop the
                // player leaves rather than a single choice.
                if (action == 0)
                {
                    // Grabbing pays GoldVar(40) for whatever the belt is carrying, then
                    // the belt turns. Jelly Liver is the one dish that needs the player:
                    // it transforms a card they pick.
                    // GenerateGrabSomethingOffTheBeltOption locks the grab below the
                    // price, so a run that cannot pay is not offered it and must not be
                    // allowed to take it.
                    if (State.Gold < RunConstants.ConveyorGrabCost)
                    {
                        return -1;
                    }

                    int dish = RunNonCombatEffects.EndlessConveyorDish(State);
                    State.Gold -= RunConstants.ConveyorGrabCost;
                    if (dish == RunNonCombatEffects.DishJellyLiver)
                    {
                        RunNonCombatEffects.RollNextConveyorDish(State);
                        State.EventPage++;
                        if (
                            RunNonCombatEffects.BeginDeckSelection(
                                State,
                                DeckSelection.TransformToRandom,
                                arg: 0,
                                eventEntry: "ENDLESS_CONVEYOR"
                            )
                        )
                        {
                            State.PendingSelectionReturnsToEvent = true;
                            State.Phase = RunPhase.TransformSelect;
                            return 0;
                        }

                        return 0;
                    }

                    RunNonCombatEffects.ApplyEndlessConveyorDish(State, dish);
                    RunNonCombatEffects.RollNextConveyorDish(State);
                    State.EventPage++;
                    return 0;
                }

                if (action == 1)
                {
                    // Only the FIRST page offers Observe the Chef. Once the player has
                    // grabbed, the belt's second option is Leave, which does nothing --
                    // upgrading a card for it paid them to walk away.
                    if (State.EventPage == 0)
                    {
                        RunNonCombatEffects.UpgradeRandomCard(State, "ENDLESS_CONVEYOR");
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventPunchOff:
                // Nab takes an Injury and offers a relic on a reward screen -- it pays no
                // gold and no potion. "I Can Take Them" does not fight: it answers with a
                // second page whose only option does, which is the shape the capture
                // shows -- the run stays on the event and nothing changes.
                if (State.EventPage == 1)
                {
                    if (action != 0)
                    {
                        return -1;
                    }

                    return StartCombatWithDeck(
                        State.Deck,
                        RunConstants.PunchOffEncounterId,
                        State.Relics,
                        State.PlayerHp,
                        State.PlayerMaxHp,
                        State.PotionSlots,
                        State.Gold,
                        Math.Max(
                            0,
                            State.NormalEncountersVisited + State.EliteEncountersVisited - 1
                        )
                    );
                }

                if (action == 1)
                {
                    State.EventPage = 1;
                    return 0;
                }

                if (action == 0)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunNonCombatEffects.NamedCard("Injury"), Upgraded: false)
                    );
                    State.RewardGold = 0;
                    State.RewardPotion = 0;
                    State.PendingPotionRewards.Clear();
                    State.RelicReward = RunRewardGenerator.NextRelic(State);
                    State.Phase = RunPhase.RelicReward;
                    return 0;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSpiralingWhirlpool:
                // Observe enchants; Drink heals a third of Max HP. The emulator had the
                // healing on Observe, at the rest-site amount, and made Drink transform.
                if (action == 0)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)Enchantment.Spiral
                    )
                        ? 0
                        : -1;
                }

                if (action == 1)
                {
                    HealPlayer(RunNonCombatEffects.SpiralingWhirlpoolHeal(State));
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTrashHeap:
                // DiveIn pays HP for a relic; Grab pays nothing for gold and a card.
                // Both prizes come off the event's own fixed tables, not the reward
                // pools -- the emulator had the two options' costs and prizes crossed.
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 8);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.TrashHeapRelic(State)
                    );
                }
                else if (action == 1)
                {
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(State.Relics, 100);
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunNonCombatEffects.TrashHeapCard(State), Upgraded: false)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWaterloggedScriptorium:
                // Bloody Ink is the free option and comes FIRST; the two paid options
                // both enchant with Steady, at 55 gold for one card and 99 for two. The
                // emulator had Bloody Ink second, upgraded instead of enchanting, and
                // turned the 99-gold option into a card reward.
                if (action == 0)
                {
                    RunNonCombatEffects.GainMaxHp(State, 6);
                }
                else if (action is 1 or 2)
                {
                    int cost =
                        action == 1
                            ? RunConstants.ScriptoriumQuillCost
                            : RunConstants.ScriptoriumSpongeCost;
                    if (State.Gold < cost)
                    {
                        return -1;
                    }

                    // The gold is spent before the selector opens, and the game does not
                    // refund it when the player picks nothing -- so a refused selection
                    // must not have taken the gold either.
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.Enchant,
                            (int)Enchantment.Steady,
                            action == 1 ? 1 : 2
                        )
                    )
                    {
                        return -1;
                    }

                    State.Gold -= cost;
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventCrystalSphere:
                // Both options end in the same minigame, differing only in what they cost
                // and how many divinations they buy. The price is not a flat 100: it is
                // rolled in CalculateVars, off the first draw of the event's own stream.
                if (action == 0)
                {
                    int cost = RunNonCombatEffects.CrystalSphereCost(State);
                    if (State.Gold < cost)
                    {
                        return -1;
                    }

                    State.Gold -= cost;
                    RunNonCombatEffects.OpenCrystalSphere(State, 3);
                    return 0;
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunNonCombatEffects.NamedCard("Debt"), Upgraded: false)
                    );
                    RunNonCombatEffects.OpenCrystalSphere(State, 6);
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventDollRoom when State.EventPage > 0:
            {
                // The second page: pick one of the dolls the wait bought.
                var offer = RunNonCombatEffects.DollRoomOffer(State, State.EventPage == 1 ? 2 : 3);
                if ((uint)action >= (uint)offer.Count)
                {
                    return action == RunConstants.EventSkipAction ? 0 : -1;
                }

                RunNonCombatEffects.ApplyRelicPickup(State, offer[action]);
                State.EventId = RunConstants.EventResultPending;
                return 0;
            }

            case RunConstants.EventDollRoom:
                // The dolls are three named relics, rolled from the event's own stream --
                // not from the reward pool. Only "Pick at Random" hands one over here:
                // the two paid options buy a CHOICE of doll on a second page, so they
                // cost the HP and grant nothing yet.
                if (action == 0)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.DollRoomRandomDoll(State)
                    );
                }
                else if (action is 1 or 2)
                {
                    // What the HP buys is the CHOICE, on a second page: five for two
                    // dolls, fifteen for all three. Costing the HP and finishing there
                    // charged the player for a decision they never got to make.
                    State.PlayerHp = Math.Max(0, State.PlayerHp - (action == 1 ? 5 : 15));
                    State.EventPage = action;
                    return 0;
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
                // Both options offer potions on a reward screen -- three named Foul
                // Potions, or one rolled Uncommon. Neither forces anything into the belt.
                if (action == 0)
                {
                    int foul = RunNonCombatEffects.NamedPotion("FoulPotion");
                    OfferPotionRewards(foul, foul, foul);
                    return 0;
                }

                if (action == 1)
                {
                    OfferPotionRewards(
                        RunRewardGenerator.NextUncommonPotion(State, State.PlayerRng.Rewards)
                    );
                    return 0;
                }

                if (action != RunConstants.EventSkipAction)
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
                    // GiveGold is offered unconditionally and simply loses GoldVar(100);
                    // LoseGold floors at zero, so a player with 99 hands over 99 and
                    // still gets the relic. Refusing below 100 made the option look
                    // unaffordable when the game never priced it that way.
                    State.Gold = Math.Max(0, State.Gold - 100);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunRewardGenerator.NextRelic(State)
                    );
                }
                else if (action == 2)
                {
                    // The elder names the relic he wants -- rolled off the event's own
                    // stream from the ones he will take -- and hands back TWO. The
                    // emulator gave up whatever sat in slot 1, which is a positional
                    // guess that would surrender an untradable relic, and returned one.
                    int traded = RunNonCombatEffects.RanwidTradeIndex(State);
                    if (traded < 0)
                    {
                        return -1;
                    }

                    State.Relics.RemoveAt(traded);
                    for (int i = 0; i < 2; i++)
                    {
                        RunNonCombatEffects.ApplyRelicPickup(
                            State,
                            RunRewardGenerator.NextRelic(State)
                        );
                    }
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRelicTrader:
                // The trader offers up to three of the player's OWN tradable relics, in
                // the order it shuffled them -- so the option index reads that list, not
                // state.Relics. The step used to index state.Relics directly (skipping
                // slot 0 to dodge Burning Blood) and to accept every option when nothing
                // was tradable, where the game offers a lone Proceed.
                if (action is >= 0 and <= 2)
                {
                    var stock = RunNonCombatEffects.RelicTraderStock(State);
                    if (stock.Count == 0)
                    {
                        // Nothing to trade: index 0 is the Proceed and the rest are not
                        // options at all.
                        if (action != 0)
                        {
                            return -1;
                        }

                        break;
                    }

                    if (action >= stock.Count)
                    {
                        return -1;
                    }

                    State.Relics.RemoveAt(stock[action]);
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
                    // Gorge rolls eight Commons and opens a grid to keep two. Uniform
                    // odds with the pool filtered to Commons means no rarity roll at all,
                    // so each card is a single draw.
                    State.PendingOfferCards = RunRewardGenerator.GenerateEventOfferCards(
                        State,
                        RunConstants.GorgeCardChoices,
                        RunRewardGenerator.IroncladRewardPool,
                        CardRarity.Common
                    );
                    State.PendingOfferPicks = RunConstants.GorgeCardsKept;
                    State.Phase = RunPhase.TransformSelect;
                    return 0;
                }
                else if (action == 1)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 14);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.NamedRelic("ChosenCheese")
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSlipperyBridge:
                // Overcome loses the ONE card the bridge is holding -- rolled off the
                // event's stream, preferring a non-Basic card -- and Hold On costs
                // 3 + NumberOfHoldOns HP, which is 3 the first time, not a flat 10.
                if (action == 0)
                {
                    int index = RunNonCombatEffects.SlipperyBridgeCardIndex(State);
                    if (index < 0)
                    {
                        return -1;
                    }

                    State.Deck.RemoveAt(index);
                }
                else if (action == 1)
                {
                    // CurrentHpLoss is 3 + NumberOfHoldOns, charged BEFORE the counter
                    // moves -- so 3, then 4, then 5 -- and each hold re-rolls which card
                    // the bridge is threatening. Holding on was a flat 3 that never
                    // escalated and never changed the card, which makes it free to repeat.
                    State.PlayerHp = Math.Max(
                        0,
                        State.PlayerHp - RunNonCombatEffects.SlipperyBridgeHpLoss(State)
                    );
                    State.EventPage++;
                    // GetNewRandomCard() again -- the re-roll is an explicit step now that
                    // the named card is remembered rather than recomputed per read.
                    RunNonCombatEffects.RollSlipperyBridgeCard(State);
                    // The bridge answers with the same two options, so the event stays
                    // open rather than falling through to the end-of-event path.
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventStoneOfAllTime:
                // Lift drinks a potion for 10 Max HP, not 5. Push costs 6 HP, not 10, and
                // enchants an Attack with Vigorous 8 rather than upgrading whatever came
                // first -- and the HP is taken BEFORE the selector opens.
                if (action == 0)
                {
                    int slot = Array.FindIndex(State.PotionSlots, potion => potion != 0);
                    if (slot < 0)
                    {
                        return -1;
                    }

                    State.PotionSlots[slot] = 0;
                    RunNonCombatEffects.GainMaxHp(State, 10);
                }
                else if (action == 1)
                {
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.Enchant,
                            (int)Enchantment.Vigorous
                        )
                    )
                    {
                        return -1;
                    }

                    State.PlayerHp = Math.Max(0, State.PlayerHp - 6);
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventSymbiote:
                // Approach enchants an Attack with Corrupted -- it does not upgrade --
                // and Kill with Fire lets the player CHOOSE what burns.
                if (action == 0)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)Enchantment.Corrupted
                    )
                        ? 0
                        : -1;
                }

                if (action == 1)
                {
                    return RunNonCombatEffects.BeginDeckSelection(
                        State,
                        DeckSelection.TransformToRandom,
                        0,
                        eventEntry: "SYMBIOTE"
                    )
                        ? 0
                        : -1;
                }

                if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventTeaMaster:
                // Three named teas at three prices. Bone Tea is 50, not 150 -- the
                // action mask already read BoneTeaCost while the step charged the
                // Ember price for both, and all three handed over a pool relic.
                if (action == 0)
                {
                    if (State.Gold < RunConstants.BoneTeaCost)
                    {
                        return -1;
                    }

                    State.Gold -= RunConstants.BoneTeaCost;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.NamedRelic("BoneTea")
                    );
                }
                else if (action == 1)
                {
                    if (State.Gold < RunConstants.EmberTeaCost)
                    {
                        return -1;
                    }

                    State.Gold -= RunConstants.EmberTeaCost;
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.NamedRelic("EmberTea")
                    );
                }
                else if (action == 2)
                {
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.NamedRelic("TeaOfDiscourtesy")
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
                        // With nothing to trade the event offers a lone Proceed at index
                        // 0, which is what the mask offers -- so refusing it here left
                        // the two disagreeing about the same action.
                        if (action == 0 && !State.PotionSlots.Any(potion => potion != 0))
                        {
                            break;
                        }

                        return -1;
                    }

                    // Three upgraded cards on a reward screen, all of the rarity the
                    // potion buys and the type the event rolled for it -- not one card
                    // taken off the map-generation stream and pushed into the deck.
                    int traded = State.PotionSlots[slot];
                    State.PotionSlots[slot] = 0;
                    RunRewardGenerator.EnterFutureOfPotionsReward(
                        State,
                        traded,
                        RunNonCombatEffects.EventStream(State, "THE_FUTURE_OF_POTIONS")
                    );
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventThisOrThat:
                // The plain chest costs 6 HP, not 7, and its purse is NextInt(41, 69) off
                // the event's own stream rather than a generic 55 +/- 15 off UpFront.
                if (action == 0)
                {
                    State.PlayerHp = Math.Max(0, State.PlayerHp - 6);
                    State.Gold += Effects.RelicEffects.ModifyGoldGained(
                        State.Relics,
                        RunNonCombatEffects.ThisOrThatGold(State)
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
                        new CardInstance(RunNonCombatEffects.NamedCard("Clumsy"), Upgraded: false)
                    );
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWarHistorianRepy:
                // Unlock the Cage frees Repy for the named History Course; Unlock the
                // Chest opens a reward screen carrying two potions and two relics. The
                // emulator had the potion on the cage option and a pool relic on both.
                // Both doors spend a Lantern Key, and a run that arrived holding two
                // gets to open the other one as well -- ShouldGetSecondReward. The key
                // has a card id now that the negative-cost cards are extracted, so the
                // deck is where that is read from rather than being assumed away.
                if (action == 0)
                {
                    RunNonCombatEffects.SpendLanternKey(State);
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        RunNonCombatEffects.NamedRelic("HistoryCourse")
                    );
                    // A run that arrived with two keys still holds one, and Repy offers
                    // the other door.
                    if (State.EventPage == 0 && RunNonCombatEffects.RepyOwesASecondReward(State))
                    {
                        State.EventPage = 1;
                        return 0;
                    }
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.SpendLanternKey(State);
                    // The chest carries TWO potions and TWO relics; the emulator's
                    // reward screen carries one of each, so this offers half of it.
                    State.RewardGold = 0;
                    State.RewardPotion = RunRewardGenerator.NextPotion(
                        State,
                        State.PlayerRng.Rewards
                    );
                    State.RelicReward = RunRewardGenerator.NextRelic(State);
                    State.Phase = RunPhase.RelicReward;
                    return 0;
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventWelcomeToWongos:
                // Three price tags, not one: 100, 200 and 300, and each option is locked
                // below its OWN price. Charging a flat 100 for all three sold the mystery
                // box at a third of its price, and only the mask knew the real numbers.
                if (action is >= 0 and <= 2)
                {
                    int price = action switch
                    {
                        0 => RunConstants.WongosBargainBinCost,
                        1 => RunConstants.WongosFeaturedItemCost,
                        _ => RunConstants.WongosMysteryBoxCost,
                    };
                    if (State.Gold < price)
                    {
                        return -1;
                    }

                    State.Gold -= price;

                    // Three tiers, three different things -- not one rolled relic three
                    // times. The bin is a shop-legal COMMON, the featured item a shop-legal
                    // RARE named on the option when the event opened, and the mystery box
                    // is Wongo's Mystery Ticket by name.
                    RunNonCombatEffects.ApplyRelicPickup(
                        State,
                        action switch
                        {
                            0 => RunRewardGenerator.NextShopRelicOfRarity(
                                State,
                                RelicRarity.Common
                            ),
                            1 => RunNonCombatEffects.WongosFeaturedItem(State),
                            _ => RunNonCombatEffects.NamedRelic("WongosMysteryTicket"),
                        }
                    );

                    // Buying also earns Wongo points toward a customer badge, which
                    // accumulate in the PROFILE across runs (SaveManager.Progress) rather
                    // than in the run. A run cannot know that total, so the badge is not
                    // modelled -- see the fixed-profile note in HANDOFF.md.
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
                // Two options, not three -- the step used to accept an option
                // the event does not declare, which the action mask correctly
                // never offered. What the two DO is transcribed but unverified:
                // this is an act-2 event with no live capture behind it.
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
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventFieldOfManSizedHoles:
                // Resist is CardSelectCmd.FromDeckForRemoval at CardsVar(2) followed by
                // AddCursesToDeck(Normality) -- TWO cards the player picks, and a curse for
                // them. The emulator removed ONE card of its own choosing and charged
                // nothing for it.
                if (action == 0)
                {
                    // A deck with nothing removable does not REFUSE the option: the game
                    // awaits an empty selection and runs AddCursesToDeck below it either
                    // way, so the curse still lands.
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.Remove,
                            0,
                            count: 2,
                            followUpCard: RunNonCombatEffects.NamedCard("Normality"),
                            followUpCount: 1
                        )
                    )
                    {
                        RunNonCombatEffects.AddCardToDeck(
                            State,
                            new CardInstance(RunNonCombatEffects.NamedCard("Normality"), false)
                        );
                        State.EventId = RunConstants.EventResultPending;
                    }

                    return 0;
                }
                else if (action == 1)
                {
                    // TODO: EnterYourHole is FromDeckForEnchantment with PerfectFit, an
                    // enchantment the emulator does not model yet (it moves its card to the
                    // front of the draw pile on every reshuffle EXCEPT the initial one).
                    // The 12 HP and a relic below are invented and belong to no option of
                    // this event; they are left in place only so the branch answers at all.
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
                // Neither option was this event. LetItIn heals HealVar(25) and adds a
                // Metamorphosis to the deck; Rejection is FromDeckForUpgrade -- the player
                // upgrades a card of their choosing -- and then takes HpLossVar(10)
                // UNBLOCKABLE damage. The emulator removed a card and granted 3 max HP for
                // the first, and transformed-then-upgraded the deck's first card for the
                // second, charging nothing either way.
                if (action == 0)
                {
                    HealPlayer(25);
                    RunNonCombatEffects.AddCardToDeck(
                        State,
                        new CardInstance(RunNonCombatEffects.NamedCard("Metamorphosis"), false)
                    );
                }
                else if (action == 1)
                {
                    // FromDeckForUpgrade on a fully upgraded deck returns nothing and
                    // Rejection carries on to the damage regardless -- so the option is
                    // still takeable, and refusing it made the event's mask offer a move
                    // its own step would not accept.
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.Upgrade,
                            0,
                            followUpHpLoss: 10
                        )
                    )
                    {
                        State.PlayerHp = Math.Max(0, State.PlayerHp - 10);
                        State.EventId = RunConstants.EventResultPending;
                    }

                    return 0;
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
                        new CardInstance(RunNonCombatEffects.NamedCard("LanternKey"), false)
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
                // The weaver sells removals. What was here -- heal 12 / upgrade a card /
                // +5 max HP -- matched none of its three options; the model is
                // BreathingTechniques, EmotionalAwareness and ArachnidAcupuncture.
                if (action == 0)
                {
                    // BreathingTechniquesCost 50, then two Enlightenments into the deck.
                    State.Gold -= RunConstants.ZenWeaverBreathingCost;
                    for (int i = 0; i < 2; i++)
                    {
                        RunNonCombatEffects.AddCardToDeck(
                            State,
                            new CardInstance(RunConstants.CardEnlightenment, Upgraded: false)
                        );
                    }
                }
                else if (action is 1 or 2)
                {
                    // EmotionalAwareness removes one for 125, ArachnidAcupuncture two for
                    // 250, and both are CreateLockedOption when the gold is not there --
                    // a locked option is shown but does nothing, so refuse the action.
                    int cost =
                        action == 1
                            ? RunConstants.ZenWeaverEmotionalCost
                            : RunConstants.ZenWeaverAcupunctureCost;
                    if (State.Gold < cost)
                    {
                        return -1;
                    }

                    // RemoveCardsAndProceed loses the gold AFTER the selector, but nothing
                    // between the two can change it, and charging here keeps the cost from
                    // outliving a selection that could not open.
                    if (
                        !RunNonCombatEffects.BeginDeckSelection(
                            State,
                            DeckSelection.Remove,
                            0,
                            count: action == 1 ? 1 : 2,
                            eventEntry: "ZEN_WEAVER"
                        )
                    )
                    {
                        return -1;
                    }

                    State.Gold -= cost;
                    return 0;
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
                        new CardInstance(RunNonCombatEffects.NamedCard("Decay"), false)
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
                // Neither option asks the player anything -- Touch a Mirror rolls its
                // cards off the event's own Rng and Shatter takes the whole deck. What
                // was here (transform the first card / upgrade the first card) was a
                // placeholder standing in for both.
                if (action == 0)
                {
                    RunNonCombatEffects.ReflectionsTouchAMirror(State);
                }
                else if (action == 1)
                {
                    RunNonCombatEffects.ReflectionsShatter(State);
                }
                else if (action != RunConstants.EventSkipAction)
                {
                    return -1;
                }

                break;
            case RunConstants.EventRoundTeaParty:
                // Two options, not three -- the step used to accept an option
                // the event does not declare, which the action mask correctly
                // never offered. What the two DO is transcribed but unverified:
                // this is an act-2 event with no live capture behind it.
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
            // Tinker Time builds a card over three pages: pick a type, pick a rider, take
            // the Mad Science it made. What was here -- upgrade a card / transform a card
            // / gain a potion -- was three options the event does not have, on a page it
            // does not have (the model declares exactly ONE initial option).
            case RunConstants.EventTinkerTime when State.EventPage == 2:
                // The rider page. Both offered riders are legal; which is which came out
                // of the shuffle, so the action indexes the offer.
                if ((uint)action >= (uint)State.EventRandomOffer.Length)
                {
                    return -1;
                }

                RunNonCombatEffects.AddCardToDeck(
                    State,
                    new CardInstance(
                        RunConstants.CardMadScience,
                        Upgraded: false,
                        TinkerType: State.TinkerCardType,
                        TinkerRider: (TinkerRider)State.EventRandomOffer[action]
                    )
                );
                break;

            case RunConstants.EventTinkerTime when State.EventPage == 1:
                // The card-type page, two of Attack/Skill/Power. Choosing one settles the
                // type and rolls the riders that go with it.
                if ((uint)action >= (uint)State.EventRandomOffer.Length)
                {
                    return -1;
                }

                State.TinkerCardType = (CardType)State.EventRandomOffer[action];
                RunNonCombatEffects.BeginTinkerRiderPage(State);
                return 0;

            case RunConstants.EventTinkerTime:
                // One option, and it only turns the page.
                if (action == 0)
                {
                    RunNonCombatEffects.BeginTinkerCardTypePage(State);
                    return 0;
                }

                if (action != RunConstants.EventSkipAction)
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

    /// <summary>
    /// Take one card off an offered grid. The card joins the deck and leaves the grid;
    /// the screen stays up while more picks are owed and there is still something to
    /// pick, which is where the game's own selector stops too.
    /// </summary>
    private int StepOfferSelect(int action)
    {
        if ((uint)action >= (uint)State.PendingOfferCards.Length)
        {
            return -1;
        }

        RunNonCombatEffects.AddCardToDeck(
            State,
            new CardInstance(State.PendingOfferCards[action], Upgraded: false)
        );
        State.PendingOfferCards = [.. State.PendingOfferCards.Where((_, index) => index != action)];
        State.PendingOfferPicks--;

        if (State.PendingOfferPicks > 0 && State.PendingOfferCards.Length > 0)
        {
            return 0;
        }

        State.PendingOfferCards = [];
        State.PendingOfferPicks = 0;

        // Hefty Tablet's Injury comes WITH the card the grid hands over, not before it:
        // CardPileCmd.Add takes ONE list holding the curse with the chosen card inserted
        // at its front, so a live capture shows both land in the same snapshot.
        if (State.PendingHeftyTabletCurse)
        {
            State.PendingHeftyTabletCurse = false;
            RunNonCombatEffects.AddCardToDeck(
                State,
                new CardInstance(RunNonCombatEffects.NamedCard("Injury"), Upgraded: false)
            );
        }

        // A grid Neow opened returns to Neow, which stays up for one more Proceed. One an
        // EVENT opened returns to the event's finished page.
        if (State.NeowAwaitingProceed)
        {
            State.Phase = RunPhase.Ancient;
            return 0;
        }

        State.EventId = RunConstants.EventResultPending;
        State.Phase = RunPhase.Event;
        return 0;
    }

    /// <summary>
    /// One divination. The action carries both the cell and the tool -- 0..120 is the big
    /// tool, 121..241 the small one -- and the last one spent settles up.
    /// </summary>
    private int StepCrystalSphere(int action, out bool terminal)
    {
        terminal = false;
        var game = State.CrystalSphere;
        if (game is null || game.IsFinished)
        {
            return -1;
        }

        if (action < 0 || action >= 2 * RunConstants.CrystalSphereCells)
        {
            return -1;
        }

        var tool =
            action >= RunConstants.CrystalSphereSmallToolAction
                ? CrystalSphereTool.Small
                : CrystalSphereTool.Big;
        int cell = action % RunConstants.CrystalSphereSmallToolAction;
        int x = cell / RunConstants.CrystalSphereSize;
        int y = cell % RunConstants.CrystalSphereSize;
        if (!game.WouldUncover(x, y, tool))
        {
            return -1;
        }

        game.Tool = tool;
        foreach (int index in game.Click(x, y))
        {
            RunNonCombatEffects.RevealCrystalSphereItem(State, game.Items[index]);
        }

        if (!game.IsFinished)
        {
            return 0;
        }

        return CompleteCrystalSphere(out terminal);
    }

    /// <summary>
    /// Settles the sphere once the divinations run out: every fully-uncovered thing turns
    /// into a reward, and the run goes to the reward screen -- or straight on, if the fog
    /// kept everything.
    ///
    /// The draw order is the game's, and it is two passes rather than one. Every reward is
    /// built first (<c>ToReward</c>, which is where a potion picks itself), then every
    /// reward is populated (<c>RewardsSet.GenerateWithoutOffering</c>, which is where the
    /// relic is pulled and the card offers rolled). Both passes walk the items in the order
    /// they were uncovered.
    /// </summary>
    private int CompleteCrystalSphere(out bool terminal)
    {
        var game = State.CrystalSphere!;
        var rng = State.CrystalSphereRng!;
        var revealed = game
            .Revealed.Select(index => game.Items[index])
            // The curse has no reward: it was taken at the moment it was uncovered.
            .Where(item => item.Kind != CrystalSphereItemKind.Curse)
            .ToList();

        // Pass one, ToReward. Only a potion draws here -- it picks itself as the reward is
        // built, where everything else waits to be populated.
        var potions = new List<int>();
        foreach (var item in revealed)
        {
            if (item.Kind == CrystalSphereItemKind.Potion)
            {
                potions.Add(RunRewardGenerator.NextPotionOfRarity(rng, (PotionRarity)item.Rarity));
            }
        }

        // Pass two, Populate, in the same order. A gold pile draws even though its amount
        // is fixed -- GoldReward.Populate is Rng.NextInt(amount, amount + 1) whether or not
        // there is a range -- and that draw sits between the piles and the card offers.
        var golds = new List<int>();
        int relic = 0;
        var offers = new List<int[]>();
        foreach (var item in revealed)
        {
            switch (item.Kind)
            {
                case CrystalSphereItemKind.Gold:
                    int amount = item.IsBigGold ? 30 : 10;
                    golds.Add(rng.NextInt(amount, amount + 1));
                    break;
                case CrystalSphereItemKind.Relic:
                    relic = RunRewardGenerator.NextRelic(State, rng);
                    break;
                case CrystalSphereItemKind.CardReward:
                    offers.Add(
                        RunRewardGenerator.GenerateFixedRarityCardOffer(
                            State,
                            State.RewardCards.Length,
                            (CardRarity)item.Rarity,
                            rng
                        )
                    );
                    break;
            }
        }

        State.CrystalSphere = null;
        State.CrystalSphereRng = null;
        RunRewardGenerator.ClearRewardScreen(State);
        State.PendingPotionRewards.Clear();

        if (golds.Count == 0 && potions.Count == 0 && relic == 0 && offers.Count == 0)
        {
            return AdvanceAfterNode(out terminal);
        }

        State.PendingGoldRewards.AddRange(golds);
        RunRewardGenerator.OfferNextGold(State);
        State.PendingPotionRewards.AddRange(potions);
        OfferNextRestPotion();
        State.RelicReward = relic;
        if (offers.Count > 0)
        {
            Array.Clear(State.RewardCards);
            Array.Clear(State.RewardUpgraded);
            for (int i = 0; i < State.RewardCards.Length && i < offers[0].Length; i++)
            {
                State.RewardCards[i] = offers[0][i];
            }

            State.RewardCardPending = true;
            State.PendingCardOffers.AddRange(offers.Skip(1));
        }

        State.Phase = RunPhase.RelicReward;
        terminal = false;
        return 0;
    }

    /// <summary>How many bundles Scroll Boxes puts on the screen.</summary>
    private const int BundleCount = 2;

    /// <summary>Cards per bundle: two Commons and an Uncommon.</summary>
    private const int BundleSize = 3;

    /// <summary>
    /// The choose-a-bundle screen, answered the way the game's is: highlight a bundle,
    /// then confirm it. A capture spends one action on each, and re-selecting before
    /// confirming just moves the highlight.
    /// </summary>
    private int StepBundleSelect(int action, out bool terminal)
    {
        terminal = false;
        if (
            action >= 0
            && action < BundleCount
            && State.BundleOffer.Length >= BundleSize * BundleCount
        )
        {
            State.SelectedBundle = action;
            return 0;
        }

        if (action != RunConstants.BundleConfirmAction || State.SelectedBundle < 0)
        {
            return -1;
        }

        for (int i = 0; i < BundleSize; i++)
        {
            RunNonCombatEffects.AddCardToDeck(
                State,
                new CardInstance(State.BundleOffer[State.SelectedBundle * BundleSize + i], false)
            );
        }

        State.BundleOffer = [];
        State.SelectedBundle = -1;

        // A screen Neow opened returns to Neow, which stays up for one more Proceed.
        if (State.NeowAwaitingProceed)
        {
            State.Phase = RunPhase.Ancient;
            return 0;
        }

        return AdvanceAfterNode(out terminal);
    }

    private int StepTransformSelect(int action, out bool terminal)
    {
        terminal = false;
        if (State.PendingOfferCards.Length > 0)
        {
            return StepOfferSelect(action);
        }

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

        if (State.PendingSelectionKind != DeckSelection.None)
        {
            if (!RunNonCombatEffects.ApplyDeckSelection(State, action))
            {
                return -1;
            }

            // A selection for two cards stays on the screen for the second -- unless the
            // deck has run out of cards it would take, which is where the game's own
            // selector stops too.
            if (
                State.PendingSelectionCount > 0
                && Enumerable
                    .Range(0, State.Deck.Count)
                    .Any(i => RunNonCombatEffects.CanSelectCard(State, i))
            )
            {
                return 0;
            }

            RunNonCombatEffects.ResolveDeckSelectionFollowUp(State);
            bool returnsToEvent = State.PendingSelectionReturnsToEvent;
            var returnTo = State.PendingSelectionReturn;
            RunNonCombatEffects.ClearDeckSelection(State);

            // A selection NEOW opened returns to Neow, which stays up for one more
            // Proceed. Leaving the flag set instead is worse than landing on the wrong
            // screen once: it is read again by the next card reward the run claims, which
            // sends a combat's reward screen back to the ancient floors later.
            if (State.NeowAwaitingProceed)
            {
                State.Phase = RunPhase.Ancient;
                return 0;
            }

            // A selection goes back to whatever opened it. Most are events, which is why
            // that is the default — but a shop's removal service and a relic's pickup are
            // not events, and assuming they were is what kept those two choosing a card
            // for the player rather than asking.
            switch (returnTo)
            {
                case SelectionReturn.Shop:
                    State.Phase = RunPhase.Shop;
                    return 0;
                case SelectionReturn.Map:
                    return AdvanceAfterNode(out terminal);
                default:
                    // The belt's Jelly Liver keeps its options up, because the belt turns
                    // and offers the next dish; everything else shows its result page.
                    if (!returnsToEvent && returnTo != SelectionReturn.EventOptions)
                    {
                        State.EventId = RunConstants.EventResultPending;
                    }

                    State.Phase = RunPhase.Event;
                    return 0;
            }
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
                // FromDeckForRemoval, like every other removal: the player picks. The
                // relic is taken outside an event, so the selection goes back to the MAP
                // rather than to an event page that is not there.
                State.TransformSelectedDeckIndex = null;
                terminal = false;
                return RunNonCombatEffects.BeginDeckSelection(
                    State,
                    DeckSelection.Remove,
                    0,
                    count: count,
                    returnTo: SelectionReturn.Map
                )
                    ? 0
                    : AdvanceAfterNode(out terminal);
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
                // Neither option is ever locked. Resting at full health still costs a
                // Poor Sleep, and Kill's LoseMaxHp floors the cap at 1 rather than
                // refusing, so gating these hid two legal moves.
                SetMask(mask, 0);
                SetMask(mask, 1);
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
                // The solo quest is offered at any health; its 18 damage can end the run.
                SetMask(mask, 0);
                SetMask(mask, 1);
                break;
            case RunConstants.EventMorphicGrove:
                // Group is LoseGold(Owner.Gold) -- it takes whatever the run holds, with
                // no minimum -- so the only thing that can stop it is having nothing to
                // transform. The gold >= 100 rule is IsAllowed, which decides whether the
                // EVENT appears, not whether the option is takeable once it has.
                if (
                    RunNonCombatEffects.CanBeginDeckSelection(
                        State,
                        DeckSelection.TransformToRandom,
                        0
                    )
                )
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventTinkerTime:
                // Tinker Time builds its options into a List, so EventOptions.g.cs has no
                // count for it and the fallback offered a flat 0..3 on every page. Page 0
                // declares exactly one option; the two after it show two of three, and
                // which two came out of the shuffle -- so the mask follows the offer.
                if (State.EventPage == 0)
                {
                    SetMask(mask, 0);
                    break;
                }

                for (int i = 0; i < State.EventRandomOffer.Length; i++)
                {
                    SetMask(mask, i);
                }

                break;
            case RunConstants.EventZenWeaver:
                // Breathing Techniques is never locked; the two removals are
                // CreateLockedOption below their price, which shows the row greyed out
                // and does nothing when it is clicked.
                SetMask(mask, 0);
                if (State.Gold >= RunConstants.ZenWeaverEmotionalCost)
                {
                    SetMask(mask, 1);
                }

                if (State.Gold >= RunConstants.ZenWeaverAcupunctureCost)
                {
                    SetMask(mask, 2);
                }

                break;
            case RunConstants.EventSelfHelpBook:
                SetSelfHelpBookMask(mask, 0);
                SetSelfHelpBookMask(mask, 1);
                SetSelfHelpBookMask(mask, 2);
                break;
            case RunConstants.EventBrainLeech:
                // Rip is offered at any health -- the game lets it kill you, and the run
                // ends when it does.
                SetMask(mask, 0);
                SetMask(mask, 1);
                break;
            case RunConstants.EventTheLegendsWereTrue:
                SetMask(mask, 0);
                if (State.PlayerHp > 8 && State.PotionSlots.Any(potion => potion == 0))
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventDoorsOfLightAndDark:
                // Both doors are always open. Light takes up to two upgradable cards and
                // is content with none; Dark opens a removal the player may leave empty.
                SetMask(mask, 0);
                SetMask(mask, 1);
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
                SetTradeMask(
                    mask,
                    State.Relics.Count(relic => RunNonCombatEffects.IsTradableRelic(State, relic))
                );
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
            case RunConstants.EventDollRoom when State.EventPage > 0:
                // The second page offers the dolls the wait bought, and nothing else.
                for (int i = 0; i < (State.EventPage == 1 ? 2 : 3); i++)
                {
                    SetMask(mask, i);
                }

                break;
            case RunConstants.EventWarHistorianRepy when State.EventPage == 1:
                // Repy's second page offers only the door the player has not opened.
                SetMask(mask, 1);
                break;
            case RunConstants.EventAbyssalBaths when State.EventPage > 0:
                // Linger, or climb out.
                SetMask(mask, 0);
                SetMask(mask, 1);
                break;
            case RunConstants.EventEndlessConveyor:
                // Grabbing is locked below its price; the second option is Observe the
                // Chef on the first page and Leave on every later one, and neither costs
                // anything.
                if (State.Gold >= RunConstants.ConveyorGrabCost)
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventTabletOfTruth when State.EventPage > 0:
                // A later page offers Decipher again, or Give Up.
                SetMask(mask, 0);
                SetMask(mask, 1);
                break;
            case RunConstants.EventDenseVegetation when State.EventPage == 1:
                // The Rest's page offers only the fight.
                SetMask(mask, 0);
                break;
            case RunConstants.EventPunchOff when State.EventPage == 1:
                // "I Can Take Them" answers with a page whose only option is the fight.
                SetMask(mask, 0);
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
                if (State.Relics.Any(relic => RunNonCombatEffects.IsTradableRelic(State, relic)))
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
            case RunConstants.EventCrystalSphere:
                // Paying for three divinations needs the rolled price; taking the Debt
                // instead is free.
                if (State.Gold >= RunNonCombatEffects.CrystalSphereCost(State))
                {
                    SetMask(mask, 0);
                }

                SetMask(mask, 1);
                break;
            case RunConstants.EventWhisperingHollow:
                if (State.Gold >= RunNonCombatEffects.WhisperingHollowGold(State))
                {
                    SetMask(mask, 0);
                }

                if (
                    RunNonCombatEffects.CanBeginDeckSelection(
                        State,
                        DeckSelection.TransformToRandom,
                        0
                    )
                )
                {
                    SetMask(mask, 1);
                }

                break;
            case RunConstants.EventWaterloggedScriptorium:
                // Bloody Ink is free; the quill and the sponge are locked below their own
                // prices, and both need a card Steady can take.
                SetMask(mask, 0);
                if (
                    RunNonCombatEffects.CanBeginDeckSelection(
                        State,
                        DeckSelection.Enchant,
                        (int)Enchantment.Steady
                    )
                )
                {
                    if (State.Gold >= RunConstants.ScriptoriumQuillCost)
                    {
                        SetMask(mask, 1);
                    }

                    if (State.Gold >= RunConstants.ScriptoriumSpongeCost)
                    {
                        SetMask(mask, 2);
                    }
                }

                break;
            case RunConstants.EventFakeMerchant:
                // The fake shop is not modelled: the step takes one relic and refuses the
                // rest, so offering three options was offering two the agent cannot use.
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

    /// <summary>
    /// One page of the book is offered when the deck holds a card its enchantment would
    /// take -- Sharp for action 0, Nimble for 1, Swift for 2. The page index IS the
    /// action, so mask and step read the same number.
    /// </summary>
    private void SetSelfHelpBookMask(Span<int> mask, int action)
    {
        var enchantment = RunNonCombatEffects.SelfHelpBookEnchantment(action);
        if (State.Deck.Any(card => Enchantments.CanEnchant(card, enchantment)))
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
        else if (State.PendingCardOffers.Count > 0)
        {
            SetMask(mask, action++);
        }

        for (int i = 0; i < State.PendingOtherCharacterCardRewards; i++)
        {
            SetMask(mask, action++);
        }
    }
}
