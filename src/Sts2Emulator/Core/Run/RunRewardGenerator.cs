using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public static class RunRewardGenerator
{
    private const int RarityCommon = 1;
    private const int RarityUncommon = 2;
    private const int RarityRare = 3;
    public const double CardRarityBaseOffset = -0.05;
    private const double CardRarityMaxOffset = 0.4;
    private const double CardRarityGrowth = 0.005;
    private const double PotionRewardStep = 0.1;

    /// <summary>
    /// The Ironclad card pool, straight from the game's own IroncladCardPool
    /// declaration. A hand-written copy of it used to live here, and it was wrong in
    /// both directions: it carried three Colorless cards (Restlessness, Splash,
    /// Ultimate Defend) and was missing six Ironclad ones. Every extra entry shifts
    /// the index NextItem lands on, so a reward roll that agreed with the game on
    /// rarity still handed back the neighbouring card.
    /// </summary>
    public static ReadOnlySpan<int> IroncladRewardPool => GeneratedData.CardPools.Ironclad;

    /// <summary>
    /// What a card may be transformed into, given what it is now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CardFactory.GetDefaultTransformationOptions draws from the ORIGINAL card's own
    /// pool -- an Ironclad card becomes an Ironclad card, a curse becomes a curse --
    /// falling back to Colorless only for the rarities that have no pool of their own
    /// (Ancient, Event, Token) and for Quest cards. It then keeps Common, Uncommon and
    /// Rare, drops the original itself, and drops multiplayer-only cards.
    /// </para>
    /// <para>
    /// The rarity filter is skipped when the original is a Status or a Curse, which is
    /// how a curse transforms into another curse rather than into nothing at all.
    /// </para>
    /// <para>
    /// One hand-written list used to stand for all of this. It held 83 cards where the
    /// Ironclad case has 80: it carried Break and Corruption, which are Ancient, and
    /// Demonic Shield and Tank, which are multiplayer-only, and it was missing Feed.
    /// </para>
    /// </remarks>
    public static int[] TransformOptionsFor(int originalCardId)
    {
        var original = GeneratedData.Cards.Get(originalCardId);
        bool ownPoolHasNoHome =
            original.Rarity is CardRarity.Ancient or CardRarity.Event or CardRarity.Token;
        int[] pool = ownPoolHasNoHome
            ? GeneratedData.CardPools.Colorless.ToArray()
            : PoolContaining(originalCardId);

        // Status and Curse originals keep the whole pool; everything else is narrowed to
        // the three rarities a run can actually be handed.
        bool keepEveryRarity = original.Rarity is CardRarity.Status or CardRarity.Curse;

        return
        [
            .. pool.Where(cardId =>
                cardId != originalCardId
                && IsAllowedSolo(cardId)
                && (
                    keepEveryRarity
                    || GeneratedData.Cards.Get(cardId).Rarity
                        is CardRarity.Common
                            or CardRarity.Uncommon
                            or CardRarity.Rare
                )
            ),
        ];
    }

    /// <summary>The pool a card belongs to, which is the pool it transforms within.</summary>
    private static int[] PoolContaining(int cardId)
    {
        foreach (
            var pool in (int[][])
                [
                    GeneratedData.CardPools.Ironclad.ToArray(),
                    GeneratedData.CardPools.Curse.ToArray(),
                    GeneratedData.CardPools.Status.ToArray(),
                    GeneratedData.CardPools.Colorless.ToArray(),
                ]
        )
        {
            if (pool.Contains(cardId))
            {
                return pool;
            }
        }

        // A card in no pool the emulator models: the Ironclad pool is the only sensible
        // home, and it is what the old single list assumed for everything.
        return GeneratedData.CardPools.Ironclad.ToArray();
    }

    /// <summary>
    /// The Colorless pool, as the game declares it. Callers filter it: the solo filter
    /// runs at pick time, the way CardFactory.FilterForPlayerCount runs on every pool.
    ///
    /// A hand-written copy stood here with 63 entries against the pool's 64 -- it kept
    /// ten multiplayer-only cards the pick would drop anyway and left out Coordinate for
    /// no reason anyone recorded.
    /// </summary>
    public static ReadOnlySpan<int> ColorlessRewardPool => GeneratedData.CardPools.Colorless;

    /// <summary>
    /// What the merchant can stock, by card type. The game does not keep per-type shop
    /// lists at all: MerchantInventory hands CardFactory.CreateForMerchant the player's
    /// whole character pool and it filters by type at pick time, dropping Basic cards
    /// and anything multiplayer-only.
    ///
    /// Hand-written copies of these lists used to live here and they were far short of
    /// the real thing -- 20 attacks, 18 skills and 5 powers against the pool's 35, 28
    /// and 19 -- with three Colorless cards mixed into the skills for good measure. A
    /// short pool does not just narrow the choice: NextItem indexes into it, so every
    /// slot after the first came back with a different card than the game stocked.
    /// </summary>
    private static readonly int[] _shopAttackCards = ShopPoolOfType(CardType.Attack);
    private static readonly int[] _shopSkillCards = ShopPoolOfType(CardType.Skill);
    private static readonly int[] _shopPowerCards = ShopPoolOfType(CardType.Power);

    public static ReadOnlySpan<int> ShopAttackCards => _shopAttackCards;

    public static ReadOnlySpan<int> ShopSkillCards => _shopSkillCards;

    public static ReadOnlySpan<int> ShopPowerCards => _shopPowerCards;

    private static int[] ShopPoolOfType(CardType type) =>
        [
            .. GeneratedData
                .CardPools.Ironclad.ToArray()
                .Where(cardId =>
                {
                    var def = GeneratedData.Cards.Get(cardId);
                    return def.Type == type
                        && def.Rarity != CardRarity.Basic
                        && IsAllowedSolo(cardId);
                }),
        ];

    /// <summary>
    /// What a potion roll may offer: the character's own pool followed by the shared
    /// one, which is how PotionFactory.GetPotionOptions builds it. Order is load-bearing
    /// because NextItem indexes into the concatenation.
    ///
    /// A hand-written list stood here and held 43 of the 48, so a roll that agreed with
    /// the game on rarity still landed on a different potion.
    /// </summary>
    private static readonly int[] _potionRewardPool =
    [
        .. GeneratedData.PotionPools.Ironclad.ToArray(),
        .. GeneratedData.PotionPools.Shared.ToArray(),
    ];

    public static ReadOnlySpan<int> PotionRewardPool => _potionRewardPool;

    public static void GenerateCombatRewards(RunState state)
    {
        if (HasRelic(state, RunConstants.RelicBurningBlood))
        {
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 6);
        }

        if (HasRelic(state, RunConstants.RelicBlackBlood))
        {
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 12);
        }

        if (
            HasRelic(state, RunConstants.RelicMeatOnTheBone)
            && state.PlayerHp <= state.PlayerMaxHp / 2
        )
        {
            state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 12);
        }

        bool hasPotionReward = CheckPotionRoll(state, state.PlayerRng.Rewards.NextDouble());
        ClearRewardScreen(state);
        state.RewardGold = GoldRewardForCurrentNode(state);
        if (hasPotionReward)
        {
            state.RewardPotion = NextPotion(state, state.PlayerRng.Rewards);
        }

        state.PendingRelicReward =
            state.CurrentNodeType is RunConstants.NodeElite or RunConstants.NodeBoss;
        if (state.PendingRelicReward)
        {
            state.RelicReward = NextRelic(state);
        }

        PopulateCardReward(state);
        state.RewardCardPending = true;
        state.Phase = RunPhase.RelicReward;
    }

    public static void EnterCardReward(RunState state)
    {
        state.Phase = RunPhase.CardReward;
        PopulateCardReward(state);
    }

    /// <summary>
    /// Kaleidoscope's card reward: three cards, each from a DIFFERENT character the
    /// player is not.
    ///
    /// Kaleidoscope.AfterObtained takes the pools of the other characters, StableShuffles
    /// them on the Niche stream, keeps three, and creates one card from each — then does
    /// the whole thing again for the second reward. Modelling it as "add two random
    /// Ironclad cards" got the deck size roughly right and everything else wrong: the
    /// cards came from the player's own pool, and the two reward screens the player is
    /// supposed to choose from never appeared at all.
    /// </summary>
    public static void EnterOtherCharacterCardReward(RunState state)
    {
        state.Phase = RunPhase.CardReward;
        Array.Clear(state.RewardCards);
        Array.Clear(state.RewardUpgraded);

        var pools = OtherCharacterPools(state);
        state.Rng.Niche.Shuffle(pools);

        var blacklist = new List<int>();
        for (int i = 0; i < state.RewardCards.Length && i < pools.Count; i++)
        {
            // Base odds, and the running offset neither read nor grown: Kaleidoscope
            // creates with CardCreationSource.Other, which CardFactory.RollForRarity
            // sends down RollWithBaseOdds.
            int rarity = RollCardRarity(
                state,
                RegularEncounterCardOdds,
                mutateOffset: false,
                state.PlayerRng.Rewards,
                useOffset: false
            );
            int cardId = ChooseCardWithRarity(pools[i], rarity, blacklist, state.PlayerRng.Rewards);
            state.RewardCards[i] = cardId;
            blacklist.Add(cardId);
            // Three Rewards draws per card, not two: rarity, the card itself, then the
            // upgrade roll. Skipping the third left the stream a call short per card and
            // every reward after this one read the wrong values.
            state.RewardUpgraded[i] = RollCardUpgrade(state, cardId, state.PlayerRng.Rewards);
        }
    }

    /// <summary>
    /// The character pools the player is not, sorted the way StableShuffle sorts them —
    /// by ModelId, which for these is the slugified class name, so alphabetical by
    /// character. The sort is what makes the shuffle reproducible.
    /// </summary>
    private static List<int[]> OtherCharacterPools(RunState state)
    {
        // Ironclad is the only playable character the emulator runs, so "the others" is
        // fixed; when a second character is playable this reads the player's own pool.
        return
        [
            GeneratedData.CardPools.Defect.ToArray(),
            GeneratedData.CardPools.Necrobinder.ToArray(),
            GeneratedData.CardPools.Regent.ToArray(),
            GeneratedData.CardPools.Silent.ToArray(),
        ];
    }

    public static bool HasPendingRewards(RunState state)
    {
        return state.RewardGold != 0
            || state.RewardPotion != 0
            || state.RelicReward != 0
            || state.RewardCardPending
            || state.PendingCardOffers.Count > 0
            || state.PendingOtherCharacterCardRewards > 0;
    }

    public static bool ClaimNextReward(RunState state)
    {
        return ClaimRewardAtIndex(state, 0);
    }

    public static bool ClaimRewardAtIndex(RunState state, int itemIndex)
    {
        if (state.RewardGold != 0)
        {
            if (itemIndex == 0)
            {
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, state.RewardGold);
                state.RewardGold = 0;
                OfferNextGold(state);
                return true;
            }
            itemIndex--;
        }

        if (state.RewardPotion != 0)
        {
            if (itemIndex == 0)
            {
                AddPotion(state, state.RewardPotion);
                state.RewardPotion = 0;
                return true;
            }
            itemIndex--;
        }

        if (state.RelicReward != 0)
        {
            if (itemIndex == 0)
            {
                if (state.Relics.All(relic => relic.DefId != state.RelicReward))
                {
                    state.Relics.Add(new RelicInstance(state.RelicReward));
                }

                state.RelicReward = 0;
                return true;
            }
            itemIndex--;
        }

        if (state.RewardCardPending && itemIndex == 0)
        {
            state.RewardCardPending = false;
            state.ReturnToRewardScreenAfterCardReward = true;
            state.Phase = RunPhase.CardReward;
            return true;
        }

        if (state.PendingCardOffers.Count > 0 && itemIndex == 0)
        {
            OfferNextCardOffer(state);
            state.RewardCardPending = false;
            state.ReturnToRewardScreenAfterCardReward = true;
            state.Phase = RunPhase.CardReward;
            return true;
        }

        if (state.RewardCardPending)
        {
            itemIndex--;
        }
        else if (state.PendingCardOffers.Count > 0)
        {
            itemIndex--;
        }

        // Kaleidoscope's rewards sit on the same screen as any other, one item each:
        // RewardsCmd.OfferCustom offers BOTH at once and the player answers them one at
        // a time, coming back to the screen in between.
        if (
            state.PendingOtherCharacterCardRewards > 0
            && itemIndex >= 0
            && itemIndex < state.PendingOtherCharacterCardRewards
        )
        {
            state.PendingOtherCharacterCardRewards--;
            state.ReturnToRewardScreenAfterCardReward = true;
            EnterOtherCharacterCardReward(state);
            return true;
        }

        return false;
    }

    public static void ClearRewardScreen(RunState state)
    {
        state.RewardGold = 0;
        state.RewardPotion = 0;
        state.RewardCardPending = false;
        state.ReturnToRewardScreenAfterCardReward = false;
        state.RelicReward = 0;
        state.PendingGoldRewards.Clear();
        state.PendingCardOffers.Clear();
    }

    /// <summary>Moves the next queued gold pile onto the screen, if the screen is free.</summary>
    public static void OfferNextGold(RunState state)
    {
        if (state.PendingGoldRewards.Count == 0 || state.RewardGold != 0)
        {
            return;
        }

        state.RewardGold = state.PendingGoldRewards[0];
        state.PendingGoldRewards.RemoveAt(0);
    }

    /// <summary>Moves the next queued card offer onto the card screen.</summary>
    private static void OfferNextCardOffer(RunState state)
    {
        var offer = state.PendingCardOffers[0];
        state.PendingCardOffers.RemoveAt(0);
        Array.Clear(state.RewardCards);
        Array.Clear(state.RewardUpgraded);
        for (int i = 0; i < state.RewardCards.Length && i < offer.Length; i++)
        {
            state.RewardCards[i] = offer[i];
        }
    }

    /// <summary>
    /// A card offer of a pinned rarity, rolled off a given stream: the Crystal Sphere's
    /// buried card rewards are <c>CardRarityOddsType.Uniform</c> over the character's pool
    /// filtered to one rarity, with the event's own Rng overriding the player's.
    /// CreateForReward then rolls an upgrade per card whatever the odds, so each card is
    /// two draws.
    /// </summary>
    public static int[] GenerateFixedRarityCardOffer(
        RunState state,
        int count,
        CardRarity rarity,
        GameRng rng
    )
    {
        int rarityValue = rarity switch
        {
            CardRarity.Rare => RarityRare,
            CardRarity.Uncommon => RarityUncommon,
            _ => RarityCommon,
        };
        var blacklist = new List<int>();
        var offer = new int[count];
        for (int i = 0; i < count; i++)
        {
            offer[i] = ChooseCardWithRarity(IroncladRewardPool, rarityValue, blacklist, rng);
            blacklist.Add(offer[i]);
            RollCardUpgrade(state, offer[i], rng);
        }

        return offer;
    }

    /// <summary>
    /// One potion of a named rarity, uniformly. No rarity roll first: the Crystal Sphere's
    /// potions pin their own rarity and take <c>rng.NextItem</c> over what is left, the way
    /// the Potion Courier's Ransack does.
    /// </summary>
    public static int NextPotionOfRarity(GameRng rng, Core.PotionRarity rarity)
    {
        var available = PotionRewardPool
            .ToArray()
            .Where(potionId => GeneratedData.Potions.Get(potionId).Rarity == rarity)
            .ToArray();
        return available.Length > 0
            ? rng.NextItem(available)
            : rng.NextItem(PotionRewardPool.ToArray());
    }

    /// <summary>
    /// Roll the cards an event offers on a grid -- Brain Leech's five, Room Full of
    /// Cheese's eight.
    /// </summary>
    /// <remarks>
    /// CardCreationOptions.ForNonCombatWithDefaultOdds is CardCreationSource.Other with
    /// RegularEncounter odds and the NoUpgradeRoll flag, so each card is two draws on the
    /// Rewards stream rather than three: the rarity, off the flat odds with no running
    /// offset, and the card itself. A combat reward's third draw is the upgrade roll, and
    /// this path does not make it.
    /// </remarks>
    /// <param name="fixedRarity">
    /// Set when the event pins the rarity itself, as Room Full of Cheese does by filtering
    /// its pool to Commons and asking for Uniform odds. CreateForReward skips RollForRarity
    /// entirely on the Uniform branch, so a pinned rarity is one draw per card rather than
    /// two -- getting that wrong shifts the whole grid.
    /// </param>
    public static int[] GenerateEventOfferCards(
        RunState state,
        int count,
        ReadOnlySpan<int> pool,
        CardRarity? fixedRarity = null
    )
    {
        var blacklist = new List<int>();
        var offers = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int rarity = fixedRarity switch
            {
                CardRarity.Common => RarityCommon,
                CardRarity.Uncommon => RarityUncommon,
                CardRarity.Rare => RarityRare,
                _ => RollCardRarity(
                    state,
                    RegularEncounterCardOdds,
                    mutateOffset: false,
                    state.PlayerRng.Rewards,
                    useOffset: false
                ),
            };
            int cardId = ChooseCardWithRarity(pool, rarity, blacklist, state.PlayerRng.Rewards);
            offers.Add(cardId);
            blacklist.Add(cardId);
        }

        return [.. offers];
    }

    internal static void PopulateCardReward(RunState state)
    {
        Array.Clear(state.RewardCards);
        Array.Clear(state.RewardUpgraded);
        bool silverCrucibleUpgrade = ConsumeSilverCrucibleCardRewardUpgrade(state);
        var blacklist = new List<int>();
        for (int i = 0; i < state.RewardCards.Length; i++)
        {
            int rarity = RollRewardCardRarity(state);
            int cardId = ChooseCardWithRarity(
                IroncladRewardPool,
                rarity,
                blacklist,
                state.PlayerRng.Rewards
            );
            state.RewardCards[i] = cardId;
            blacklist.Add(cardId);
            state.RewardUpgraded[i] =
                silverCrucibleUpgrade
                || RollCardUpgrade(state, cardId, state.PlayerRng.Rewards)
                // TryModifyCardRewardOptionsLate: an egg upgrades the option on the screen,
                // not just the copy that reaches the deck.
                || RunNonCombatEffects
                    .UpgradedByEggs(state, new CardInstance(cardId, Upgraded: false))
                    .Upgraded;
        }
    }

    private static bool ConsumeSilverCrucibleCardRewardUpgrade(RunState state)
    {
        int index = state.Relics.FindIndex(relic =>
            relic.DefId == RunConstants.RelicSilverCrucible && relic.Counter > 0
        );
        if (index < 0)
        {
            return false;
        }

        state.Relics[index] = state.Relics[index] with
        {
            Counter = state.Relics[index].Counter - 1,
        };
        return true;
    }

    public static void EnterRelicReward(RunState state)
    {
        state.Phase = RunPhase.RelicReward;
        state.RelicReward = NextRelic(state);
    }

    public static void EnterTreasureRoom(RunState state)
    {
        ClearRewardScreen(state);
        if (state.Relics.Any(relic => relic.DefId == RunConstants.RelicSilverCrucible))
        {
            state.Phase = RunPhase.Treasure;
            return;
        }

        int gold = state.PlayerRng.Rewards.NextInt(42, 53);
        state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, (int)(gold * 0.75));
        state.Phase = RunPhase.Treasure;
    }

    public static void EnterShop(RunState state)
    {
        // Meal Ticket's AfterRoomEntered(MerchantRoom): HealVar(15m), skipped when dead.
        if (state.PlayerHp > 0 && HasRelic(state, Effects.RelicEffects.MealTicket))
        {
            state.PlayerHp = Math.Min(
                state.PlayerMaxHp,
                state.PlayerHp + Effects.RelicEffects.MealTicketHeal
            );
        }

        state.Phase = RunPhase.Shop;
        Array.Clear(state.ShopCards);
        Array.Clear(state.ShopRelics);
        Array.Clear(state.ShopPotions);
        Array.Clear(state.ShopCosts);

        int saleIndex = state.PlayerRng.Shops.NextInt(5);
        var blacklist = new List<int>();
        int[][] typedPools =
        [
            ShopAttackCards.ToArray(),
            ShopAttackCards.ToArray(),
            ShopSkillCards.ToArray(),
            ShopSkillCards.ToArray(),
            ShopPowerCards.ToArray(),
        ];
        for (int i = 0; i < typedPools.Length; i++)
        {
            int rarity = RollCardRarity(
                state,
                (0.045, 0.37),
                mutateOffset: false,
                state.PlayerRng.Rewards
            );
            int cardId = ChooseCardWithRarity(
                typedPools[i],
                rarity,
                blacklist,
                state.PlayerRng.Shops
            );
            state.ShopCards[i] = cardId;
            blacklist.Add(cardId);
            state.PlayerRng.Rewards.NextDouble();
            int cost = ShopCardCost(cardId, colorless: false, state.PlayerRng.Shops);
            if (i == saleIndex)
            {
                // MerchantEntry.Populate calls CalcCost, and SetOnSale calls it again --
                // so the discounted slot prices itself twice, and the second roll is the
                // one that stands. Halving a single roll instead left the Shops stream a
                // draw short from here on, which is why every slot after the sale came
                // back with a different card.
                cost = ShopCardCost(cardId, colorless: false, state.PlayerRng.Shops) / 2;
            }

            state.ShopCosts[i] = cost;
        }

        for (int i = 0; i < 2; i++)
        {
            int action = 5 + i;
            int rarity = i == 0 ? RarityUncommon : RarityRare;
            int cardId = ChooseCardWithRarity(
                ColorlessRewardPool,
                rarity,
                blacklist,
                state.PlayerRng.Shops
            );
            state.ShopCards[action] = cardId;
            blacklist.Add(cardId);
            state.PlayerRng.Rewards.NextDouble();
            state.ShopCosts[action] = ShopCardCost(cardId, colorless: true, state.PlayerRng.Shops);
        }

        // MerchantInventory.PopulateRelicEntries builds its three slots as
        // [RollRarity, RollRarity, RelicRarity.Shop] and fills each at that rarity. The
        // two rolls were being made and then thrown away, with all three slots pulled at
        // Shop rarity -- so the rolls lined the stream up correctly and then the relics
        // came from the wrong queues.
        RelicRarity[] slotRarities =
        [
            RelicGrabBag.RollRarity(state.PlayerRng.Rewards),
            RelicGrabBag.RollRarity(state.PlayerRng.Rewards),
            RelicRarity.Shop,
        ];
        for (int i = 0; i < state.ShopRelics.Length; i++)
        {
            state.ShopRelics[i] = NextShopRelic(state, slotRarities[i]);
            state.ShopCosts[7 + i] = ShopRelicCost(state.ShopRelics[i], state.PlayerRng.Shops);
        }

        // MerchantInventory.PopulatePotionEntries rolls all three potions in one
        // CreateRandomPotionsOutOfCombat call and only then builds the entries that price
        // them, so the draws are three picks followed by three costs -- not a pick and a
        // cost three times over. Interleaving them read the same number of values off the
        // Shops stream in the wrong order, which left every potion after the first
        // rolling its rarity against a price.
        var potionBlacklist = new List<int>();
        for (int i = 0; i < state.ShopPotions.Length; i++)
        {
            int potion = NextPotion(state, state.PlayerRng.Shops, potionBlacklist);
            state.ShopPotions[i] = potion;
            potionBlacklist.Add(potion);
        }

        for (int i = 0; i < state.ShopPotions.Length; i++)
        {
            state.ShopCosts[10 + i] = ShopPotionCost(state.ShopPotions[i], state.PlayerRng.Shops);
        }
        state.ShopCosts[RunConstants.ShopRemoveAction] = 100 + 50 * state.ShopRemovalsUsed;

        // Membership Card's ModifyMerchantPrice: DynamicVar("Discount", 50m) as a
        // percentage of the original, applied to every entry the merchant quotes.
        if (HasRelic(state, RunConstants.RelicMembershipCard))
        {
            for (int i = 0; i < state.ShopCosts.Length; i++)
            {
                state.ShopCosts[i] /= 2;
            }
        }
    }

    public static bool AddPotion(RunState state, int potionId)
    {
        // Sozu's ShouldProcurePotion is false for its owner, and PotionCmd is the game's
        // single gate for gaining one — so this covers rewards, events and shop buys alike.
        if (HasRelic(state, Effects.RelicEffects.Sozu))
        {
            return false;
        }

        int maxPotionSlots = Math.Min(state.MaxPotionSlots, state.PotionSlots.Length);
        for (int i = 0; i < maxPotionSlots; i++)
        {
            if (state.PotionSlots[i] != 0)
            {
                continue;
            }

            state.PotionSlots[i] = potionId;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The game's <c>RelicFactory.PullNextRelicFromFront</c>: roll a rarity off the
    /// player's rewards stream, take the front of that rarity's queue, and strike it from
    /// the shared bag too. Shops call <see cref="NextShopRelic"/>, which reads the same
    /// queues from the back.
    ///
    /// This used to re-roll uniformly from a flat pool on the UpFront stream, filtered to
    /// relics the player did not already own. Wrong mechanism, wrong stream, and a rarity
    /// distribution that did not exist: the queue is the reason a run does not see the
    /// same relic twice, and the 50/33/17 rarity split is the reason it sees Commons most.
    /// </summary>
    public static int NextRelic(RunState state, GameRng? rngOverride = null) =>
        PullRelic(state, fromFront: true, rngOverride: rngOverride);

    /// <summary>
    /// A shop pull of a NAMED rarity, filtered to relics that may be sold. Welcome to
    /// Wongos asks for a Common for its bargain bin and a Rare for its featured item, so
    /// neither is the usual rolled rarity -- and both go through the shop filter, which
    /// five relics fail.
    /// </summary>
    public static int NextShopRelicOfRarity(RunState state, RelicRarity rarity)
    {
        var allowed = RelicGrabBag.AllowedInSoloRun(state.Floor);
        int? relicId = state.RelicBag.Pull(
            rarity,
            fromFront: true,
            allowed,
            relicId => GeneratedData.Relics.Get(relicId).IsAllowedInShops
        );
        if (relicId is null)
        {
            return FallbackRelic;
        }

        state.SharedRelicBag.Remove(relicId.Value);
        return relicId.Value;
    }

    /// <summary>Shops pull the same queues from the BACK.</summary>
    public static int NextShopRelic(RunState state, RelicRarity rarity = RelicRarity.Shop) =>
        PullRelic(state, fromFront: false, rarity);

    private static int PullRelic(
        RunState state,
        bool fromFront,
        RelicRarity? rarity = null,
        GameRng? rngOverride = null
    )
    {
        var rolled = rarity ?? RelicGrabBag.RollRarity(rngOverride ?? state.PlayerRng.Rewards);
        var allowed = RelicGrabBag.AllowedInSoloRun(state.Floor);
        int? relicId = state.RelicBag.Pull(rolled, fromFront, allowed);
        if (relicId is null)
        {
            // RelicFactory falls back to a fixed relic when the bag has nothing left.
            return FallbackRelic;
        }

        state.SharedRelicBag.Remove(relicId.Value);
        return relicId.Value;
    }

    /// <summary>
    /// <c>RelicFactory.FallbackRelic</c>, handed over when every queue is exhausted.
    /// </summary>
    private static int FallbackRelic =>
        GeneratedData.Relics.FindId("Circlet")
        ?? throw new InvalidOperationException("No relic named Circlet");

    private static int GoldRewardForCurrentNode(RunState state)
    {
        if (state.ActiveCombat?.EncounterId == RunConstants.GremlinMercEncounterId)
        {
            return 0;
        }

        if (state.CurrentNodeType == RunConstants.NodeElite)
        {
            return state.PlayerRng.Rewards.NextInt(26, 34);
        }

        if (state.CurrentNodeType == RunConstants.NodeBoss)
        {
            state.PlayerRng.Rewards.NextInt(100, 101);
            return 100;
        }
        return state.PlayerRng.Rewards.NextInt(7, 16);
    }

    /// <summary>The RegularEncounter rarity odds: rare, then uncommon.</summary>
    private static readonly (double Rare, double Uncommon) RegularEncounterCardOdds = (
        0.0149,
        0.37
    );

    private static int RollRewardCardRarity(RunState state)
    {
        return state.CurrentNodeType switch
        {
            RunConstants.NodeElite => RollCardRarity(
                state,
                (0.05, 0.4),
                mutateOffset: true,
                state.PlayerRng.Rewards
            ),
            RunConstants.NodeBoss => RollCardRarity(
                state,
                (1.0, 0.0),
                mutateOffset: true,
                state.PlayerRng.Rewards
            ),
            _ => RollCardRarity(
                state,
                RegularEncounterCardOdds,
                mutateOffset: true,
                state.PlayerRng.Rewards
            ),
        };
    }

    /// <param name="useOffset">
    /// Whether the running rare-chance offset applies. CardFactory.RollForRarity picks
    /// <c>Roll</c> — which reads and grows the offset — only when the card is made for an
    /// ENCOUNTER; everything else takes <c>RollWithBaseOdds</c>, which uses the flat odds
    /// and leaves the offset alone. Kaleidoscope creates with CardCreationSource.Other,
    /// so applying the offset there skewed its rarities rare AND spent the pity timer the
    /// next real combat reward was owed.
    /// </param>
    private static int RollCardRarity(
        RunState state,
        (double Rare, double Uncommon) odds,
        bool mutateOffset,
        GameRng rng,
        bool useOffset = true
    )
    {
        double offset = !useOffset || odds.Rare >= 1.0 ? 0.0 : state.CardRarityOffset;
        double roll = rng.NextDouble();
        double rareThreshold = odds.Rare + offset;
        // The two roll shapes differ in more than the offset. RollWithoutChangingFutureOdds
        // compares against rare + uncommon, so the uncommon band sits ON TOP of the rare
        // one; RollWithBaseOdds — the path a non-encounter card takes — compares against
        // the flat uncommon odds instead, which makes its uncommon band that much
        // narrower.
        double uncommonThreshold = useOffset ? rareThreshold + odds.Uncommon : odds.Uncommon;
        int rarity =
            roll < rareThreshold ? RarityRare
            : roll < uncommonThreshold ? RarityUncommon
            : RarityCommon;

        if (mutateOffset)
        {
            state.CardRarityOffset =
                rarity == RarityRare
                    ? CardRarityBaseOffset
                    : Math.Min(state.CardRarityOffset + CardRarityGrowth, CardRarityMaxOffset);
        }
        return rarity;
    }

    private static int ChooseCardWithRarity(
        ReadOnlySpan<int> pool,
        int rarity,
        List<int> blacklist,
        GameRng rng
    )
    {
        // CardFactory.FilterForPlayerCount runs on the pool before anything is rolled from
        // it, so every branch below chooses from the same solo-legal set — including the
        // last-resort one, which used to reach past the blacklist into the raw pool.
        var allowed = pool.ToArray().Where(IsAllowedSolo).ToArray();

        foreach (int allowedRarity in RarityFallbacks(rarity))
        {
            var available = allowed
                .Where(cardId => !blacklist.Contains(cardId) && RarityOf(cardId) == allowedRarity)
                .ToArray();
            if (available.Length > 0)
            {
                return rng.NextItem(available);
            }
        }

        var fallback = allowed.Where(cardId => !blacklist.Contains(cardId)).ToArray();
        return rng.NextItem(fallback.Length > 0 ? fallback : allowed);
    }

    /// <summary>
    /// CardFactory.FilterForPlayerCount: a solo run drops every MultiplayerOnly card from
    /// the pool before the rarity roll, so the choice is made over a smaller set — not just
    /// re-rolled when one comes up.
    /// </summary>
    public static bool IsAllowedSolo(int cardId) =>
        !GeneratedData.Cards.Get(cardId).MultiplayerOnly;

    private static bool RollCardUpgrade(RunState state, int cardId, GameRng rng)
    {
        _ = rng.NextDouble();
        return false;
    }

    private static bool CheckPotionRoll(RunState state, double roll)
    {
        // PotionRewardOdds.Roll draws its float whether or not the hook forces the reward,
        // so White Beast Statue must not skip the roll — only override the answer.
        bool forced =
            HasRelic(state, RunConstants.RelicWhiteBeastStatue)
            && state.CurrentNodeType
                is RunConstants.NodeNormal
                    or RunConstants.NodeElite
                    or RunConstants.NodeBoss;
        double eliteBonus = state.CurrentNodeType == RunConstants.NodeElite ? 0.25 * 0.5 : 0.0;
        if (forced || roll < state.PotionRewardOdds + eliteBonus)
        {
            state.PotionRewardOdds -= PotionRewardStep;
            return true;
        }
        state.PotionRewardOdds += PotionRewardStep;
        return false;
    }

    public static int NextPotion(RunState state, GameRng rng, List<int>? blacklist = null)
    {
        int rarity = RollPotionRarity(rng);
        var blocked = blacklist ?? [];
        var available = PotionRewardPool
            .ToArray()
            .Where(potionId => !blocked.Contains(potionId) && PotionRarity(potionId) == rarity)
            .ToArray();
        return available.Length > 0
            ? rng.NextItem(available)
            : rng.NextItem(PotionRewardPool.ToArray());
    }

    /// <summary>
    /// The Potion Courier's Ransack rolls only among Uncommon potions, so it does NOT
    /// roll a rarity first -- it filters the pool and takes one.
    /// </summary>
    public static int NextUncommonPotion(RunState state, GameRng rng)
    {
        var available = PotionRewardPool
            .ToArray()
            .Where(potionId => PotionRarity(potionId) == RarityUncommon)
            .ToArray();
        return available.Length > 0 ? rng.NextItem(available) : NextPotion(state, rng);
    }

    private static int RollPotionRarity(GameRng rng)
    {
        double roll = rng.NextDouble();
        if (roll <= 0.1)
        {
            return RarityRare;
        }

        if (roll <= 0.35)
        {
            return RarityUncommon;
        }

        return RarityCommon;
    }

    private static int RollRelicRarity(GameRng rng)
    {
        double roll = rng.NextDouble();
        return roll < 0.5 ? RarityCommon
            : roll < 0.83 ? RarityUncommon
            : RarityRare;
    }

    /// <summary>
    /// What the merchant asks for a card before any discount. Internal so a test can
    /// tell a sale slot from a cheap card.
    /// </summary>
    internal static int ShopCardCost(int cardId, bool colorless, GameRng rng)
    {
        int baseCost = RarityOf(cardId) switch
        {
            RarityRare => 150,
            RarityUncommon => 75,
            _ => 50,
        };
        if (colorless)
        {
            baseCost = RoundToEven(baseCost * 1.15f);
        }

        // MerchantCardEntry.CalcCost is float arithmetic end to end, rounded the way
        // Mathf.RoundToInt rounds.
        return RoundToEven(baseCost * NextFloat(rng, 0.95f, 1.05f));
    }

    /// <summary>
    /// The game's <c>MerchantRelicEntry.CalcCost</c>: the relic's MerchantCost jittered
    /// and rounded. MerchantCost is not per-relic data at all -- RelicModel derives it
    /// from the rarity, and the only relics that override it are the Fake Merchant's,
    /// which all sell for 50. A hand-written table of base costs used to stand in for
    /// it, defaulting anything it did not know to 200.
    /// </summary>
    private static int ShopRelicCost(int relicId, GameRng rng)
    {
        var def = GeneratedData.Relics.Get(relicId);
        int baseCost = def.Name.StartsWith("Fake", StringComparison.Ordinal)
            ? 50
            : def.Rarity switch
            {
                RelicRarity.Common => 175,
                RelicRarity.Uncommon => 225,
                RelicRarity.Rare => 275,
                RelicRarity.Shop => 200,
                RelicRarity.None => 1,
                // Ancient, Starter and Event relics are priced out of reach rather than
                // excluded, which is the game's way of saying they are never for sale.
                _ => 999999999,
            };

        return RoundToEven(baseCost * NextFloat(rng, 0.85f, 1.15f));
    }

    private static int ShopPotionCost(int potionId, GameRng rng)
    {
        int baseCost = PotionRarity(potionId) switch
        {
            RarityRare => 100,
            RarityUncommon => 75,
            _ => 50,
        };
        return RoundPositive(baseCost * NextDouble(rng, 0.95, 1.05));
    }

    private static double NextDouble(GameRng rng, double min, double max) =>
        min + rng.NextDouble() * (max - min);

    /// <summary>
    /// The game's <c>Rng.NextFloat(min, max)</c>: computed in double, then cast to
    /// float. The cast is not cosmetic -- the merchant multiplies a price by this and
    /// rounds, so carrying the extra double precision moves the odd price by one gold.
    /// </summary>
    private static float NextFloat(GameRng rng, float min, float max) =>
        (float)(rng.NextDouble() * (double)(max - min) + min);

    private static int RoundPositive(double value) => (int)(value + 0.5);

    /// <summary>
    /// Godot's <c>Mathf.RoundToInt</c>, which is <c>Math.Round</c> -- and .NET rounds a
    /// midpoint to even, not away from zero. It matters here because merchant prices land
    /// on a midpoint exactly: a Rare colourless card is 150 * 1.15f, which is 172.5f to
    /// the bit, and the two rules disagree by a gold on every one of them.
    /// </summary>
    private static int RoundToEven(double value) => (int)Math.Round(value);

    private static bool HasRelic(RunState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);

    /// <summary>
    /// A card's rarity, read from the extracted card data.
    ///
    /// This used to be a hand-written table of 144 ids that defaulted to Common — fine
    /// for the Ironclad pool it was built from, wrong for everything else: 249 Uncommon
    /// and Rare cards were absent from it and so read as Common, which let a Common roll
    /// hand back a Rare. Kaleidoscope draws from the other characters' pools and hit it
    /// on nearly every card. The table agreed with the extracted data on every id it did
    /// carry, so nothing is lost by reading the data instead.
    /// </summary>
    private static int RarityOf(int cardId) => (int)GeneratedData.Cards.Get(cardId).Rarity;

    /// <summary>
    /// The card rarity a potion trades for at The Future of Potions, from the event's own
    /// GetCardRarity: Rare and Event potions buy Rare cards, Uncommon buys Uncommon, and
    /// Common and Token buy Common.
    /// </summary>
    public static CardRarity CardRarityForPotion(int potionId) =>
        GeneratedData.Potions.Get(potionId).Rarity switch
        {
            Core.PotionRarity.Rare or Core.PotionRarity.Event => CardRarity.Rare,
            Core.PotionRarity.Uncommon => CardRarity.Uncommon,
            _ => CardRarity.Common,
        };

    /// <summary>
    /// The card types a potion can buy at The Future of Potions. Power is off the table
    /// for a Common or Token potion -- and the reason is in the pool: the Ironclad has no
    /// Common Power at all, so rolling one would filter the reward down to nothing and
    /// offer the player an empty screen.
    /// </summary>
    public static CardType[] FutureOfPotionsCardTypes(int potionId) =>
        GeneratedData.Potions.Get(potionId).Rarity
            is Core.PotionRarity.Common
                or Core.PotionRarity.Token
            ? [CardType.Attack, CardType.Skill]
            : [CardType.Attack, CardType.Skill, CardType.Power];

    /// <summary>
    /// The Future of Potions offers THREE upgraded cards, all of one rarity and one type:
    /// the rarity comes from the potion handed over, and the type is rolled per potion
    /// from Attack/Skill/Power -- with Power off the table for a Common or Token potion,
    /// which cannot buy one.
    ///
    /// The odds are uniform over the filtered pool, not the usual rarity roll, so this
    /// picks cards rather than rolling a rarity first.
    /// </summary>
    public static void EnterFutureOfPotionsReward(RunState state, int potionId, GameRng rng)
    {
        var rarity = CardRarityForPotion(potionId);
        var type = rng.NextItem(FutureOfPotionsCardTypes(potionId));

        var pool = IroncladRewardPool
            .ToArray()
            .Where(IsAllowedSolo)
            .Where(cardId =>
                GeneratedData.Cards.Get(cardId).Rarity == rarity
                && GeneratedData.Cards.Get(cardId).Type == type
            )
            .ToList();

        Array.Clear(state.RewardCards);
        Array.Clear(state.RewardUpgraded);
        for (int i = 0; i < state.RewardCards.Length && pool.Count > 0; i++)
        {
            int cardId = rng.NextItem(pool);
            pool.Remove(cardId);
            state.RewardCards[i] = cardId;
            // reward.AfterGenerated upgrades every card in the reward.
            state.RewardUpgraded[i] = true;
        }

        state.RewardGold = 0;
        state.RewardPotion = 0;
        state.RelicReward = 0;
        state.PendingPotionRewards.Clear();
        state.RewardCardPending = true;
        state.Phase = RunPhase.CardReward;
    }

    /// <summary>
    /// A potion's rarity, from the extracted potion data. A hand-written table stood
    /// here and defaulted anything it did not list to Common, which put every potion it
    /// had never heard of into the bucket a Common roll draws from.
    /// </summary>
    private static int PotionRarity(int potionId) =>
        GeneratedData.Potions.Get(potionId).Rarity switch
        {
            Core.PotionRarity.Rare => RarityRare,
            Core.PotionRarity.Uncommon => RarityUncommon,
            _ => RarityCommon,
        };

    private static int[] RarityFallbacks(int rarity) =>
        rarity switch
        {
            RarityCommon => [RarityCommon, RarityUncommon, RarityRare],
            RarityUncommon => [RarityUncommon, RarityRare, RarityCommon],
            _ => [RarityRare, RarityCommon, RarityUncommon],
        };
}
