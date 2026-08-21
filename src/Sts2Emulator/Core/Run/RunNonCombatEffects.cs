using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public enum RunFollowUp
{
    None,
    CardReward,
    TransformSelect,
}

public static class RunNonCombatEffects
{
    private static readonly int[] RareIroncladSingleplayerPool =
    [
        9,
        29,
        58,
        546,
        99,
        113,
        114,
        119,
        141,
        183,
        188,
        246,
        261,
        272,
        295,
        328,
        332,
        334,
        339,
        364,
        374,
        464,
        494,
        505,
        525,
    ];

    /// <summary>
    /// The game's Hook.TryModifyCardBeingAddedToDeck, which the egg relics answer: a card
    /// of their type joins the deck already upgraded. Every deck addition goes through
    /// here so an event, a shop and a card reward all agree.
    /// </summary>
    public static void AddCardToDeck(RunState state, CardInstance card)
    {
        state.Deck.Add(UpgradedByEggs(state, card));
    }

    public static CardInstance UpgradedByEggs(RunState state, CardInstance card)
    {
        if (!RunConstants.IsRunCardUpgradable(card))
        {
            return card;
        }

        int eggForType = GeneratedData.Cards.Get(card.DefId).Type switch
        {
            CardType.Power => RunConstants.RelicFrozenEgg,
            CardType.Attack => RunConstants.RelicMoltenEgg,
            CardType.Skill => RunConstants.RelicToxicEgg,
            _ => 0,
        };

        return eggForType != 0 && HasRelic(state, eggForType)
            ? card with
            {
                Upgraded = true,
            }
            : card;
    }

    /// <summary>
    /// War Paint and Whetstone: CardsVar(2) upgraded off the deck on pickup, taken from
    /// Deck.Where(type and IsUpgradable).StableShuffle(Rng.Niche).Take(2).
    /// </summary>
    private static void UpgradeRandomDeckCards(RunState state, CardType type, int count)
    {
        var candidates = Enumerable
            .Range(0, state.Deck.Count)
            .Where(index =>
                GeneratedData.Cards.Get(state.Deck[index].DefId).Type == type
                && RunConstants.IsRunCardUpgradable(state.Deck[index])
            )
            .ToList();

        // StableShuffle sorts before shuffling so the order does not depend on deck order.
        candidates.Sort((left, right) => state.Deck[left].DefId.CompareTo(state.Deck[right].DefId));
        state.Rng.Niche.Shuffle(candidates);

        foreach (int index in candidates.Take(count))
        {
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
        }
    }

    private static bool HasRelic(RunState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);

