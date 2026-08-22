namespace Sts2Emulator.Core;

using Sts2Emulator.Core.Rng;
using Sts2Emulator.Core.Run;

/// <summary>
/// Deep copies of the mutable simulation state, so a search can fork a position
/// instead of replaying the run to reach it.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is hand-written rather than reflected, because the core is
/// NativeAOT. That makes it drift-prone -- a field added to a state class and not
/// added here is a clone that silently shares it -- so <c>StateCloningTests</c>
/// walks every field by reflection and fails when one is missed.
/// </para>
/// <para>
/// A plain clone is a faithful copy, which for a tree search is an oracle: every
/// run-level stream derives from the run seed, so rolling forward reads the exact
/// rewards, shop stock and encounter compositions still to come. Resampling is what
/// makes a clone safe to search with -- see <c>RunEngine.ResampleHiddenState</c> and
/// docs/agent-interface.md.
/// </para>
/// </remarks>
public static class StateCloning
{
    public static GameRng Clone(this GameRng rng)
    {
        var copy = new GameRng(rng.RawSeed);
        copy.AdvanceToCallCount(rng.CallCount);
        return copy;
    }

    public static CountingRandom Clone(this CountingRandom rng)
    {
        var copy = new CountingRandom(rng.Seed);
        for (int i = 0; i < rng.CallCount; i++)
        {
            copy.Next();
        }

        return copy;
    }

    public static EnemyState Clone(this EnemyState enemy) =>
        new()
        {
            DefId = enemy.DefId,
            Hp = enemy.Hp,
            MaxHp = enemy.MaxHp,
            Block = enemy.Block,
            CurrentIntent = enemy.CurrentIntent,
            SecondaryIntent = enemy.SecondaryIntent,
            Buffs = [.. enemy.Buffs],
            MoveIndex = enemy.MoveIndex,
            LastMove = enemy.LastMove,
            LastMoveRepeats = enemy.LastMoveRepeats,
            OnceOnlyMoveUsed = enemy.OnceOnlyMoveUsed,
            MoveHistory = [.. enemy.MoveHistory],
            StartsOnBranch = enemy.StartsOnBranch,
            StolenGold = enemy.StolenGold,
            HeistGold = enemy.HeistGold,
        };

    public static PendingCardSelection Clone(this PendingCardSelection selection) =>
        new()
        {
            Kind = selection.Kind,
            Candidates = [.. selection.Candidates],
            SourceCardDefId = selection.SourceCardDefId,
            Amount = selection.Amount,
            GeneratedCandidates = [.. selection.GeneratedCandidates],
        };

    public static CombatState Clone(this CombatState combat) =>
        new()
        {
            AscensionLevel = combat.AscensionLevel,
            PlayerHp = combat.PlayerHp,
            PlayerMaxHp = combat.PlayerMaxHp,
            PlayerBlock = combat.PlayerBlock,
            Energy = combat.Energy,
            MaxEnergy = combat.MaxEnergy,
            PlayerGold = combat.PlayerGold,
            Hand = [.. combat.Hand],
            DrawPile = [.. combat.DrawPile],
            DiscardPile = [.. combat.DiscardPile],
            ExhaustPile = [.. combat.ExhaustPile],
            ReturnToHandBeforeDraw = [.. combat.ReturnToHandBeforeDraw],
            AutoPlayQueue = [.. combat.AutoPlayQueue],
            Orbs = [.. combat.Orbs],
            OrbCapacity = combat.OrbCapacity,
            OstyHp = combat.OstyHp,
            OstyMaxHp = combat.OstyMaxHp,
            Stars = combat.Stars,
            PotionSlots = (int[])combat.PotionSlots.Clone(),
            MaxPotionSlots = combat.MaxPotionSlots,
            Relics = [.. combat.Relics],
            PlayerBuffs = [.. combat.PlayerBuffs],
            PlayerDebuffsAtRoundStart = [.. combat.PlayerDebuffsAtRoundStart],
            Enemies = [.. combat.Enemies.Select(enemy => enemy.Clone())],
            EncounterId = combat.EncounterId,
            IsEliteCombat = combat.IsEliteCombat,
            ShuffleRng = combat.ShuffleRng?.Clone(),
            TargetRng = combat.TargetRng?.Clone(),
            CardSelectionRng = combat.CardSelectionRng?.Clone(),
            CardGenerationRng = combat.CardGenerationRng?.Clone(),
            PotionGenerationRng = combat.PotionGenerationRng?.Clone(),
            // A CountingRandom carries its seed and position, so it clones exactly. A
            // plain Random does not, and only the combat-only environment supplies one
            // -- that path has no clone export, so the shared reference is unreachable.
            AiRng = combat.AiRng is CountingRandom counting ? counting.Clone() : combat.AiRng,
            NicheHpRng = combat.NicheHpRng?.Clone(),
            PendingSelection = combat.PendingSelection?.Clone(),
            AutoPlaying = combat.AutoPlaying,
            PlayedCardBonusDamage = combat.PlayedCardBonusDamage,
            Turn = combat.Turn,
            PlayerTurn = combat.PlayerTurn,
            SkillPlayedWhileSmoggy = combat.SkillPlayedWhileSmoggy,
            AttackCardsPlayedThisTurn = combat.AttackCardsPlayedThisTurn,
            AttackOrSkillCardsPlayedThisTurn = combat.AttackOrSkillCardsPlayedThisTurn,
            CardPlaysThisTurn = combat.CardPlaysThisTurn,
            CardsPlayedThisCombat = combat.CardsPlayedThisCombat,
            DrawnCardsSinceAutomationProc = combat.DrawnCardsSinceAutomationProc,
            CardsPlayedSincePanacheProc = combat.CardsPlayedSincePanacheProc,
            BlockGainsThisTurn = combat.BlockGainsThisTurn,
            PlayerHpLostThisTurn = combat.PlayerHpLostThisTurn,
            CardsExhaustedThisTurn = combat.CardsExhaustedThisTurn,
            LightningOrbsChanneledThisCombat = combat.LightningOrbsChanneledThisCombat,
            EtherealExhaustCount = combat.EtherealExhaustCount,
            UnblockedDamageHitCount = combat.UnblockedDamageHitCount,
            TargetEnemyIndex = combat.TargetEnemyIndex,
            KnownTopCount = combat.KnownTopCount,
            KnownBottomCount = combat.KnownBottomCount,
        };

