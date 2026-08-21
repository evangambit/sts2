using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public static class RunRewardGenerator
{
    private const int RarityCommon = 1;
    private const int RarityUncommon = 2;
    private const int RarityRare = 3;
    private const double CardRarityBaseOffset = -0.05;
    private const double CardRarityMaxOffset = 0.4;
    private const double CardRarityGrowth = 0.005;
    private const double PotionRewardStep = 0.1;

    public static ReadOnlySpan<int> IroncladRewardPool =>
        [
            9,
            13,
            18,
            20,
            29,
            31,
            46,
            45,
            47,
            50,
            58,
            60,
            66,
            69,
            87,
            95,
            99,
            113,
            114,
            119,
            141,
            142,
            147,
            150,
            155,
            174,
            175,
            183,
            185,
            188,
            189,
            195,
            205,
            238,
            240,
            246,
            247,
            254,
            261,
            262,
            263,
            265,
            268,
            272,
            273,
            295,
            313,
            328,
            332,
            334,
            339,
            349,
            353,
            358,
            364,
            374,
            378,
            381,
            396,
            404,
            414,
            421,
            433,
            454,
            455,
            462,
            464,
            465,
            466,
            486,
            492,
            493,
            494,
            505,
            508,
            516,
            517,
            519,
            521,
            525,
            526,
            529,
            533,
            538,
        ];

    public static ReadOnlySpan<int> IroncladTransformPool =>
        [
            9,
            13,
            18,
            20,
            29,
            31,
            46,
            45,
            47,
            50,
            58,
            59,
            60,
            66,
            69,
            546,
            87,
            95,
            99,
            107,
            113,
            114,
            119,
            141,
            142,
            147,
            150,
            155,
            174,
            175,
            185,
            188,
            189,
            195,
            205,
            238,
            240,
            246,
            247,
            254,
            261,
            262,
            263,
            265,
            268,
            272,
            273,
            295,
            313,
            328,
            332,
            334,
            339,
            349,
            353,
            358,
            364,
            374,
            378,
            381,
            404,
            414,
            421,
            433,
            454,
            462,
            464,
            465,
            466,
            486,
            492,
            493,
            494,
            505,
            508,
            516,
            517,
            519,
            525,
            526,
            529,
            533,
            538,
        ];

    public static ReadOnlySpan<int> ColorlessRewardPool =>
        [
            10,
            14,
            23,
            32,
            34,
            38,
            51,
            73,
            80,
            121,
            146,
            153,
            168,
            170,
            173,
            181,
            191,
            193,
            197,
            213,
            225,
            234,
            250,
            255,
            260,
            266,
            270,
            271,
            277,
            286,
            297,
            300,
            306,
            307,
            327,
            333,
            342,
            343,
            363,
            365,
            366,
            369,
            372,
            380,
            394,
            396,
            401,
            406,
            411,
            415,
            416,
            417,
            431,
            455,
            470,
            491,
            498,
            499,
            504,
            506,
            521,
            522,
            535,
        ];

    public static ReadOnlySpan<int> ShopAttackCards =>
        [
            13,
            20,
            50,
            60,
            69,
            87,
            147,
            189,
            240,
            247,
            268,
            349,
            358,
            421,
            454,
            465,
            486,
            508,
            519,
            538,
        ];

    public static ReadOnlySpan<int> ShopSkillCards =>
        [18, 31, 46, 45, 150, 155, 174, 175, 205, 238, 396, 414, 433, 455, 493, 516, 517, 521];

    public static ReadOnlySpan<int> ShopPowerCards => [185, 265, 273, 462, 533];

    public static ReadOnlySpan<int> PotionRewardPool =>
        [
            6, // Blood Potion
            55, // Soldier's Stew
            1,
            2,
            4,
            5,
            8,
            9,
            10,
            13,
            14,
            15,
            17,
            18,
            19,
            21,
            22,
            23,
            24,
            26,
            28,
            29,
            30,
            32,
            34,
            36,
            37,
            38,
            40,
            42,
            48,
            49,
            50,
            51,
            53,
            54,
            56,
            57,
            58,
            59,
            60,
            62,
            63,
        ];

    public static ReadOnlySpan<int> RelicRewardPool =>
        [
            3,
            4,
            9,
            10,
            19,
            23,
            41,
            110,
            114,
            128,
            135,
            144,
            149,
            169,
            170,
            172,
            186,
            190,
            215,
            250,
            252,
            279,
            282,
            286,
        ];

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

    public static bool HasPendingRewards(RunState state)
    {
        return state.RewardGold != 0
            || state.RewardPotion != 0
            || state.RelicReward != 0
            || state.RewardCardPending;
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

        return false;
    }

    public static void ClearRewardScreen(RunState state)
    {
        state.RewardGold = 0;
        state.RewardPotion = 0;
        state.RewardCardPending = false;
        state.ReturnToRewardScreenAfterCardReward = false;
        state.RelicReward = 0;
    }

    private static void PopulateCardReward(RunState state)
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
            state.ShopCosts[i] = i == saleIndex ? cost / 2 : cost;
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

        _ = RollRelicRarity(state.PlayerRng.Rewards);
        _ = RollRelicRarity(state.PlayerRng.Rewards);
        for (int i = 0; i < state.ShopRelics.Length; i++)
        {
            state.ShopRelics[i] = NextRelic(state);
            state.ShopCosts[7 + i] = ShopRelicCost(state.ShopRelics[i], state.PlayerRng.Shops);
        }

        var potionBlacklist = new List<int>();
        for (int i = 0; i < state.ShopPotions.Length; i++)
        {
            int potion = NextPotion(state, state.PlayerRng.Shops, potionBlacklist);
            state.ShopPotions[i] = potion;
            potionBlacklist.Add(potion);
            state.ShopCosts[10 + i] = ShopPotionCost(potion, state.PlayerRng.Shops);
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

        int maxPotionSlots = Math.Min(2, state.PotionSlots.Length);
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

    public static int NextRelic(RunState state)
    {
        var available = RelicRewardPool
            .ToArray()
            .Where(relicId => state.Relics.All(relic => relic.DefId != relicId))
            .ToArray();
        return available.Length == 0
            ? state.Rng.UpFront.NextItem(RelicRewardPool.ToArray())
            : state.Rng.UpFront.NextItem(available);
    }

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
            _ => RollCardRarity(state, (0.0149, 0.37), mutateOffset: true, state.PlayerRng.Rewards),
        };
    }

    private static int RollCardRarity(
        RunState state,
        (double Rare, double Uncommon) odds,
        bool mutateOffset,
        GameRng rng
    )
    {
        double offset = odds.Rare >= 1.0 ? 0.0 : state.CardRarityOffset;
        double roll = rng.NextDouble();
        double rareThreshold = odds.Rare + offset;
        int rarity =
            roll < rareThreshold ? RarityRare
            : roll < rareThreshold + odds.Uncommon ? RarityUncommon
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
                .Where(cardId => !blacklist.Contains(cardId) && CardRarity(cardId) == allowedRarity)
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

    private static int ShopCardCost(int cardId, bool colorless, GameRng rng)
    {
        int baseCost = CardRarity(cardId) switch
        {
            RarityRare => 150,
            RarityUncommon => 75,
            _ => 50,
        };
        if (colorless)
        {
            baseCost = RoundPositive(baseCost * 1.15);
        }

        return RoundPositive(baseCost * NextDouble(rng, 0.95, 1.05));
    }

    private static int ShopRelicCost(int relicId, GameRng rng)
    {
        int baseCost = ShopRelicBaseCosts.GetValueOrDefault(relicId, 200);
        return RoundPositive(baseCost * NextDouble(rng, 0.85, 1.15));
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

    private static int RoundPositive(double value) => (int)(value + 0.5);

    private static bool HasRelic(RunState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);

    private static int CardRarity(int cardId) =>
        CardRarityById.GetValueOrDefault(cardId, RarityCommon);

    private static int PotionRarity(int potionId) =>
        PotionRarityById.GetValueOrDefault(potionId, RarityCommon);

    private static int[] RarityFallbacks(int rarity) =>
        rarity switch
        {
            RarityCommon => [RarityCommon, RarityUncommon, RarityRare],
            RarityUncommon => [RarityUncommon, RarityRare, RarityCommon],
            _ => [RarityRare, RarityCommon, RarityUncommon],
        };

    private static readonly Dictionary<int, int> CardRarityById = new()
    {
        [9] = 3,
        [10] = 3,
        [13] = 1,
        [14] = 3,
        [18] = 1,
        [20] = 2,
        [23] = 2,
        [29] = 3,
        [31] = 2,
        [32] = 3,
        [34] = 3,
        [38] = 2,
        [45] = 1,
        [46] = 1,
        [47] = 2,
        [50] = 1,
        [51] = 3,
        [58] = 3,
        [60] = 1,
        [66] = 2,
        [69] = 2,
        [73] = 3,
        [80] = 2,
        [87] = 1,
        [95] = 2,
        [99] = 3,
        [113] = 3,
        [114] = 3,
        [119] = 3,
        [121] = 2,
        [141] = 3,
        [142] = 2,
        [146] = 2,
        [147] = 2,
        [150] = 2,
        [153] = 2,
        [155] = 2,
        [168] = 3,
        [170] = 2,
        [173] = 3,
        [174] = 2,
        [175] = 2,
        [181] = 2,
        [183] = 3,
        [185] = 2,
        [188] = 3,
        [189] = 2,
        [191] = 2,
        [193] = 2,
        [195] = 2,
        [197] = 2,
        [205] = 2,
        [213] = 2,
        [225] = 3,
        [234] = 3,
        [238] = 1,
        [240] = 1,
        [246] = 3,
        [247] = 2,
        [250] = 3,
        [254] = 2,
        [255] = 2,
        [260] = 2,
        [261] = 3,
        [262] = 2,
        [263] = 2,
        [265] = 2,
        [266] = 2,
        [268] = 1,
        [270] = 2,
        [271] = 3,
        [272] = 3,
        [273] = 2,
        [277] = 3,
        [286] = 2,
        [295] = 3,
        [297] = 3,
        [300] = 3,
        [306] = 3,
        [307] = 2,
        [313] = 1,
        [327] = 3,
        [328] = 3,
        [332] = 3,
        [333] = 2,
        [334] = 3,
        [339] = 3,
        [342] = 2,
        [343] = 2,
        [349] = 1,
        [353] = 2,
        [358] = 1,
        [363] = 2,
        [364] = 3,
        [365] = 2,
        [366] = 2,
        [369] = 2,
        [372] = 2,
        [374] = 3,
        [378] = 2,
        [380] = 3,
        [381] = 2,
        [394] = 3,
        [396] = 2,
        [401] = 3,
        [404] = 2,
        [406] = 3,
        [411] = 3,
        [414] = 2,
        [415] = 3,
        [416] = 3,
        [417] = 2,
        [421] = 1,
        [431] = 2,
        [433] = 1,
        [454] = 2,
        [455] = 2,
        [462] = 2,
        [464] = 3,
        [465] = 2,
        [466] = 2,
        [470] = 2,
        [486] = 1,
        [491] = 2,
        [492] = 3,
        [493] = 2,
        [494] = 3,
        [498] = 2,
        [499] = 3,
        [504] = 2,
        [505] = 3,
        [506] = 2,
        [508] = 1,
        [516] = 1,
        [517] = 1,
        [519] = 1,
        [521] = 2,
        [522] = 2,
        [525] = 3,
        [526] = 2,
        [529] = 2,
        [533] = 2,
        [535] = 2,
        [538] = 2,
    };

    private static readonly Dictionary<int, int> PotionRarityById = new()
    {
        [1] = 2,
        [2] = 1,
        [3] = 3,
        [4] = 2,
        [5] = 1,
        [6] = 1,
        [8] = 3,
        [9] = 2,
        [10] = 1,
        [13] = 2,
        [14] = 1,
        [15] = 3,
        [16] = 3,
        [17] = 2,
        [18] = 1,
        [19] = 3,
        [21] = 1,
        [22] = 3,
        [23] = 1,
        [24] = 1,
        [26] = 2,
        [28] = 3,
        [29] = 2,
        [30] = 2,
        [32] = 3,
        [34] = 2,
        [36] = 2,
        [37] = 3,
        [38] = 3,
        [39] = 3,
        [40] = 3,
        [42] = 2,
        [47] = 2,
        [48] = 1,
        [49] = 2,
        [50] = 2,
        [51] = 3,
        [52] = 3,
        [53] = 1,
        [54] = 3,
        [56] = 1,
        [57] = 2,
        [58] = 3,
        [59] = 1,
        [60] = 1,
        [61] = 2,
        [62] = 1,
        [63] = 1,
    };

    private static readonly Dictionary<int, int> ShopRelicBaseCosts = new()
    {
        [3] = 175,
        [4] = 175,
        [9] = 175,
        [10] = 175,
        [23] = 175,
        [41] = 275,
        [110] = 175,
        [114] = 225,
        [128] = 175,
        [135] = 200,
        [144] = 275,
        [149] = 275,
        [169] = 175,
        [170] = 275,
        [172] = 175,
        [186] = 175,
        [190] = 225,
        [215] = 175,
        [250] = 175,
        [252] = 175,
        [279] = 175,
        [282] = 175,
        [286] = 999999999,
    };
}
