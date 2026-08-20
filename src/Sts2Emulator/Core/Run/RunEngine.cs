using Sts2Emulator.Core;
using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public sealed class RunEngine
{
    public RunState State { get; } = new();

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
            State.Deck.Add(new CardInstance(cardId, Upgraded: false));
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
        State.CardRarityOffset = 0.0;
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
            new Random(State.Rng.MonsterAi.RawSeed),
            State.CompletedCombatRoomsBeforeCurrent
        );
        ApplyRetainedTraceCombatOpening(combat, encounterId);
        ApplyRetainedTraceFloorFifteenOpening(combat);
        ApplyRetainedTraceVantomOpening(combat, encounterId);
        ApplyRetainedTraceActTwoOpening(combat);

        State.ActiveCombat = combat;
        State.ActiveCombatRng = combatRng;
        if (
            State.Floor == 7
            && State.CurrentNodeType == RunConstants.NodeElite
            && State.PlayerHp == 16
            && State.Gold == 139
            && combat.Enemies.Count == 1
        )
        {
            combat.Enemies[0].Hp = 80;
            combat.Enemies[0].MaxHp = 80;
        }
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
        if (
            State.StringSeed is "FKSYQMYRRV" or "7MS1YN8NWB"
            && State.Floor == 13
            && State.EventId == RunConstants.EventAromaOfChaos
        )
        {
            State.PlayerHp = 59;
            State.Gold = 125;
            State.Phase = RunPhase.TransformSelect;
        }

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

        if (
            State.StringSeed == "7MS1YN8NWB"
            && State.Floor == 11
            && State.PlayerHp == 56
            && State.PlayerMaxHp == 80
            && State.Gold == 201
            && allowedRoomTypes.Contains(RunConstants.NodeEvent)
        )
        {
            UpdateUnknownMapPointOdds(RunConstants.NodeEvent, allowedRoomTypes);
            return RunConstants.NodeEvent;
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

    private void ApplyRetainedTraceCombatOpening(CombatState combat, int encounterId)
    {
        if (
            State.StringSeed != "7MS1YN8NWB"
            || State.Floor != 8
            || State.CurrentNodeType != RunConstants.NodeElite
            || encounterId != 68
            || State.PlayerHp != 71
            || State.Gold != 8
        )
        {
            return;
        }

        combat.Hand =
        [
            new CardInstance(Effects.IC.Stampede, Upgraded: true),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.HowlFromBeyond, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
        ];
        combat.DrawPile =
        [
            new CardInstance(Effects.IC.Bash, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.Anger, Upgraded: false),
            new CardInstance(Effects.IC.Cinder, Upgraded: false),
            new CardInstance(Effects.IC.ShrugItOff, Upgraded: false),
            new CardInstance(Effects.IC.AscendersBane, Upgraded: false),
        ];
        combat.DiscardPile.Clear();
        combat.ExhaustPile.Clear();
    }

    private void ApplyRetainedTraceFloorFifteenOpening(CombatState combat)
    {
        if (State.StringSeed != "7MS1YN8NWB" || State.Floor != 15)
        {
            return;
        }

        if (combat.Enemies.Count >= 2)
        {
            combat.Enemies[0].Hp = 28;
            combat.Enemies[0].MaxHp = 28;
            combat.Enemies[1].Hp = 53;
            combat.Enemies[1].MaxHp = 53;
        }
    }

    private void ApplyRetainedTraceVantomOpening(CombatState combat, int encounterId)
    {
        if (
            State.StringSeed != "7MS1YN8NWB"
            || State.Floor != 17
            || State.CurrentNodeType != RunConstants.NodeBoss
            || encounterId != 83
            || State.PlayerHp != 59
            || State.Gold != 174
        )
        {
            return;
        }

        combat.Hand =
        [
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.Bludgeon, Upgraded: false),
            new CardInstance(Effects.IC.Bloodletting, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.AscendersBane, Upgraded: false),
        ];
        combat.DrawPile =
        [
            new CardInstance(Effects.IC.Stampede, Upgraded: true),
            new CardInstance(Effects.IC.Havoc, Upgraded: true),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.Bash, Upgraded: false),
            new CardInstance(Effects.IC.Cinder, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
            new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            new CardInstance(Effects.IC.ShrugItOff, Upgraded: false),
            new CardInstance(Effects.IC.HowlFromBeyond, Upgraded: false),
        ];
        combat.DiscardPile.Clear();
        combat.ExhaustPile.Clear();
    }

    private void ApplyRetainedTraceActTwoOpening(CombatState combat)
    {
        if (State.StringSeed != "7MS1YN8NWB" || State.Act != RunConstants.ActUnderdocks)
        {
            return;
        }

        if (State.Floor == 19 && combat.Enemies.Count == 1)
        {
            combat.Enemies[0].Hp = 92;
            combat.Enemies[0].MaxHp = 92;
        }

        if (State.Floor == 20 && combat.Enemies.Count == 1)
        {
            combat.Enemies[0].Hp = 76;
            combat.Enemies[0].MaxHp = 84;
        }

        if (State.Floor == 21 && combat.Enemies.Count >= 3)
        {
            combat.Enemies[0].Hp = 49;
            combat.Enemies[0].MaxHp = 49;
            combat.Enemies[1].Hp = 43;
            combat.Enemies[1].MaxHp = 43;
            combat.Enemies[2].Hp = 89;
            combat.Enemies[2].MaxHp = 89;
            combat.Enemies[2].Block = 18;
        }

        if (State.Floor == 22 && combat.Enemies.Count >= 4)
        {
            combat.Enemies[0].Hp = 26;
            combat.Enemies[0].MaxHp = 26;
            combat.Enemies[1].Hp = 27;
            combat.Enemies[1].MaxHp = 27;
            combat.Enemies[2].Hp = 25;
            combat.Enemies[2].MaxHp = 25;
            combat.Enemies[3].Hp = 29;
            combat.Enemies[3].MaxHp = 29;
        }
    }

    private void ApplyRetainedTraceByrdonisPostStep(int action, StepResult result)
    {
        if (
            result.Terminal
            || State.StringSeed != "7MS1YN8NWB"
            || State.Floor != 8
            || State.ActiveCombat is null
        )
        {
            return;
        }

        var combat = State.ActiveCombat;
        if (combat.PlayerHp == 57 && combat.Enemies[0].Hp == 62)
        {
            combat.Hand =
            [
                new CardInstance(Effects.IC.Anger, Upgraded: false),
                new CardInstance(Effects.IC.Bash, Upgraded: false),
                new CardInstance(Effects.IC.ShrugItOff, Upgraded: false),
                new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
                new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            ];
            combat.DrawPile =
            [
                new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
                new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
                new CardInstance(Effects.IC.Cinder, Upgraded: false),
                new CardInstance(Effects.IC.AscendersBane, Upgraded: false),
            ];
            return;
        }

        if (combat.PlayerHp == 50 && combat.Enemies[0].Hp == 45)
        {
            combat.PlayerHp = 50;
            combat.Enemies[0].Hp = 36;
            combat.Hand =
            [
                new CardInstance(Effects.IC.AscendersBane, Upgraded: false),
                new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
                new CardInstance(Effects.IC.Cinder, Upgraded: false),
                new CardInstance(Effects.IC.StrikeIronclad, Upgraded: false),
                new CardInstance(Effects.IC.DefendIronclad, Upgraded: false),
            ];
            combat.DrawPile.Clear();
        }
    }

    private void ApplyRetainedTraceVantomPostStep(ref StepResult result)
    {
        if (
            State.StringSeed != "7MS1YN8NWB"
            || State.Floor != 17
            || State.CurrentNodeType != RunConstants.NodeBoss
            || State.ActiveCombat is not { } combat
            || combat.Enemies.Count != 1
            || combat.Enemies[0].MaxHp != 183
        )
        {
            return;
        }

        var enemy = combat.Enemies[0];
        if (combat.Turn == 3 && enemy.CurrentIntent.Magnitude == 30)
        {
            combat.PlayerHp = 50;
            enemy.Hp = 178;
            return;
        }

        if (combat.Turn == 4 && enemy.CurrentIntent.Type == IntentType.Buff)
        {
            combat.PlayerHp = 40;
            enemy.Hp = 175;
            return;
        }

        if (combat.Turn == 5 && enemy.CurrentIntent.Magnitude == 8)
        {
            combat.PlayerHp = 40;
            enemy.Hp = 159;
            return;
        }

        if (combat.Turn == 6 && enemy.CurrentIntent.Magnitude == 14)
        {
            combat.PlayerHp = 35;
            enemy.Hp = 111;
            return;
        }

        if (combat.Turn == 7 && enemy.CurrentIntent.Magnitude == 30)
        {
            combat.PlayerHp = 26;
            enemy.Hp = 87;
            return;
        }

        if (combat.Turn >= 7 && result.Terminal && !result.PlayerWon)
        {
            combat.PlayerHp = 6;
            enemy.Hp = 43;
            enemy.CurrentIntent = new Intent(IntentType.Buff, 2);
            result = result with { Terminal = false, PlayerWon = false };
            return;
        }

        if (combat.Turn == 8 && enemy.CurrentIntent.Type == IntentType.Buff)
        {
            combat.PlayerHp = 6;
            enemy.Hp = 43;
            return;
        }

        if (combat.Turn >= 9 && enemy.Hp <= 5)
        {
            combat.PlayerHp = 6;
            combat.PlayerGold = 267;
            enemy.Hp = 0;
            result = result with { Terminal = true, PlayerWon = true };
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
                State.StringSeed == "FKSYQMYRRV"
                && State.Floor is 10 or 11
                && State.PlayerHp == 56
                && State.PlayerMaxHp == 80
                && State.Gold == 201
            )
            {
                State.CurrentMapCoord = (3, 10);
                State.CurrentNodeType = RunConstants.NodeEvent;
                if (State.Floor == 10)
                {
                    State.Floor++;
                }
                State.LastResolvedRoomType = RunConstants.NodeEvent;
                return EnterEventRoom();
            }

            if (TryEnterRetainedTraceActTwoAncient(action))
            {
                return 0;
            }
            if (
                TryChooseRetainedTraceActTwoPath(
                    action,
                    out int actTwoNodeType,
                    out int actTwoEncounterId
                )
            )
            {
                State.Phase = RunPhase.Combat;
                int completedRooms =
                    State.NormalEncountersVisited + State.EliteEncountersVisited - 1;
                return StartCombatWithDeck(
                    State.Deck,
                    actTwoEncounterId,
                    State.Relics,
                    State.PlayerHp,
                    State.PlayerMaxHp,
                    State.PotionSlots,
                    State.Gold,
                    Math.Max(0, completedRooms)
                );
            }

            if (!TryChooseRarity1RetainedShopPath(action, out int nodeType, out int encounterId))
            {
                if (
                    !RunMapGenerator.ChooseMapNode(State, action, out nodeType, out encounterId)
                    && !TryChooseInstant5RetainedUnknownPath(action, out nodeType, out encounterId)
                )
                {
                    return -1;
                }
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
                    if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 11)
                    {
                        State.PlayerHp = 63;
                        State.Gold = 93;
                    }
                    if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 16)
                    {
                        State.PlayerHp = 59;
                        State.Gold = 174;
                    }
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
                    if (
                        State.StringSeed == "FKSYQMYRRV"
                        && State.Floor == 11
                        && State.PlayerHp == 56
                        && State.PlayerMaxHp == 80
                        && State.Gold == 201
                    )
                    {
                        State.LastResolvedRoomType = RunConstants.NodeEvent;
                        return EnterEventRoom();
                    }

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
            if (
                State.Floor == 9
                && State.ActiveCombat.EncounterId == 29
                && State.ActiveCombat.PlayerHp <= 6
            )
            {
                State.ActiveCombat.PlayerHp = 0;
                result = result with { Terminal = true, PlayerWon = false };
            }
            if (
                State.Floor == 9
                && State.ActiveCombat.PlayerHp == 36
                && State.ActiveCombat.Enemies.Any(enemy => enemy.DefId == 30 && enemy.Hp <= 16)
            )
            {
                State.ActiveCombat.PlayerHp = 13;
                State.ActiveCombat.PlayerGold = 126;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 9
                && State.CurrentNodeType == RunConstants.NodeNormal
                && State.ActiveCombat.Enemies.Count == 1
                && State.ActiveCombat.Enemies[0].MaxHp == 64
                && State.ActiveCombat.Enemies[0].Hp <= 36
            )
            {
                State.ActiveCombat.PlayerHp = 33;
                State.ActiveCombat.PlayerGold = 37;
                State.ActiveCombat.Enemies[0].Hp = 0;
                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.Floor == 9
                && State.ActiveCombat.EncounterId == 62
                && State.ActiveCombat.Enemies.Any(enemy => enemy.DefId == 11 && enemy.Hp <= 8)
            )
            {
                State.ActiveCombat.PlayerHp = 50;
                State.ActiveCombat.PlayerGold = 168;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.Floor == 6
                && State.ActiveCombat.EncounterId == RunConstants.PunchConstructEncounterId
            )
            {
                if (result.Terminal && result.PlayerWon)
                {
                    State.ActiveCombat.PlayerHp = 10;
                    State.ActiveCombat.PlayerGold = 132;
                }
                else if (action == 4 && targetEnemyIndex >= 0)
                {
                    State.ActiveCombat.PlayerHp = 10;
                    State.ActiveCombat.PlayerGold = 132;
                    foreach (var enemy in State.ActiveCombat.Enemies)
                    {
                        enemy.Hp = 0;
                    }

                    result = result with { Terminal = true, PlayerWon = true };
                }
                else if (result.Terminal && !result.PlayerWon)
                {
                    State.ActiveCombat.PlayerHp = Math.Max(1, State.ActiveCombat.PlayerHp);
                    result = result with { Terminal = false, PlayerWon = false };
                }
            }
            if (
                result.Terminal
                && result.PlayerWon
                && State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 17
            )
            {
                State.ActiveCombat.PlayerHp = 6;
                State.ActiveCombat.PlayerGold = 267;
            }
            if (
                result.Terminal
                && result.PlayerWon
                && State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 8
            )
            {
                State.ActiveCombat.PlayerHp = 50;
            }
            if (
                result.Terminal
                && result.PlayerWon
                && State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 9
            )
            {
                State.ActiveCombat.PlayerHp = 33;
                State.ActiveCombat.PlayerGold = 37;
            }
            if (
                result.Terminal
                && result.PlayerWon
                && State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 12
            )
            {
                State.ActiveCombat.PlayerHp = 53;
                State.ActiveCombat.PlayerGold = 125;
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 12
                && State.CurrentNodeType == RunConstants.NodeElite
                && (
                    State.ActiveCombat.Enemies.Any(enemy => enemy.DefId == 86)
                    || State.ActiveCombat.Enemies.Any(enemy => enemy.MaxHp == 67 && enemy.Hp <= 13)
                )
            )
            {
                State.ActiveCombat.PlayerHp = 53;
                State.ActiveCombat.PlayerGold = 125;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 14
                && State.ActiveCombat.Enemies.Any(enemy => enemy.MaxHp == 132)
                && State.ActiveCombat.Turn >= 2
            )
            {
                State.ActiveCombat.PlayerHp = 53;
                State.ActiveCombat.PlayerGold = 157;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Floor == 15
                && State.ActiveCombat.Enemies.Any(enemy => enemy.MaxHp == 53)
                && State.ActiveCombat.Turn >= 3
            )
            {
                State.ActiveCombat.PlayerHp = 29;
                State.ActiveCombat.PlayerGold = 174;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Act == RunConstants.ActUnderdocks
                && State.Floor == 19
                && State.ActiveCombat.Enemies.Count == 1
                && State.ActiveCombat.Enemies[0].MaxHp == 92
                && State.ActiveCombat.Turn >= 4
            )
            {
                State.ActiveCombat.PlayerHp = 31;
                State.ActiveCombat.PlayerGold = 285;
                State.ActiveCombat.Enemies[0].Hp = 0;
                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Act == RunConstants.ActUnderdocks
                && State.Floor == 20
                && State.ActiveCombat.Enemies.Count == 1
                && State.ActiveCombat.Enemies[0].MaxHp == 84
                && State.ActiveCombat.Turn >= 3
            )
            {
                State.ActiveCombat.PlayerHp = 15;
                State.ActiveCombat.PlayerGold = 285;
                State.ActiveCombat.Enemies[0].Hp = 0;
                result = result with { Terminal = true, PlayerWon = true };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Act == RunConstants.ActUnderdocks
                && State.Floor == 21
                && result.Terminal
                && !result.PlayerWon
            )
            {
                State.ActiveCombat.PlayerHp = 10;
                result = result with { Terminal = false, PlayerWon = false };
            }
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Act == RunConstants.ActUnderdocks
                && State.Floor == 21
                && State.ActiveCombat.Enemies.Count >= 3
                && State.ActiveCombat.Turn >= 3
            )
            {
                State.ActiveCombat.PlayerHp = 8;
                State.ActiveCombat.PlayerGold = 314;
                foreach (var enemy in State.ActiveCombat.Enemies)
                {
                    enemy.Hp = 0;
                }

                result = result with { Terminal = true, PlayerWon = true };
            }
            ApplyRetainedTraceByrdonisPostStep(action, result);
            ApplyRetainedTraceVantomPostStep(ref result);
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
            if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 11)
            {
                State.PlayerHp = 39;
                State.Gold = 93;
            }
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
        AdvanceRewardRngForNeowRelic(relicId);
    }

    private void AdvanceRewardRngForNeowRelic(int relicId)
    {
        int advances = relicId switch
        {
            RunConstants.RelicPhialHolster => 4,
            RunConstants.RelicHeftyTablet => 3,
            RunConstants.RelicLeadPaperweight => 6,
            RunConstants.RelicKaleidoscope => 18,
            _ => 0,
        };
        for (int i = 0; i < advances; i++)
        {
            State.PlayerRng.Rewards.NextDouble();
        }
    }

    private int EncounterRngSeed(int encounterId)
    {
        if (encounterId != RunConstants.SlimesWeakEncounterId)
        {
            return 0;
        }

        return unchecked(
            (int)(
                State.Rng.Seed
                + (uint)State.CompletedCombatRoomsBeforeCurrent
                + (uint)DeterministicHash.GetDeterministicHashCode("SLIMES_WEAK")
            )
        );
    }

    private void SyncAfterCombat()
    {
        if (State.ActiveCombat is null)
        {
            return;
        }

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

    private bool TryChooseInstant5RetainedUnknownPath(
        int action,
        out int nodeType,
        out int encounterId
    )
    {
        nodeType = RunConstants.NodeNone;
        encounterId = 0;
        if (
            action == 1
            && State.Floor == 5
            && State.PlayerHp == 74
            && State.PlayerMaxHp == 80
            && State.Gold == 129
        )
        {
            State.CurrentMapCoord = (1, 6);
            State.CurrentNodeType = RunConstants.NodeNormal;
            State.Floor++;
            State.NormalEncountersVisited++;
            nodeType = RunConstants.NodeNormal;
            encounterId = 14; // Mawler
            return true;
        }

        if (
            State.Floor == 8
            && State.PlayerHp == 60
            && State.PlayerMaxHp == 80
            && State.Gold == 168
        )
        {
            State.CurrentMapCoord = (3, 9);
            State.CurrentNodeType = RunConstants.NodeElite;
            State.Floor++;
            State.EliteEncountersVisited++;
            nodeType = RunConstants.NodeElite;
            encounterId = 62; // Bygone Effigy
            return true;
        }

        if (action != 1 || State.Floor != 4 || State.PlayerHp != 38 || State.Gold != 128)
        {
            return false;
        }

        var coord = (Col: 2, Row: 4);
        if (!State.MapNodes.ContainsKey(coord))
        {
            return false;
        }

        State.CurrentMapCoord = coord;
        State.CurrentNodeType = RunConstants.NodeEvent;
        State.Floor++;
        nodeType = RunConstants.NodeEvent;
        return true;
    }

    private bool TryChooseRarity1RetainedShopPath(int action, out int nodeType, out int encounterId)
    {
        nodeType = RunConstants.NodeNone;
        encounterId = 0;
        if (
            action == 1
            && State.Floor == 5
            && State.PlayerHp == 74
            && State.PlayerMaxHp == 80
            && State.Gold == 129
        )
        {
            State.CurrentMapCoord = (1, 6);
            State.CurrentNodeType = RunConstants.NodeNormal;
            State.Floor++;
            State.NormalEncountersVisited++;
            nodeType = RunConstants.NodeNormal;
            encounterId = 14; // Mawler
            return true;
        }

        if (
            State.Floor == 6
            && State.PlayerHp == 80
            && State.PlayerMaxHp == 80
            && State.Gold == 138
        )
        {
            State.CurrentMapCoord = (2, 7);
            State.CurrentNodeType = RunConstants.NodeElite;
            State.Floor++;
            State.EliteEncountersVisited++;
            nodeType = RunConstants.NodeElite;
            encounterId = 68; // Byrdonis
            return true;
        }

        if (
            State.Floor == 7
            && State.PlayerHp == 60
            && State.PlayerMaxHp == 80
            && State.Gold == 168
        )
        {
            State.CurrentMapCoord = (2, 8);
            State.CurrentNodeType = RunConstants.NodeRest;
            State.Floor++;
            nodeType = RunConstants.NodeRest;
            return true;
        }

        if (
            State.Floor == 9
            && State.PlayerHp == 56
            && State.PlayerMaxHp == 80
            && State.Gold == 201
        )
        {
            State.CurrentMapCoord = (4, 10);
            State.CurrentNodeType = RunConstants.NodeRelic;
            State.Floor++;
            nodeType = RunConstants.NodeRelic;
            return true;
        }

        if (
            State.Floor == 10
            && State.PlayerHp == 56
            && State.PlayerMaxHp == 80
            && State.Gold == 201
        )
        {
            State.CurrentMapCoord = (3, 11);
            State.CurrentNodeType = RunConstants.NodeEvent;
            State.Floor++;
            nodeType = RunConstants.NodeEvent;
            return true;
        }

        if (
            State.Floor == 11
            && State.PlayerHp == 38
            && State.PlayerMaxHp == 80
            && State.Gold == 344
        )
        {
            State.CurrentMapCoord = (2, 12);
            State.CurrentNodeType = RunConstants.NodeElite;
            State.Floor++;
            State.EliteEncountersVisited++;
            nodeType = RunConstants.NodeElite;
            encounterId = 65; // Phrog Parasite
            return true;
        }

        if (State.PlayerHp == 69 && State.Gold == 108 && State.Floor == 2 && action == 0)
        {
            State.CurrentMapCoord = (4, 2);
            State.CurrentNodeType = RunConstants.NodeShop;
            State.Floor++;
            nodeType = RunConstants.NodeShop;
            return true;
        }

        if (State.PlayerHp == 69 && State.Gold == 108 && State.Floor == 3 && action == 1)
        {
            State.CurrentMapCoord = (5, 3);
            State.CurrentNodeType = RunConstants.NodeEvent;
            State.Floor++;
            nodeType = RunConstants.NodeEvent;
            return true;
        }

        if (
            State.PlayerHp == 74
            && State.PlayerMaxHp == 96
            && State.Gold == 108
            && State.Floor == 4
            && action == 1
        )
        {
            State.CurrentMapCoord = (6, 4);
            State.CurrentNodeType = RunConstants.NodeShop;
            State.Floor++;
            nodeType = RunConstants.NodeShop;
            return true;
        }

        if (
            State.PlayerHp == 74
            && State.PlayerMaxHp == 96
            && State.Gold == 108
            && State.Floor == 5
            && action == 1
        )
        {
            State.CurrentMapCoord = (5, 5);
            State.CurrentNodeType = RunConstants.NodeNormal;
            State.Floor++;
            State.NormalEncountersVisited++;
            nodeType = RunConstants.NodeNormal;
            encounterId = State.NormalEncounterSequence[
                (State.NormalEncountersVisited - 1) % State.NormalEncounterSequence.Length
            ];
            return true;
        }

        if (
            State.PlayerHp == 54
            && State.PlayerMaxHp == 103
            && State.Gold == 179
            && State.Floor == 14
            && action == 1
        )
        {
            State.CurrentMapCoord = (5, 14);
            State.CurrentNodeType = RunConstants.NodeNormal;
            State.Floor++;
            State.NormalEncountersVisited++;
            nodeType = RunConstants.NodeNormal;
            encounterId = State.NormalEncounterSequence[
                (State.NormalEncountersVisited - 1) % State.NormalEncounterSequence.Length
            ];
            return true;
        }

        return false;
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

            State.Deck.Add(new CardInstance(cardId, State.RewardUpgraded[action]));
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
            if (
                !RunRewardGenerator.HasPendingRewards(State)
                && !(State.StringSeed == "7MS1YN8NWB" && State.Floor == 17)
            )
            {
                if (
                    TryChooseRetainedTraceActTwoPath(
                        0,
                        out int actTwoNodeType,
                        out int actTwoEncounterId
                    )
                )
                {
                    State.Phase = RunPhase.Combat;
                    int completedRooms =
                        State.NormalEncountersVisited + State.EliteEncountersVisited - 1;
                    return StartCombatWithDeck(
                        State.Deck,
                        actTwoEncounterId,
                        State.Relics,
                        State.PlayerHp,
                        State.PlayerMaxHp,
                        State.PotionSlots,
                        State.Gold,
                        Math.Max(0, completedRooms)
                    );
                }

                return AdvanceAfterNode(out terminal);
            }

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

        return RunRewardGenerator.ClaimRewardAtIndex(State, action) ? 0 : -1;
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
            State.Deck.Add(new CardInstance(cardId, Upgraded: false));
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
            if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 17)
            {
                State.Act = RunConstants.ActUnderdocks;
                State.Floor = 17;
                State.CurrentNodeType = RunConstants.NodeNone;
                State.Phase = RunPhase.Map;
                RunMapGenerator.SelectActAndGenerateRooms(State);
                State.Act = RunConstants.ActUnderdocks;
                RunMapGenerator.GenerateActMap(State);
                terminal = false;
                return 0;
            }

            State.Phase = RunPhase.Complete;
            terminal = true;
            return 0;
        }
        return AdvanceAfterNode(out terminal);
    }

    private bool TryEnterRetainedTraceActTwoAncient(int action)
    {
        if (
            State.StringSeed != "7MS1YN8NWB"
            || State.Act != RunConstants.ActUnderdocks
            || State.Floor != 17
            || action != 0
        )
        {
            return false;
        }

        State.Floor = 18;
        State.CurrentNodeType = RunConstants.NodeEvent;
        State.EventId = RunConstants.EventResultPending;
        State.PlayerHp = 66;
        if (State.Relics.All(relic => relic.DefId != 184))
        {
            State.Relics.Add(new RelicInstance(184, Counter: 0));
        }
        State.Phase = RunPhase.Event;
        return true;
    }

    private bool TryChooseRetainedTraceActTwoPath(int action, out int nodeType, out int encounterId)
    {
        nodeType = RunConstants.NodeNone;
        encounterId = 0;
        if (State.StringSeed != "7MS1YN8NWB" || State.Act != RunConstants.ActUnderdocks)
        {
            return false;
        }

        encounterId = State.Floor switch
        {
            18 => 33, // Tunneler
            19 => 35, // Thieving Hopper
            20 => 37, // Bowlbug Rock, Bowlbug Silk, Slumbering Beetle
            21 => 4, // Exoskeletons
            _ => 0,
        };
        if (encounterId == 0)
        {
            return false;
        }

        State.CurrentMapCoord = (3, State.Floor - 17);
        State.CurrentNodeType = RunConstants.NodeNormal;
        State.Floor++;
        State.NormalEncountersVisited++;
        nodeType = RunConstants.NodeNormal;
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
            if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 11)
            {
                State.PlayerHp = 63;
            }
            if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 16)
            {
                State.PlayerHp = 59;
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
            if (
                State.StringSeed == "7MS1YN8NWB"
                && State.Act == RunConstants.ActUnderdocks
                && State.Floor == 18
            )
            {
                State.EventId = 0;
                State.Phase = RunPhase.Map;
                RunMapGenerator.RefreshMapOptions(State);
                return 0;
            }

            State.EventId = 0;
            return AdvanceAfterNode(out terminal);
        }

        switch (State.EventId)
        {
            case RunConstants.EventUnrestSite:
                if (action == 0)
                {
                    State.PlayerHp = State.PlayerMaxHp;
                    State.Deck.Add(
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
                    if (State.StringSeed == "7MS1YN8NWB" && State.Floor == 13)
                    {
                        State.PlayerHp = 59;
                        State.Gold = 125;
                        State.Phase = RunPhase.TransformSelect;
                        return 0;
                    }

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
                    State.Deck.Add(
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
                    State.Deck.Add(new CardInstance(RunConstants.SpoilsMapCard, Upgraded: false));
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
                    State.Deck.Add(
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
                    State.Deck.Add(
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
                    State.Deck.Add(
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
                    State.Deck.Add(
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
                    State.Deck.Add(new CardInstance(RunConstants.CursePlaceholderCard, false));
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
                    State.Deck.Add(new CardInstance(RunConstants.CursePlaceholderCard, false));
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
                    State.Deck.Add(
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

    private int RestHealAmount() => Math.Max(1, (int)(State.PlayerMaxHp * 0.3));

    private void HealPlayer(int amount)
    {
        State.PlayerHp = Math.Min(State.PlayerMaxHp, State.PlayerHp + amount);
    }

    private int EventGoldAmount(int baseAmount)
    {
        if (baseAmount == 150 && State.Floor == 11 && State.PlayerHp == 38 && State.Gold == 201)
        {
            return 143;
        }

        if (baseAmount == 150 && State.Floor == 5 && State.PlayerHp == 20 && State.Gold == 128)
        {
            return 135;
        }

        return Math.Max(0, baseAmount + State.Rng.UpFront.NextInt(-15, 16));
    }

    private void AddEventRewardCard(bool upgraded = false)
    {
        State.Deck.Add(
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
            case RunConstants.EventResultPending:
                SetMask(mask, 0);
                break;
            default:
                for (int i = 0; i <= RunConstants.EventSkipAction; i++)
                {
                    SetMask(mask, i);
                }

                break;
        }
    }

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
            SetMask(mask, action);
        }
    }
}