    public static RunMapNode Clone(this RunMapNode node)
    {
        var copy = new RunMapNode(node.Col, node.Row)
        {
            NodeType = node.NodeType,
            EncounterId = node.EncounterId,
            CanBeModified = node.CanBeModified,
        };
        copy.Children.AddRange(node.Children);
        copy.Parents.AddRange(node.Parents);
        return copy;
    }

    /// <summary>
    /// A RunRngSet is twelve streams off one seed, so it rebuilds from the seed and
    /// then fast-forwards each stream to where the original had got to.
    /// </summary>
    public static RunRngSet CloneAt(string stringSeed, RunRngSet source)
    {
        var copy = new RunRngSet(stringSeed);
        copy.UpFront.AdvanceToCallCount(source.UpFront.CallCount);
        copy.Shuffle.AdvanceToCallCount(source.Shuffle.CallCount);
        copy.UnknownMapPoint.AdvanceToCallCount(source.UnknownMapPoint.CallCount);
        copy.CombatCardGeneration.AdvanceToCallCount(source.CombatCardGeneration.CallCount);
        copy.CombatPotionGeneration.AdvanceToCallCount(source.CombatPotionGeneration.CallCount);
        copy.CombatCardSelection.AdvanceToCallCount(source.CombatCardSelection.CallCount);
        copy.CombatEnergyCosts.AdvanceToCallCount(source.CombatEnergyCosts.CallCount);
        copy.CombatTargets.AdvanceToCallCount(source.CombatTargets.CallCount);
        copy.MonsterAi.AdvanceToCallCount(source.MonsterAi.CallCount);
        copy.Niche.AdvanceToCallCount(source.Niche.CallCount);
        copy.CombatOrbs.AdvanceToCallCount(source.CombatOrbs.CallCount);
        copy.TreasureRoomRelics.AdvanceToCallCount(source.TreasureRoomRelics.CallCount);
        return copy;
    }

    public static PlayerRngSet CloneAt(RunRngSet runRng, PlayerRngSet source)
    {
        var copy = new PlayerRngSet(runRng);
        copy.Rewards.AdvanceToCallCount(source.Rewards.CallCount);
        copy.Shops.AdvanceToCallCount(source.Shops.CallCount);
        copy.Transformations.AdvanceToCallCount(source.Transformations.CallCount);
        return copy;
    }

