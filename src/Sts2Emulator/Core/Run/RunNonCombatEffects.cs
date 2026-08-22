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

        // RelicCmd.Obtain strikes the relic from both grab bags unless it is stackable,
        // and Circlet -- the fallback -- is the only stackable relic there is. Without
        // this a relic handed over by name (the Sword of Stone, a Neow pick) stays in the
        // queue and can be offered a second time.
        //
        // Guarded rather than a plain Get: RunConstants carries five Neow-only relic ids
        // in the 1300-1500 range (Astrolabe = 1332, where Relics.g.cs says Astrolabe is
        // id 8) that resolve to nothing. Those are ancient relics and are not in any grab
        // bag anyway, so skipping them is correct here -- but the mismatch is real and is
        // its own bug.
        if (relicId != CircletRelic && GeneratedData.Relics.TryGet(relicId, out _))
        {
            state.RelicBag.Remove(relicId);
            state.SharedRelicBag.Remove(relicId);
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

                // The game rolls this curse from CurseCardPool on Rng.Niche, filtered
                // to CanBeGeneratedByModifiers and ordered by id. Neither the pool nor
                // that flag is extracted, so this still hands over Ascender's Bane --
                // which is at least A curse, but never the one the game picked.
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
                AddCardToDeck(state, new CardInstance(NamedCard("Greed"), Upgraded: false));
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 333);
                break;
            case RunConstants.RelicHeftyTablet:
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddCardToDeck(state, new CardInstance(NamedCard("Injury"), Upgraded: false));
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
                    new CardInstance(NamedCard("CurseOfTheBell"), Upgraded: false)
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
        state.EventPage = 0;
        state.CrystalSphere = null;
        state.CrystalSphereRng = null;
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

    internal static bool IsEventAllowed(RunState state, int eventId)
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
            // CrystalSphere.IsAllowed also wants CurrentActIndex > 0, so the sphere
            // never turns up in Act 1 however much gold the run is holding.
            RunConstants.EventCrystalSphere => state.Gold >= 100
                && state.Act > RunConstants.ActOvergrowth,
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

    /// <summary>
    /// The book offers Sharp to Attacks, Nimble to Skills and Swift to Powers, so a card
    /// is a candidate for SOME page of it when its own type's enchantment would take.
    /// </summary>
    public static bool CardCanReceiveSelfHelpBookEnchantment(CardInstance card) =>
        Enchantments.CanEnchant(card, Enchantment.Sharp)
        || Enchantments.CanEnchant(card, Enchantment.Nimble)
        || Enchantments.CanEnchant(card, Enchantment.Swift);

    public static Enchantment SelfHelpBookEnchantment(int enchantType) =>
        enchantType switch
        {
            0 => Enchantment.Sharp,
            1 => Enchantment.Nimble,
            2 => Enchantment.Swift,
            _ => Enchantment.None,
        };

    /// <summary>
    /// Open a deck selection: the run moves to the card-select screen and stays there
    /// until <paramref name="count"/> cards have been chosen. Refuses -- and leaves the
    /// run where it was -- when no card in the deck is eligible, which is what an event
    /// with a locked option is telling the player.
    /// </summary>
    public static bool BeginDeckSelection(
        RunState state,
        DeckSelection kind,
        int arg,
        int count = 1,
        int followUpCard = 0,
        int followUpCount = 0,
        int followUpHpLoss = 0
    )
    {
        state.PendingSelectionKind = kind;
        state.PendingSelectionArg = arg;
        state.PendingSelectionCount = count;
        state.PendingSelectionFollowUpCard = followUpCard;
        state.PendingSelectionFollowUpCount = followUpCount;
        state.PendingSelectionFollowUpHpLoss = followUpHpLoss;
        if (!Enumerable.Range(0, state.Deck.Count).Any(i => CanSelectCard(state, i)))
        {
            ClearDeckSelection(state);
            return false;
        }

        state.Phase = RunPhase.TransformSelect;
        return true;
    }

    public static void ClearDeckSelection(RunState state)
    {
        state.PendingSelectionKind = DeckSelection.None;
        state.PendingSelectionArg = 0;
        state.PendingSelectionCount = 0;
        state.PendingSelectionFollowUpCard = 0;
        state.PendingSelectionFollowUpCount = 0;
        state.PendingSelectionFollowUpHpLoss = 0;
    }

    /// <summary>
    /// Pay out whatever the event owes once the selection is done -- the curse Luminous
    /// Choir and the Wellspring hand over in exchange for the removal.
    /// </summary>
    public static void ResolveDeckSelectionFollowUp(RunState state)
    {
        for (int i = 0; i < state.PendingSelectionFollowUpCount; i++)
        {
            AddCardToDeck(
                state,
                new CardInstance(state.PendingSelectionFollowUpCard, Upgraded: false)
            );
        }

        state.PlayerHp = Math.Max(0, state.PlayerHp - state.PendingSelectionFollowUpHpLoss);
    }

    /// <summary>Whether the pending selection would take the card at this deck index.</summary>
    public static bool CanSelectCard(RunState state, int deckIndex)
    {
        if ((uint)deckIndex >= (uint)state.Deck.Count)
        {
            return false;
        }

        var card = state.Deck[deckIndex];
        return state.PendingSelectionKind switch
        {
            DeckSelection.Enchant => Enchantments.CanEnchant(
                card,
                (Enchantment)state.PendingSelectionArg
            ),
            // CardSelectCmd.FromDeckGeneric(c => c.IsTransformable && c.Rarity == Basic)
            // -- Wood Carvings carves a Basic card into a Peck or a Toric Toughness.
            DeckSelection.TransformTo => GeneratedData.Cards.Get(card.DefId).Rarity
                == CardRarity.Basic,
            DeckSelection.Upgrade => RunConstants.IsRunCardUpgradable(card),
            DeckSelection.TransformToRandom or DeckSelection.Remove => true,
            _ => false,
        };
    }

    /// <summary>
    /// Take one card for the pending selection. Returns false -- changing nothing -- when
    /// the index is out of range or the card is not one the selection would take.
    /// </summary>
    public static bool ApplyDeckSelection(RunState state, int deckIndex)
    {
        if (!CanSelectCard(state, deckIndex))
        {
            return false;
        }

        var card = state.Deck[deckIndex];
        switch (state.PendingSelectionKind)
        {
            case DeckSelection.Enchant:
                state.Deck[deckIndex] = card with
                {
                    Enchantment = (Enchantment)state.PendingSelectionArg,
                    // Self-Help Book applies at 2 (its Enchantment*Amount vars); every
                    // event enchantment is CardCmd.Enchant<T>(card, 1m).
                    EnchantAmount = SelfHelpBookAmount((Enchantment)state.PendingSelectionArg),
                };
                break;
            case DeckSelection.TransformTo:
                state.Deck[deckIndex] = new CardInstance(
                    state.PendingSelectionArg,
                    Upgraded: false
                );
                break;
            case DeckSelection.Upgrade:
                state.Deck[deckIndex] = card with { Upgraded = true };
                break;
            case DeckSelection.TransformToRandom:
                TransformCardAt(state, deckIndex, state.Rng.Niche);
                break;
            case DeckSelection.Remove:
                state.Deck.RemoveAt(deckIndex);
                break;
            default:
                return false;
        }

        state.PendingSelectionCount--;
        return true;
    }

    private static int SelfHelpBookAmount(Enchantment enchantment) =>
        enchantment is Enchantment.Sharp or Enchantment.Nimble or Enchantment.Swift ? 2 : 1;

    /// <summary>
    /// What Luminous Choir asks for its tribute. The event starts from a GoldVar of 149
    /// and, on generate, takes off <c>Rng.NextInt(0, 50)</c> from its own stream -- so
    /// the price is somewhere in 100..149 and the option is locked below it.
    /// </summary>
    public static int LuminousChoirTributeCost(RunState state) =>
        149 - EventRng(state, "LUMINOUS_CHOIR").NextInt(0, 50);

    /// <summary>
    /// What the sphere charges to Uncover Future. <c>CrystalSphere.CalculateVars</c> adds
    /// <c>Rng.NextInt(1, 50)</c> to a base of 50, so the price is 51..99 -- and since the
    /// event only turns up on a run holding at least 100 gold, it is always affordable and
    /// neither option is ever locked.
    ///
    /// The draw matters beyond the price: it is the first thing taken from the event's
    /// stream, and the board the minigame lays out is drawn from the same stream straight
    /// after. A cost read off a fresh Rng would leave the board one draw early.
    /// </summary>
    public static int CrystalSphereCost(RunState state)
    {
        EnsureCrystalSphereVars(state);
        return state.EventValue0!.Value;
    }

    private static void EnsureCrystalSphereVars(RunState state)
    {
        if (state.CrystalSphereRng is not null)
        {
            return;
        }

        state.CrystalSphereRng = EventRng(state, "CRYSTAL_SPHERE");
        state.EventValue0 = 50 + state.CrystalSphereRng.NextInt(1, 50);
    }

    /// <summary>
    /// Opens the sphere with the given number of divinations: three for Uncover Future,
    /// six for the Payment Plan. Both options roll nothing of their own -- one spends gold,
    /// the other adds a Debt -- so the board is the same either way for a given seed.
    /// </summary>
    public static void OpenCrystalSphere(RunState state, int divinations)
    {
        EnsureCrystalSphereVars(state);
        state.CrystalSphere = CrystalSphereGame.Create(state.CrystalSphereRng!, divinations);
        state.Phase = RunPhase.CrystalSphere;
    }

    /// <summary>
    /// What uncovering a thing does on the spot. Only the curse acts immediately -- its
    /// <c>RevealItem</c> puts a Doubt in the deck there and then; everything else waits for
    /// the last divination and arrives as a reward.
    /// </summary>
    public static void RevealCrystalSphereItem(RunState state, CrystalSphereItem item)
    {
        if (item.Kind == CrystalSphereItemKind.Curse)
        {
            AddCardToDeck(state, new CardInstance(NamedCard("Doubt"), Upgraded: false));
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
        switch (state.EventId)
        {
            case RunConstants.EventSunkenTreasury:
                CalculateSunkenTreasuryVars(state);
                break;
            case RunConstants.EventEndlessConveyor:
                CalculateEndlessConveyorVars(state);
                break;
            case RunConstants.EventSunkenStatue:
                CalculateSunkenStatueVars(state);
                break;
            case RunConstants.EventDenseVegetation:
                CalculateDenseVegetationVars(state);
                break;
            case RunConstants.EventWhisperingHollow:
                CalculateWhisperingHollowVars(state);
                break;
            case RunConstants.EventJungleMazeAdventure:
                CalculateJungleMazeVars(state);
                break;
            case RunConstants.EventThisOrThat:
                CalculateThisOrThatVars(state);
                break;
        }
    }

    /// <summary>
    /// The gold the statue's pool pays. <c>SunkenStatue.CalculateVars</c> takes a
    /// GoldVar of 111 and adds <c>Rng.NextInt(-10, 11)</c> from the event's own stream,
    /// so the amount is 101..121 and is rolled when the event is generated -- before
    /// either option is taken, which is why it cannot be rolled inside the option.
    /// </summary>
    public static int SunkenStatueGold(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateSunkenStatueVars(state);
        }

        return state.EventValue0!.Value;
    }

    private static void CalculateSunkenStatueVars(RunState state)
    {
        state.EventValue0 = 111 + EventRng(state, "SUNKEN_STATUE").NextInt(-10, 11);
    }

    /// <summary>
    /// What trudging through the vegetation pays: <c>Rng.NextInt(61, 100)</c>, rolled in
    /// CalculateVars rather than in the option.
    /// </summary>
    public static int DenseVegetationGold(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateDenseVegetationVars(state);
        }

        return state.EventValue0!.Value;
    }

    private static void CalculateDenseVegetationVars(RunState state)
    {
        state.EventValue0 = EventRng(state, "DENSE_VEGETATION").NextInt(61, 100);
    }

    /// <summary>
    /// Trash Heap's two prize tables, transcribed from the event's own literal arrays.
    /// The game lists them by class, so they are resolved by name here rather than by
    /// number: an id that stops resolving throws instead of silently picking whatever
    /// now sits at that index.
    /// </summary>
    private static readonly string[] TrashHeapRelicNames =
    [
        "DarkstonePeriapt",
        "DreamCatcher",
        "HandDrill",
        "MawBank",
        "TheBoot",
    ];

    private static readonly string[] TrashHeapCardNames =
    [
        "Caltrops",
        "Clash",
        "Distraction",
        "DualWield",
        "Entrench",
        "HelloWorld",
        "Outmaneuver",
        "Rebound",
        "RipAndTear",
        "Stack",
    ];

    public static int TrashHeapRelic(RunState state) =>
        ResolveRelic(EventRng(state, "TRASH_HEAP").NextItem(TrashHeapRelicNames));

    public static int TrashHeapCard(RunState state) =>
        ResolveCard(EventRng(state, "TRASH_HEAP").NextItem(TrashHeapCardNames));

    /// <summary>
    /// A relic an event names outright. Most events that hand over a relic hand over a
    /// SPECIFIC one -- <c>RelicCmd.Obtain&lt;ChosenCheese&gt;</c> -- and the emulator was
    /// rolling one from the reward pool at every such site, which is a different relic
    /// and also burns a draw the game never makes.
    /// </summary>
    public static int NamedRelic(string name) => ResolveRelic(name);

    /// <summary>A card an event names outright -- Wood Carvings' Peck and Toric Toughness.</summary>
    public static int NamedCard(string name) => ResolveCard(name);

    /// <summary>Circlet, the fallback relic -- the only stackable one in the game.</summary>
    public static int CircletRelic => ResolveRelic("Circlet");

    /// <summary>A potion an event names outright -- the Potion Courier's Foul Potions.</summary>
    public static int NamedPotion(string name) =>
        GeneratedData.Potions.FindId(name)
        ?? throw new InvalidOperationException($"No potion named {name}");

    /// <summary>
    /// What Whispering Hollow charges for its two potions: a GoldVar of 35 shifted by
    /// <c>Rng.NextInt(-9, 10)</c> in CalculateVars, so 26..44 -- and it is SPENT, not paid
    /// out. The event only appears at all when the run holds 44 gold.
    /// </summary>
    public static int WhisperingHollowGold(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateWhisperingHollowVars(state);
        }

        return state.EventValue0!.Value;
    }

    private static void CalculateWhisperingHollowVars(RunState state)
    {
        state.EventValue0 = 35 + EventRng(state, "WHISPERING_HOLLOW").NextInt(-9, 10);
    }

    /// <summary>
    /// Jungle Maze's two purses. CalculateVars shifts each by <c>Rng.NextFloat(-15f, 15f)</c>
    /// off the same stream, solo first, and DynamicVar.IntValue truncates the decimal.
    /// </summary>
    public static int JungleMazeSoloGold(RunState state)
    {
        EnsureJungleMazeVars(state);
        return state.EventValue0!.Value;
    }

    public static int JungleMazeJoinForcesGold(RunState state)
    {
        EnsureJungleMazeVars(state);
        return state.EventValue1!.Value;
    }

    private static void EnsureJungleMazeVars(RunState state)
    {
        if (state.EventValue0 is null || state.EventValue1 is null)
        {
            CalculateJungleMazeVars(state);
        }
    }

    /// <summary>
    /// This or That's purse: <c>Rng.NextInt(41, 69)</c>, rolled in CalculateVars.
    /// </summary>
    public static int ThisOrThatGold(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateThisOrThatVars(state);
        }

        return state.EventValue0!.Value;
    }

    private static void CalculateThisOrThatVars(RunState state)
    {
        state.EventValue0 = EventRng(state, "THIS_OR_THAT").NextInt(41, 69);
    }

    /// <summary>
    /// The card the Slippery Bridge is holding over the player. It prefers a non-Basic
    /// card and falls back to any removable one, which on a starter deck is the whole
    /// deck bar Ascender's Bane -- so it is a roll, not a fixed pick.
    /// </summary>
    public static int SlipperyBridgeCardIndex(RunState state)
    {
        var candidates = Enumerable
            .Range(0, state.Deck.Count)
            .Where(i => GeneratedData.Cards.Get(state.Deck[i].DefId).Rarity != CardRarity.Basic)
            .Where(i => IsRemovable(state.Deck[i]))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = Enumerable
                .Range(0, state.Deck.Count)
                .Where(i => IsRemovable(state.Deck[i]))
                .ToList();
        }

        return candidates.Count == 0 ? -1 : EventRng(state, "SLIPPERY_BRIDGE").NextItem(candidates);
    }

    /// <summary>
    /// <c>CardModel.IsRemovable</c>: Ascender's Bane and the quest cards stay in the deck
    /// whatever removes from it.
    /// </summary>
    private static bool IsRemovable(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Type != CardType.Quest && def.Entry != "ASCENDERS_BANE";
    }

    /// <summary>Upgrade one upgradable card, chosen off the event's own stream.</summary>
    public static bool UpgradeRandomCard(RunState state, string eventEntry)
    {
        var candidates = Enumerable
            .Range(0, state.Deck.Count)
            .Where(i => RunConstants.IsRunCardUpgradable(state.Deck[i]))
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        int index = EventRng(state, eventEntry).NextItem(candidates);
        state.Deck[index] = state.Deck[index] with { Upgraded = true };
        return true;
    }

    private static void CalculateJungleMazeVars(RunState state)
    {
        var rng = EventRng(state, "JUNGLE_MAZE_ADVENTURE");
        state.EventValue0 = (int)(150m + (decimal)rng.NextFloat(-15f, 15f));
        state.EventValue1 = (int)(50m + (decimal)rng.NextFloat(-15f, 15f));
    }

    /// <summary>Doll Room's three dolls, in the event's own declaration order.</summary>
    private static readonly string[] DollRoomDolls =
    [
        "DaughterOfTheWind",
        "MrStruggles",
        "BingBong",
    ];

    public static int DollRoomRandomDoll(RunState state) =>
        ResolveRelic(EventRng(state, "DOLL_ROOM").NextItem(DollRoomDolls));

    private static int ResolveRelic(string name) =>
        GeneratedData.Relics.FindId(name)
        ?? throw new InvalidOperationException($"No relic named {name}");

    private static int ResolveCard(string name) =>
        GeneratedData.Cards.FindId(name)
        ?? throw new InvalidOperationException($"No card named {name}");

    /// <summary>
    /// What Spiraling Whirlpool's Drink heals: a HealVar whose BaseValue is
    /// <c>MaxHp * 0.33m</c>, read through <c>DynamicVar.IntValue</c>, which is a plain
    /// <c>(int)</c> cast and therefore truncates.
    /// </summary>
    public static int SpiralingWhirlpoolHeal(RunState state) => (int)(state.PlayerMaxHp * 0.33m);

    public static int FresnelLensRelic => ResolveRelic("FresnelLens");

    public static int SwordOfStoneRelic => ResolveRelic("SwordOfStone");

    public static int GlowwaterPotion =>
        GeneratedData.Potions.FindId("GlowwaterPotion")
        ?? throw new InvalidOperationException("No potion named GlowwaterPotion");

    // ── Endless Conveyor's belt ───────────────────────────────────────────────
    // The dish is rolled before the option is even shown -- the option reads "Grab
    // Fried Eel off the Belt" -- and rolled again after every grab, so the belt is a
    // small state machine rather than a one-off. EventValue0 carries the dish now on
    // the belt and EventValue1 how many grabs have happened.

    public const int DishCaviar = 1;
    public const int DishSpicySnappy = 2;
    public const int DishJellyLiver = 3;
    public const int DishFriedEel = 4;
    public const int DishSuspiciousCondiment = 5;
    public const int DishClamRoll = 6;
    public const int DishGoldenFysh = 7;
    public const int DishSeapunkSalad = 8;

    /// <summary>The dish currently on the belt.</summary>
    public static int EndlessConveyorDish(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateEndlessConveyorVars(state);
        }

        return state.EventValue0!.Value;
    }

    private static void CalculateEndlessConveyorVars(RunState state)
    {
        state.EventValue1 = 0;
        RollDish(state);
    }

    /// <summary>
    /// EndlessConveyor.RollDish: every fifth grab is a Seapunk Salad outright, and
    /// otherwise a weighted pick over the dishes the run currently qualifies for, minus
    /// whatever was on the belt a moment ago.
    /// </summary>
    /// <remarks>
    /// The forced fifth returns before the roll, so it consumes nothing -- which is why
    /// the stream position is the grab count less the fifths, not the grab count.
    /// </remarks>
    /// <summary>Turn the belt after a grab.</summary>
    public static void RollNextConveyorDish(RunState state) => RollDish(state);

    private static void RollDish(RunState state)
    {
        int grabs = (state.EventValue1 ?? 0) + 1;
        state.EventValue1 = grabs;
        if (grabs % 5 == 0)
        {
            state.EventValue0 = DishSeapunkSalad;
            return;
        }

        int lastDish = state.EventValue0 ?? 0;
        var dishes = new List<(int Dish, float Weight)>
        {
            (DishCaviar, 6f),
            (DishSpicySnappy, 3f),
            (DishJellyLiver, 3f),
            (DishFriedEel, 3f),
        };
        if (state.PotionSlots.Any(potion => potion == 0))
        {
            dishes.Add((DishSuspiciousCondiment, 3f));
        }

        if (state.PlayerHp != state.PlayerMaxHp)
        {
            dishes.Add((DishClamRoll, 6f));
        }

        if (grabs > 1)
        {
            dishes.Add((DishGoldenFysh, 1f));
        }

        dishes.RemoveAll(dish => dish.Dish == lastDish);

        int priorRolls = grabs - 1 - ((grabs - 1) / 5);
        var rng = EventRng(state, "ENDLESS_CONVEYOR");
        rng.AdvanceToCallCount(priorRolls);

        float total = dishes.Sum(dish => dish.Weight);
        float roll = (float)rng.NextDouble() * total;
        float running = 0f;
        foreach (var (dish, weight) in dishes)
        {
            running += weight;
            if (roll < running)
            {
                state.EventValue0 = dish;
                return;
            }
        }

        state.EventValue0 = dishes[^1].Dish;
    }

    /// <summary>
    /// Eat what was grabbed, then let the belt turn. Returns false when the dish needs a
    /// screen the caller has to open.
    /// </summary>
    public static void ApplyEndlessConveyorDish(RunState state, int dish)
    {
        switch (dish)
        {
            case DishClamRoll:
                state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 10);
                break;
            case DishCaviar:
                GainMaxHp(state, 4);
                break;
            case DishGoldenFysh:
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 75);
                break;
            case DishSeapunkSalad:
                AddCardToDeck(state, new CardInstance(NamedCard("FeedingFrenzy"), Upgraded: false));
                break;
            case DishFriedEel:
                AddCardToDeck(
                    state,
                    new CardInstance(
                        RunRewardGenerator.GenerateEventOfferCards(
                            state,
                            1,
                            RunRewardGenerator.ColorlessRewardPool
                        )[0],
                        Upgraded: false
                    )
                );
                break;
            case DishSpicySnappy:
                UpgradeRandomCard(state, "ENDLESS_CONVEYOR");
                break;
            case DishSuspiciousCondiment:
                state.PendingPotionRewards.Add(
                    RunRewardGenerator.NextPotion(state, state.PlayerRng.Rewards)
                );
                break;
            default:
                break;
        }
    }

    private static void CalculateSunkenTreasuryVars(RunState state)
    {
        GameRng rng = EventRng(state, "SUNKEN_TREASURY");
        state.EventValue0 = 60 + rng.NextInt(16) - 8;
        state.EventValue1 = 333 + rng.NextInt(61) - 30;
    }

    /// <summary>
    /// An event's own stream. EventModel seeds it with
    /// <c>Rng.Seed + (IsShared ? 0 : GetPlayerSlotIndex(Owner)) + hash(Id.Entry)</c>,
    /// and a solo run's only player is slot 0 either way -- so the term is zero, not
    /// one. It was one here, which is the same off-by-one that had Neow offering the
    /// wrong relics and the player rng set reading the wrong stream. Sunken Treasury's
    /// chests paid 67 and 343 where the game pays 63 and 340.
    /// </summary>
    private static GameRng EventRng(RunState state, string eventEntry)
    {
        uint eventSeed = unchecked(
            state.Rng.Seed + (uint)DeterministicHash.GetDeterministicHashCode(eventEntry)
        );
        return new GameRng(eventSeed);
    }

    public static void GainMaxHp(RunState state, int amount)
    {
        state.PlayerMaxHp += amount;
        state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + amount);
    }

    /// <summary>
    /// The game's <c>CreatureCmd.LoseMaxHp</c>: current HP is only damaged by the amount
    /// the new maximum falls BELOW it, and the maximum itself never goes under 1. A
    /// player already below the new cap keeps the HP they had.
    /// </summary>
    public static void LoseMaxHp(RunState state, int amount)
    {
        int newMaxHp = Math.Max(1, state.PlayerMaxHp - amount);
        state.PlayerHp = Math.Min(state.PlayerHp, newMaxHp);
        state.PlayerMaxHp = newMaxHp;
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
        // What a card can become depends on what it is: the options come from the
        // original's own pool, narrowed to the rarities a run is handed, with the
        // original and the multiplayer-only cards dropped.
        var pool = RunRewardGenerator.TransformOptionsFor(originalId);
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