    public static RunFollowUp ApplyRelicPickup(RunState state, int relicId)
    {
        if (state.Relics.All(relic => relic.DefId != relicId))
        {
            state.Relics.Add(new RelicInstance(relicId, StartingRelicCounter(relicId)));
        }

        switch (relicId)
        {
            case RunConstants.RelicWarPaint:
                UpgradeRandomDeckCards(state, CardType.Skill, 2);
                break;
            case RunConstants.RelicWhetstone:
                UpgradeRandomDeckCards(state, CardType.Attack, 2);
                break;
            case RunConstants.RelicGoldenPearl:
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 150);
                break;
            case RunConstants.RelicNeowsTorment:
                AddCardToDeck(state, new CardInstance(RunConstants.NeowsFuryCard, Upgraded: false));
                break;
            case RunConstants.RelicNeowsBones:
                for (int i = 0; i < 2; i++)
                {
                    ApplyRelicPickup(
                        state,
                        state.Rng.UpFront.NextItem(RunConstants.NeowPositiveOptions.ToArray())
                    );
                }

                AddCardToDeck(
                    state,
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                break;
            case RunConstants.RelicNutritiousOyster:
                GainMaxHp(state, 11);
                break;
            case RunConstants.RelicStrawberry:
                GainMaxHp(state, 7);
                break;
            case RunConstants.RelicPear:
                GainMaxHp(state, 10);
                break;
            case RunConstants.RelicMango:
                GainMaxHp(state, 14);
                break;
            case RunConstants.RelicLeesWaffle:
                GainMaxHp(state, 7);
                state.PlayerHp = state.PlayerMaxHp;
                break;
            case RunConstants.RelicOldCoin:
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 300);
                break;
            case RunConstants.RelicSmallCapsule:
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                break;
            case RunConstants.RelicLargeCapsule:
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                AddCardToDeck(state, new CardInstance(472, Upgraded: false));
                AddCardToDeck(state, new CardInstance(131, Upgraded: false));
                break;
            case RunConstants.RelicPomander:
                UpgradeFirstCard(state);
                break;
            case RunConstants.RelicNeowsTalisman:
                UpgradeLastCardMatching(state, 472);
                UpgradeLastCardMatching(state, 131);
                break;
            case RunConstants.RelicCursedPearl:
                AddCardToDeck(
                    state,
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 333);
                break;
            case RunConstants.RelicHeftyTablet:
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddCardToDeck(
                    state,
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                break;
            case RunConstants.RelicKaleidoscope:
                // Handled as two card rewards in RunEngine.ApplyAncientChoice; the relic
                // offers screens to choose from, it does not put cards in the deck.
                break;
            case RunConstants.RelicArcaneScroll:
                AddCardToDeck(
                    state,
                    new CardInstance(
                        state.PlayerRng.Rewards.NextItem(RareIroncladSingleplayerPool),
                        Upgraded: false
                    )
                );
                break;
            case RunConstants.RelicLeadPaperweight:
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicPhialHolster:
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.PlayerRng.Rewards)
                );
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.PlayerRng.Rewards)
                );
                break;
            case RunConstants.RelicPreciseScissors:
                RemoveLowestPriorityCard(state);
                break;
            case RunConstants.RelicScrollBoxes:
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicLeafyPoultice:
                state.PlayerMaxHp = Math.Max(1, state.PlayerMaxHp - 12);
                state.PlayerHp = Math.Min(state.PlayerHp, state.PlayerMaxHp);
                TransformFirstCardMatching(state, 472);
                TransformFirstCardMatching(state, 131);
                break;
            case RunConstants.RelicPrecariousShears:
                RemoveLowestPriorityCard(state);
                RemoveLowestPriorityCard(state);
                state.PlayerHp = Math.Max(0, state.PlayerHp - 16);
                break;
            case RunConstants.RelicSilkenTress:
                state.Gold = 0;
                break;
            case RunConstants.RelicPandorasBox:
                TransformAllMatching(state, 472);
                TransformAllMatching(state, 131);
                break;
            case RunConstants.RelicCallingBell:
                AddCardToDeck(
                    state,
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                break;
            case RunConstants.RelicDustyTome:
                AddCardToDeck(
                    state,
                    new CardInstance(RandomRewardCard(state.Rng.UpFront), Upgraded: true)
                );
                break;
            case RunConstants.RelicPrismaticGem:
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicNewLeaf:
                state.TransformSelectedDeckIndex = null;
                return RunFollowUp.TransformSelect;
            case RunConstants.RelicAstrolabe:
                state.TransformSelectedDeckIndex = -3;
                return RunFollowUp.TransformSelect;
            case RunConstants.RelicEmptyCage:
                state.TransformSelectedDeckIndex = -2;
                return RunFollowUp.TransformSelect;
        }

        return RunFollowUp.None;
    }

    private static int StartingRelicCounter(int relicId)
    {
        return relicId == RunConstants.RelicSilverCrucible ? 3 : 0;
    }

    public static void EnterEvent(RunState state)
    {
        state.EventValue0 = null;
        state.EventValue1 = null;
        if (
            state.EventSequenceIndex == 0
            && state.Relics.Any(relic => relic.DefId == RunConstants.RelicNewLeaf)
            && IsEventAllowed(state, RunConstants.EventTheLegendsWereTrue)
        )
        {
            state.EventId = RunConstants.EventTheLegendsWereTrue;
            CalculateEventVars(state);
            state.Phase = RunPhase.Event;
            return;
        }

        List<int> eventPool = [];
        while (state.EventSequenceIndex < state.EventSequence.Length)
        {
            int eventId = state.EventSequence[state.EventSequenceIndex++];
            if (IsEventAllowed(state, eventId))
            {
                state.EventId = eventId;
                CalculateEventVars(state);
                state.Phase = RunPhase.Event;
                return;
            }
        }

        eventPool.Add(RunConstants.EventJungleMazeAdventure);
        eventPool.Add(RunConstants.EventBrainLeech);
        eventPool.Add(RunConstants.EventCrystalSphere);
        eventPool.Add(RunConstants.EventDollRoom);
        eventPool.Add(RunConstants.EventFakeMerchant);
        eventPool.Add(RunConstants.EventPotionCourier);
        eventPool.Add(RunConstants.EventRanwidTheElder);
        eventPool.Add(RunConstants.EventRelicTrader);
        eventPool.Add(RunConstants.EventRoomFullOfCheese);
        eventPool.Add(RunConstants.EventSlipperyBridge);
        eventPool.Add(RunConstants.EventStoneOfAllTime);
        eventPool.Add(RunConstants.EventSymbiote);
        eventPool.Add(RunConstants.EventTeaMaster);
        eventPool.Add(RunConstants.EventTheFutureOfPotions);
        eventPool.Add(RunConstants.EventThisOrThat);
        eventPool.Add(RunConstants.EventWarHistorianRepy);
        eventPool.Add(RunConstants.EventWelcomeToWongos);
        eventPool.Add(RunConstants.EventDoorsOfLightAndDark);
        eventPool.Add(RunConstants.EventSunkenTreasury);
        eventPool.Add(RunConstants.EventSelfHelpBook);
        if (state.Act == RunConstants.ActOvergrowth)
        {
            eventPool.Add(RunConstants.EventByrdonisNest);
            eventPool.Add(RunConstants.EventDenseVegetation);
            eventPool.Add(RunConstants.EventLuminousChoir);
            eventPool.Add(RunConstants.EventSapphireSeed);
            eventPool.Add(RunConstants.EventSunkenStatue);
            eventPool.Add(RunConstants.EventTabletOfTruth);
            eventPool.Add(RunConstants.EventWellspring);
            eventPool.Add(RunConstants.EventWhisperingHollow);
            eventPool.Add(RunConstants.EventWoodCarvings);
        }
        else
        {
            eventPool.Add(RunConstants.EventAbyssalBaths);
            eventPool.Add(RunConstants.EventDrowningBeacon);
            eventPool.Add(RunConstants.EventEndlessConveyor);
            eventPool.Add(RunConstants.EventPunchOff);
            eventPool.Add(RunConstants.EventSpiralingWhirlpool);
            eventPool.Add(RunConstants.EventSunkenStatue);
            eventPool.Add(RunConstants.EventTrashHeap);
            eventPool.Add(RunConstants.EventWaterloggedScriptorium);
        }
        if (
            state.PlayerHp >= 10
            && state.Deck.Any(card => card.DefId != RunConstants.SpoilsMapCard)
        )
        {
            eventPool.Add(RunConstants.EventTheLegendsWereTrue);
        }

        if (state.Gold >= 100 && state.Deck.Count >= 2)
        {
            eventPool.Add(RunConstants.EventMorphicGrove);
        }

        if (state.PlayerHp <= (int)(state.PlayerMaxHp * 0.7))
        {
            eventPool.Add(RunConstants.EventUnrestSite);
            eventPool.Add(RunConstants.EventAromaOfChaos);
            eventPool.Add(RunConstants.EventSimpleReward);
        }
        else
        {
            eventPool.Add(RunConstants.EventAromaOfChaos);
            eventPool.Add(RunConstants.EventSimpleReward);
        }

        state.EventId = state.Rng.UpFront.NextItem(eventPool);
        CalculateEventVars(state);
        state.Phase = RunPhase.Event;
    }

    private static bool IsEventAllowed(RunState state, int eventId)
    {
        return eventId switch
        {
            RunConstants.EventMorphicGrove => state.Gold >= 100 && state.Deck.Count >= 2,
            RunConstants.EventLuminousChoir => state.Gold >= 100 || state.Deck.Count >= 3,
            RunConstants.EventWoodCarvings => state.Deck.Any(card =>
                GeneratedData.Cards.Get(card.DefId).Rarity == CardRarity.Basic
            ),
            RunConstants.EventDrowningBeacon => state.PlayerHp > 13,
            RunConstants.EventEndlessConveyor => state.Gold >= 40,
            RunConstants.EventWaterloggedScriptorium => state.Gold >= 55 || state.Deck.Count > 0,
            RunConstants.EventSelfHelpBook => state.Deck.Any(CardCanReceiveSelfHelpBookEnchantment),
            RunConstants.EventTheLegendsWereTrue => state.PlayerHp >= 10
                && state.Deck.Any(card => card.DefId != RunConstants.SpoilsMapCard),
            RunConstants.EventCrystalSphere => state.Gold >= 100,
            RunConstants.EventRanwidTheElder => state.Gold >= 100
                || state.PotionSlots.Any(potion => potion != 0)
                || state.Relics.Count > 1,
            RunConstants.EventRelicTrader => state.Relics.Count >= 5,
            RunConstants.EventStoneOfAllTime => state.PotionSlots.Any(potion => potion != 0)
                || state.Deck.Any(CardCanReceiveSelfHelpBookEnchantment),
            RunConstants.EventTeaMaster => state.Gold >= 150,
            RunConstants.EventTheFutureOfPotions => state.PotionSlots.Count(potion => potion != 0)
                >= 2,
            RunConstants.EventWelcomeToWongos => state.Gold >= 100,
            RunConstants.EventAmalgamator => state.Deck.Count >= 2,
            RunConstants.EventFieldOfManSizedHoles => state.Deck.Count > 0,
            RunConstants.EventInfestedAutomaton => state.Deck.Count > 0,
            RunConstants.EventReflections => state.Deck.Count > 0,
            RunConstants.EventSpiritGrafter => state.Deck.Count > 0,
            RunConstants.EventTinkerTime => state.Deck.Count > 0,
            RunConstants.EventZenWeaver => state.Deck.Any(RunConstants.IsRunCardUpgradable)
                || state.PlayerHp < state.PlayerMaxHp,
            RunConstants.EventUnrestSite
            or RunConstants.EventAromaOfChaos
            or RunConstants.EventSimpleReward => true,
            _ => true,
        };
    }

    public static bool CardCanReceiveSelfHelpBookEnchantment(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Type switch
        {
            CardType.Attack => card.Sharp == 0,
            CardType.Skill => card.Nimble == 0,
            CardType.Power => card.Swift == 0,
            _ => false,
        };
    }

    public static bool CanApplySelfHelpBookEnchantment(RunState state, int deckIndex)
    {
        if ((uint)deckIndex >= (uint)state.Deck.Count)
        {
            return false;
        }

        return CanApplySelfHelpBookEnchantment(
            state.Deck[deckIndex],
            state.PendingSelfHelpBookEnchantType
        );
    }

    public static bool CanApplySelfHelpBookEnchantment(CardInstance card, int enchantType)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return enchantType switch
        {
            1 => def.Type == CardType.Attack && card.Sharp == 0,
            2 => def.Type == CardType.Skill && card.Nimble == 0,
            3 => def.Type == CardType.Power && card.Swift == 0,
            _ => false,
        };
    }

    public static bool ApplySelfHelpBookEnchantment(RunState state, int deckIndex)
    {
        if ((uint)deckIndex >= (uint)state.Deck.Count)
        {
            return false;
        }

        var card = state.Deck[deckIndex];
        switch (state.PendingSelfHelpBookEnchantType)
        {
            case 1 when CanApplySelfHelpBookEnchantment(card, 1):
                state.Deck[deckIndex] = card with { Sharp = 2 };
                return true;
            case 2 when CanApplySelfHelpBookEnchantment(card, 2):
                state.Deck[deckIndex] = card with { Nimble = 2 };
                return true;
            case 3 when CanApplySelfHelpBookEnchantment(card, 3):
                state.Deck[deckIndex] = card with { Swift = 2 };
                return true;
            default:
                return false;
        }
    }

    public static int SunkenTreasurySmallChestGold(RunState state)
    {
        EnsureSunkenTreasuryVars(state);
        return state.EventValue0!.Value;
    }

    public static int SunkenTreasuryLargeChestGold(RunState state)
    {
        EnsureSunkenTreasuryVars(state);
        return state.EventValue1!.Value;
    }

    private static void EnsureSunkenTreasuryVars(RunState state)
    {
        if (state.EventValue0 is null || state.EventValue1 is null)
        {
            CalculateSunkenTreasuryVars(state);
        }
    }

    private static void CalculateEventVars(RunState state)
    {
        if (state.EventId == RunConstants.EventSunkenTreasury)
        {
            CalculateSunkenTreasuryVars(state);
        }
    }

    private static void CalculateSunkenTreasuryVars(RunState state)
    {
        GameRng rng = EventRng(state, "SUNKEN_TREASURY");
        state.EventValue0 = 60 + rng.NextInt(16) - 8;
        state.EventValue1 = 333 + rng.NextInt(61) - 30;
    }

    private static GameRng EventRng(RunState state, string eventEntry)
    {
        uint eventSeed = unchecked(
            state.Rng.Seed + 1u + (uint)DeterministicHash.GetDeterministicHashCode(eventEntry)
        );
        return new GameRng(eventSeed);
    }

    public static void GainMaxHp(RunState state, int amount)
    {
        state.PlayerMaxHp += amount;
        state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + amount);
    }

    public static bool UpgradeFirstCard(RunState state)
    {
        for (int i = 0; i < state.Deck.Count; i++)
        {
            if (!RunConstants.IsRunCardUpgradable(state.Deck[i]))
            {
                continue;
            }

            state.Deck[i] = state.Deck[i] with { Upgraded = true };
            return true;
        }
        return false;
    }

    public static void UpgradeTwoRandomCardsWithNiche(RunState state)
    {
        var indexes = state
            .Deck.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .OrderBy(index => Math.Abs(state.Deck[index].DefId))
            .ToList();
        state.Rng.Niche.Shuffle(indexes);
        foreach (int index in indexes.Take(2))
        {
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
        }
    }

    public static void RemoveLowestPriorityCard(RunState state)
    {
        if (state.Deck.Count == 0)
        {
            return;
        }

        foreach (int cardId in new[] { RunConstants.CursePlaceholderCard, 472, 131, 30 })
        {
            int index = state.Deck.FindIndex(card => Math.Abs(card.DefId) == cardId);
            if (index >= 0)
            {
                state.Deck.RemoveAt(index);
                return;
            }
        }
        state.Deck.RemoveAt(state.Deck.Count - 1);
    }

    public static void TransformCardAt(RunState state, int deckIndex, GameRng rng)
    {
        if ((uint)deckIndex >= (uint)state.Deck.Count)
        {
            return;
        }

        int originalId = Math.Abs(state.Deck[deckIndex].DefId);
        // Transforming rolls a new card the same way a reward does, so the solo filter
        // applies here too: CardFactory.FilterForPlayerCount runs on every pool.
        var pool = RunRewardGenerator
            .IroncladTransformPool.ToArray()
            .Where(cardId => cardId != originalId && RunRewardGenerator.IsAllowedSolo(cardId))
            .ToArray();
        if (pool.Length == 0)
        {
            return;
        }

        state.Deck.RemoveAt(deckIndex);
        AddCardToDeck(state, new CardInstance(rng.NextItem(pool), Upgraded: false));
    }

    public static void TransformFirstCard(RunState state) =>
        TransformCardAt(state, 0, state.PlayerRng.Transformations);

    public static void TransformFirstCardMatching(RunState state, int cardId)
    {
        int index = state.Deck.FindIndex(card => Math.Abs(card.DefId) == cardId);
        if (index >= 0)
        {
            TransformCardAt(state, index, state.PlayerRng.Transformations);
        }
    }

    private static void TransformAllMatching(RunState state, int cardId)
    {
        for (int i = 0; i < state.Deck.Count; i++)
        {
            if (Math.Abs(state.Deck[i].DefId) == cardId)
            {
                TransformCardAt(state, i, state.PlayerRng.Transformations);
            }
        }
    }

    private static void UpgradeLastCardMatching(RunState state, int cardId)
    {
        for (int i = state.Deck.Count - 1; i >= 0; i--)
        {
            if (state.Deck[i].DefId != cardId || !RunConstants.IsRunCardUpgradable(state.Deck[i]))
            {
                continue;
            }

            state.Deck[i] = state.Deck[i] with { Upgraded = true };
            return;
        }
    }

    private static int RandomRewardCard(GameRng rng) =>
        rng.NextItem(RunRewardGenerator.IroncladRewardPool.ToArray());

    private static void AddRandomRewardCard(RunState state, GameRng rng)
    {
        AddCardToDeck(state, new CardInstance(RandomRewardCard(rng), Upgraded: false));
    }
}