    /// <summary>
    /// A deep copy of the run. Pass a different <paramref name="stringSeed" /> to
    /// rebuild every stream off a new seed, which resamples everything the run has
    /// not yet paid out -- rewards, shop stock, encounter composition, shuffles --
    /// while leaving what has already happened exactly as it was.
    /// </summary>
    public static RunState Clone(this RunState state, string? stringSeed = null)
    {
        var rng = CloneAt(stringSeed ?? state.StringSeed, state.Rng);
        var mapNodes = new Dictionary<(int Col, int Row), RunMapNode>(state.MapNodes.Count);
        foreach (var (coord, node) in state.MapNodes)
        {
            mapNodes[coord] = node.Clone();
        }

        return new RunState
        {
            StringSeed = state.StringSeed,
            Rng = rng,
            PlayerRng = CloneAt(rng, state.PlayerRng),
            PlayerHp = state.PlayerHp,
            PlayerMaxHp = state.PlayerMaxHp,
            Gold = state.Gold,
            Floor = state.Floor,
            Act = state.Act,
            Phase = state.Phase,
            Deck = [.. state.Deck],
            Relics = [.. state.Relics],
            UsedUpRelics = [.. state.UsedUpRelics],
            PendingRestPotions = state.PendingRestPotions,
            PotionSlots = (int[])state.PotionSlots.Clone(),
            CurrentNodeType = state.CurrentNodeType,
            NeowOptions = (int[])state.NeowOptions.Clone(),
            RewardCards = (int[])state.RewardCards.Clone(),
            RewardGold = state.RewardGold,
            RewardPotion = state.RewardPotion,
            RewardCardPending = state.RewardCardPending,
            PendingOtherCharacterCardRewards = state.PendingOtherCharacterCardRewards,
            NeowAwaitingProceed = state.NeowAwaitingProceed,
            ReturnToRewardScreenAfterCardReward = state.ReturnToRewardScreenAfterCardReward,
            MapNodeTypes = (int[])state.MapNodeTypes.Clone(),
            MapChoices = (int[])state.MapChoices.Clone(),
            ShopCards = (int[])state.ShopCards.Clone(),
            ShopRelics = (int[])state.ShopRelics.Clone(),
            ShopPotions = (int[])state.ShopPotions.Clone(),
            ShopCosts = (int[])state.ShopCosts.Clone(),
            RewardUpgraded = (bool[])state.RewardUpgraded.Clone(),
            RelicReward = state.RelicReward,
            EventId = state.EventId,
            EventValue0 = state.EventValue0,
            EventValue1 = state.EventValue1,
            ActiveCombat = state.ActiveCombat?.Clone(),
            ActiveCombatRng = state.ActiveCombatRng?.Clone(),
            LastPlayerWon = state.LastPlayerWon,
            CompletedCombatRoomsBeforeCurrent = state.CompletedCombatRoomsBeforeCurrent,
            MapNodes = mapNodes,
            CurrentMapCoord = state.CurrentMapCoord,
            MapOptionCoords = ((int Col, int Row)?[])state.MapOptionCoords.Clone(),
            NormalEncounterSequence = (int[])state.NormalEncounterSequence.Clone(),
            EliteEncounterSequence = (int[])state.EliteEncounterSequence.Clone(),
            BossEncounterId = state.BossEncounterId,
            NormalEncountersVisited = state.NormalEncountersVisited,
            EliteEncountersVisited = state.EliteEncountersVisited,
            EventSequence = (int[])state.EventSequence.Clone(),
            EventSequenceIndex = state.EventSequenceIndex,
            WingedBootsTimesUsed = state.WingedBootsTimesUsed,
            CardRarityOffset = state.CardRarityOffset,
            PotionRewardOdds = state.PotionRewardOdds,
            PendingRelicReward = state.PendingRelicReward,
            ShopRemovalsUsed = state.ShopRemovalsUsed,
            TransformSelectedDeckIndex = state.TransformSelectedDeckIndex,
            PendingSelectionKind = state.PendingSelectionKind,
            PendingSelectionArg = state.PendingSelectionArg,
            PendingSelectionCount = state.PendingSelectionCount,
            PendingSelectionFollowUpCard = state.PendingSelectionFollowUpCard,
            PendingSelectionFollowUpCount = state.PendingSelectionFollowUpCount,
            PendingRestUpgrade = state.PendingRestUpgrade,
            RestResultPending = state.RestResultPending,
            UnknownMapPointsVisited = state.UnknownMapPointsVisited,
            UnknownMapPointMonsterOdds = state.UnknownMapPointMonsterOdds,
            UnknownMapPointEliteOdds = state.UnknownMapPointEliteOdds,
            UnknownMapPointTreasureOdds = state.UnknownMapPointTreasureOdds,
            UnknownMapPointShopOdds = state.UnknownMapPointShopOdds,
            LastResolvedRoomType = state.LastResolvedRoomType,
        };
    }
}
