using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public enum RunFollowUp
{
    None,
    CardReward,
    TransformSelect,

    /// <summary>A rewards screen holding relics the player must claim one at a time.</summary>
    BonusRelicRewards,

    /// <summary>
    /// A choose-a-card screen whose cards are ALREADY rolled, so the caller must not roll
    /// another offer over the top of them.
    /// </summary>
    PreRolledCardReward,

    /// <summary>Scroll Boxes' choose-a-bundle screen.</summary>
    BundleSelect,

    /// <summary>
    /// A queue of card-reward screens claimed one after another, as Glass Eye's five are.
    /// </summary>
    BonusCardOffers,
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

        // `AfterCardChangedPiles` with the destination pile being the DECK. Two relics
        // read it, and both are per-card rather than per-reward: taking three cards in one
        // Neow blessing pays three times.
        if (Effects.RelicEffects.Has(state.Relics, Effects.RelicEffects.LuckyFysh))
        {
            state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 15);
        }

        int book = state.Relics.FindIndex(relic =>
            relic.DefId == Effects.RelicEffects.BookOfFiveRings
        );
        if (book >= 0)
        {
            // `CardsAddedSinceLastTrigger` is `CardsAdded % 5`, so the heal lands on every
            // fifth card rather than once at five.
            int added = state.Relics[book].Counter + 1;
            state.Relics[book] = state.Relics[book] with { Counter = added % 5 };
            if (added % 5 == 0)
            {
                state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + 20);
            }
        }
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

    public static RunFollowUp ApplyRelicPickup(
        RunState state,
        int relicId,
        SelectionReturn returnTo = SelectionReturn.Map
    )
    {
        if (state.Relics.All(relic => relic.DefId != relicId))
        {
            state.Relics.Add(new RelicInstance(relicId, StartingRelicCounter(relicId)));

            // `PotionBelt.AfterObtained` -- GainMaxPotionCount(2). Inside the not-already-
            // held branch so a duplicate pickup cannot pay twice, which is the same reason
            // the strike-from-the-grab-bag below sits here.
            if (relicId == Effects.RelicEffects.PotionBelt)
            {
                state.MaxPotionSlots += 2;
            }
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
                // NeowsBones.AfterObtained: shuffle every relic Neow could offer except
                // itself on PlayerRng.Rewards, take two, and OFFER them -- a RewardsSet
                // with WithSkippingDisallowed, so the player claims both -- and only then
                // add the curse.
                //
                // This used to be two Rng.UpFront.NextItem draws applied on the spot: the
                // wrong stream, a draw that can hand out the SAME relic twice where a
                // shuffle-and-take cannot, no screen at all, and a candidate list of only
                // the positives. A live capture answers a rewards screen twice here and
                // comes away with Winged Boots and Silken Tress.
                state.PendingBonusRelicRewards.Clear();
                state.PendingBonusRelicRewards.AddRange(NeowsBonesRelicOffer(state));
                state.PendingNeowsBonesCurse = true;
                // Put the first one ON the screen here rather than leaving that to the
                // caller: the pickup should leave a state that makes sense however it was
                // reached, and a queue with nothing offered from it is a screen that
                // reports rewards it will not hand over.
                RunRewardGenerator.OfferNextBonusRelic(state);
                return RunFollowUp.BonusRelicRewards;
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
                // RewardsCmd.OfferCustom with a single RelicReward: the relic goes on a
                // SCREEN the player claims from, not straight into the run. Granting it
                // outright also ran its pickup effect a step early, so a capture
                // (`P14DQ9GNPW`) is on `rewards` holding two relics where the emulator
                // was back on the ancient holding three.
                state.PendingBonusRelicRewards.Clear();
                state.PendingBonusRelicRewards.Add(RunRewardGenerator.NextRelic(state));
                RunRewardGenerator.OfferNextBonusRelic(state);
                return RunFollowUp.BonusRelicRewards;
            case RunConstants.RelicLargeCapsule:
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                AddCardToDeck(state, new CardInstance(472, Upgraded: false));
                AddCardToDeck(state, new CardInstance(131, Upgraded: false));
                break;
            case RunConstants.RelicPomander:
                // CardsVar(1) through CardSelectCmd.FromDeckForUpgrade: the PLAYER picks
                // which card is upgraded. UpgradeFirstCard upgraded the deck's first
                // upgradable card, which in a starting deck is a Strike -- and a live
                // capture (`RRRR6WR3C4`) shows the game opening a card_select screen at
                // step 1 where the emulator had already silently upgraded one.
                return BeginDeckSelection(state, DeckSelection.Upgrade, 0, count: 1)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicNeowsTalisman:
                UpgradeLastCardMatching(state, 472);
                UpgradeLastCardMatching(state, 131);
                break;
            case RunConstants.RelicCursedPearl:
                AddCardToDeck(state, new CardInstance(NamedCard("Greed"), Upgraded: false));
                state.Gold += Effects.RelicEffects.ModifyGoldGained(state.Relics, 333);
                break;
            case RunConstants.RelicHeftyTablet:
                // AfterObtained offers CardsVar(3) RARE cards from the owner's own pool --
                // Uniform odds, NoUpgradeRoll -- on a choose-a-card screen, and adds its
                // Injury together with whichever the player takes. It used to hand over
                // one card rolled off Rng.UpFront and drop the Injury immediately: no
                // screen, no choice, the wrong stream and the curse a decision early.
                RunRewardGenerator.OfferPreRolledCards(
                    state,
                    RunRewardGenerator.GenerateRareOnlyCardOffer(state, 3)
                );
                state.PendingHeftyTabletCurse = true;
                return RunFollowUp.PreRolledCardReward;
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
                // Two COLOURLESS cards on a choose-a-card screen, RegularEncounter odds
                // through CardCreationSource.Other. Same story as Hefty Tablet: one card
                // off the wrong stream, granted rather than offered.
                RunRewardGenerator.OfferPreRolledCards(
                    state,
                    RunRewardGenerator.GenerateOtherSourceCardOffer(
                        state,
                        RunRewardGenerator.ColorlessRewardPool,
                        2
                    )
                );
                return RunFollowUp.PreRolledCardReward;
            case RunConstants.RelicPhialHolster:
                // PhialHolster.AfterObtained: GainMaxPotionCount(PotionSlots=1) FIRST --
                // so both of the potions it then rolls have somewhere to go -- and
                // CreateRandomPotionsOutOfCombat(Potions=2, Rng.CombatPotionGeneration).
                //
                // The stream is the whole point. These rolled off PlayerRng.Rewards, which
                // is the stream every card reward, shop and transformation in the run also
                // draws from: two draws the game never makes there put the very next
                // combat's gold reward and card offer at the wrong position, and every one
                // after it. A live capture of a run that took this from Neow paid 15 gold
                // where the emulator paid 9.
                state.MaxPotionSlots++;
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.Rng.CombatPotionGeneration)
                );
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.Rng.CombatPotionGeneration)
                );
                break;
            // ---- the act ancients' blessings -------------------------------------
            case RunConstants.RelicPaelsHorn:
                // Two Relax into the deck. The one ancient blessing a live capture pins:
                // `ACT2TEST01` takes it and comes away with two.
                AddCardToDeck(state, new CardInstance(NamedCard("Relax"), Upgraded: false));
                AddCardToDeck(state, new CardInstance(NamedCard("Relax"), Upgraded: false));
                break;
            case RunConstants.RelicStorybook:
                AddCardToDeck(
                    state,
                    new CardInstance(NamedCard("BrightestFlame"), Upgraded: false)
                );
                break;
            case RunConstants.RelicSandCastle:
                // CardsVar(6), StableShuffled on Rng.NICHE -- not the event's own stream
                // and not UpFront. Same shape as Doors of Light and Dark's light door,
                // which is worth knowing because that one taught the lesson twice: sort
                // by ModelId before shuffling, and only upgradable cards are candidates.
                UpgradeRandomCards(state, 6, state.Rng.Niche);
                break;
            case RunConstants.RelicAlchemicalCoffer:
                // PotionSlots(4): GainMaxPotionCount FIRST so all four potions have
                // somewhere to go, then four rolled off CombatPotionGeneration. Phial
                // Holster is the same shape, and the stream is the point -- rolling these
                // off PlayerRng.Rewards would move every card reward after it.
                state.MaxPotionSlots += 4;
                for (int i = 0; i < 4; i++)
                {
                    RunRewardGenerator.AddPotion(
                        state,
                        RunRewardGenerator.NextPotion(state, state.Rng.CombatPotionGeneration)
                    );
                }

                break;
            case RunConstants.RelicPaelsClaw:
                // Goopy onto EVERY card in the deck that can take it — no screen, no
                // choice. `CanEnchant` is the filter, which for Goopy is the Defend tag.
                EnchantEveryCard(state, Enchantment.Goopy, 1);
                break;
            case RunConstants.RelicNutritiousSoup:
                // Tezcatara's Ember onto every BASIC Strike. The rarity and tag checks
                // are the relic's own, on top of the enchantment's CanEnchant.
                EnchantEveryCard(
                    state,
                    Enchantment.TezcatarasEmber,
                    1,
                    card => IsBasicStrike(card)
                );
                break;
            case RunConstants.RelicElectricShrymp:
                // FromDeckForEnchantment with Imbued: ONE card the player picks, and
                // Imbued takes skills only, so a deck with no skill left is offered
                // nothing.
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.Imbued,
                    count: 1
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicPaelsGrowth:
                // Likewise, with Clone at amount FOUR rather than one.
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.Clone,
                    count: 1,
                    enchantAmount: 4
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicSeaGlass:
                // CardsVar(15) / 3 = five cards of each rarity from the OTHER character's
                // pool, all fifteen offered on one grid. Uniform odds with
                // NoRarityModification, so each card is a pick and an upgrade roll: thirty
                // draws, the same budget as Glass Eye.
                //
                // The screen is a 0-to-15 multi-select with a confirm
                // (`CardSelectorPrefs(prompt, 0, list.Count)`), and the emulator's offer
                // grid cannot express "stop early" -- it hands over picks until they run
                // out. A run that wanted only some of the fifteen will diverge here; one
                // that takes them all will not. See O16.
                state.PendingOfferCards =
                [
                    .. new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }.SelectMany(
                        rarity =>
                            RunRewardGenerator.GenerateFixedRarityCardOffer(
                                state,
                                5,
                                rarity,
                                state.PlayerRng.Rewards,
                                RunConstants.OtherCharacterPoolFor(
                                    Math.Max(0, state.SeaGlassCharacter)
                                )
                            )
                    ),
                ];
                state.PendingOfferPicks = state.PendingOfferCards.Length;
                return RunFollowUp.PreRolledCardReward;
            case RunConstants.RelicGlassEye:
                // FIVE card rewards on one screen — Common, Common, Uncommon, Uncommon,
                // Rare — each offering three of that rarity. Uniform odds with
                // NoRarityModification, so there is no rarity roll: each card is a pick
                // and an upgrade roll, two draws, thirty in all. No RngOverride, so they
                // come off PlayerRng.Rewards like any other card reward.
                state.PendingCardOffers.Clear();
                foreach (
                    CardRarity rarity in new[]
                    {
                        CardRarity.Common,
                        CardRarity.Common,
                        CardRarity.Uncommon,
                        CardRarity.Uncommon,
                        CardRarity.Rare,
                    }
                )
                {
                    state.PendingCardOffers.Add(
                        RunRewardGenerator.GenerateFixedRarityCardOffer(
                            state,
                            3,
                            rarity,
                            state.PlayerRng.Rewards
                        )
                    );
                }

                return RunFollowUp.BonusCardOffers;
            case RunConstants.RelicPaelsTooth:
                // CardsVar(5) through FromDeckForRemoval with a filter of `IsUpgradable`
                // -- five cards the player picks, and only upgradable ones are offered.
                // The relic keeps copies of what it took for its own combat effect, which
                // the run layer does not need.
                return BeginDeckSelection(state, DeckSelection.RemoveUpgradable, 0, count: 5)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicYummyCookie:
                // CardsVar(4) through FromDeckForUpgrade: four cards the PLAYER picks.
                return BeginDeckSelection(state, DeckSelection.Upgrade, 0, count: 4)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicBiiigHug:
                // CardsVar(4) through FromDeckForRemoval.
                return BeginDeckSelection(state, DeckSelection.Remove, 0, count: 4)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicPreciseScissors:
                // CardsVar(1) through CardSelectCmd.FromDeckForRemoval -- the PLAYER picks
                // which card goes, and picking is the whole of the blessing.
                // RemoveLowestPriorityCard was the emulator choosing instead, and choosing
                // badly: its priority list starts with the curse placeholder, which is
                // Ascender's Bane, a card the game will not even offer for removal.
                return BeginDeckSelection(state, DeckSelection.Remove, 0, count: 1)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicScrollBoxes:
                // GenerateRandomBundles on PlayerRng.Rewards, then
                // CardSelectCmd.FromChooseABundleScreen: the player takes ONE bundle of
                // three whole, and never sees the other three cards again. This handed
                // over three cards rolled off Rng.UpFront -- the wrong stream, the wrong
                // count of draws, six cards' worth of choice collapsed into none, and no
                // screen at all.
                state.BundleOffer =
                [
                    .. RunRewardGenerator.GenerateScrollBoxBundles(state).SelectMany(b => b),
                ];
                state.SelectedBundle = -1;
                return RunFollowUp.BundleSelect;
            case RunConstants.RelicLeafyPoultice:
                state.PlayerMaxHp = Math.Max(1, state.PlayerMaxHp - 12);
                state.PlayerHp = Math.Min(state.PlayerHp, state.PlayerMaxHp);
                TransformFirstCardMatching(state, 472);
                TransformFirstCardMatching(state, 131);
                break;
            case RunConstants.RelicPrecariousShears:
                // CardsVar(2) through the same removal screen, then DamageVar(16). The
                // damage is owed only once the cards are gone, which BeginDeckSelection's
                // follow-up already models -- the same shape Luminous Choir uses.
                return BeginDeckSelection(
                    state,
                    DeckSelection.Remove,
                    0,
                    count: 2,
                    followUpHpLoss: 16
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
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
                // CardsVar(1) through CardSelectCmd.FromDeckForTransformation, then
                // CardCmd.TransformToRandom on Rng.Niche. This used the older
                // TransformSelectedDeckIndex path, which answers the screen a step later
                // than the game does -- the `N11HWGCNUN` capture is back at the event on
                // step 3 with the card already transformed while the emulator is still
                // holding the selection open.
                return BeginDeckSelection(state, DeckSelection.TransformToRandom, 0, count: 1)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
            case RunConstants.RelicAstrolabe:
                state.TransformSelectedDeckIndex = -3;
                return RunFollowUp.TransformSelect;
            case RunConstants.RelicEmptyCage:
                state.TransformSelectedDeckIndex = -2;
                return RunFollowUp.TransformSelect;

            // The five shop relics whose AfterObtained raises a DECK-SELECTION screen.
            // All five go through the same machinery Empty Cage and the events use; what
            // differs is the kind, the count and the enchantment they apply.
            case Effects.RelicEffects.DollysMirror:
                return BeginDeckSelection(state, DeckSelection.Duplicate, 0, returnTo: returnTo)
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;

            case Effects.RelicEffects.GnarledHammer:
                // `CardSelectorPrefs(prompt, 0, 3)` -- up to THREE cards, and Sharp at 3
                // rather than the 2 Self-Help Book applies.
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.Sharp,
                    count: 3,
                    enchantAmount: 3,
                    returnTo: returnTo
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;

            case Effects.RelicEffects.Kifuda:
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.Adroit,
                    count: 3,
                    enchantAmount: 3,
                    returnTo: returnTo
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;

            case Effects.RelicEffects.PunchDagger:
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.Momentum,
                    enchantAmount: 5,
                    returnTo: returnTo
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;

            case Effects.RelicEffects.RoyalStamp:
                // The game shuffles the eligible cards on Rng.Niche before offering them,
                // which changes the ORDER of the screen and not what is on it. The
                // emulator offers the deck in deck order, so the shuffle is not modelled
                // and no stream draw is spent -- recorded rather than faked, because a
                // wrong draw on Niche desynchronises far more than a screen's ordering.
                return BeginDeckSelection(
                    state,
                    DeckSelection.Enchant,
                    (int)Enchantment.RoyallyApproved,
                    enchantAmount: 1,
                    returnTo: returnTo
                )
                    ? RunFollowUp.TransformSelect
                    : RunFollowUp.None;
        }

        return RunFollowUp.None;
    }

    /// <summary>
    /// <c>NeowsBones.GetValidRelics</c>, shuffled and cut to two.
    /// </summary>
    /// <remarks>
    /// The list is <c>Neow.AllPossibleOptions</c> in DECLARATION order -- curse options,
    /// then positive ones, then the six that are offered as one of a pair -- minus Neow's
    /// Bones itself and anything <c>IsAllowedAtNeow</c> refuses. Order is load-bearing:
    /// the shuffle that follows is over this exact sequence, so a list assembled any other
    /// way draws different relics from the same stream position. MassiveScroll is absent
    /// for the same reason it is absent from Neow's own offer -- its IsAllowed is
    /// <c>Players.Count > 1</c>.
    /// </remarks>
    private static List<int> NeowsBonesRelicOffer(RunState state)
    {
        var candidates = new List<int>();
        foreach (int relicId in RunConstants.NeowCurseOptions)
        {
            if (relicId != RunConstants.RelicNeowsBones)
            {
                candidates.Add(relicId);
            }
        }

        candidates.AddRange(RunConstants.NeowPositiveOptions);
        candidates.AddRange(RunConstants.NeowPairedOptions);
        state.PlayerRng.Rewards.Shuffle(candidates);
        return candidates.Take(2).ToList();
    }

    /// <summary>
    /// The curse Neow's Bones adds once its relics are claimed:
    /// <c>Rng.Niche.NextItem</c> over the generatable curses, ordered by id.
    /// </summary>
    public static void AddNeowsBonesCurse(RunState state)
    {
        if (!state.PendingNeowsBonesCurse)
        {
            return;
        }

        state.PendingNeowsBonesCurse = false;
        AddCardToDeck(state, new CardInstance(RollGeneratableCurse(state), Upgraded: false));
    }

    private static int StartingRelicCounter(int relicId)
    {
        return relicId == RunConstants.RelicSilverCrucible ? 3 : 0;
    }

    /// <summary>
    /// Put the run into <paramref name="eventId"/> the way the game does.
    /// </summary>
    /// <remarks>
    /// Entering an event is not just setting its id: the game calls the event's
    /// <c>CalculateVars</c> as it generates the options, and several events DRAW from
    /// their own Rng stream there. Skip it and every later draw in that event lands one
    /// position early -- which is not a visible failure, it is a different outcome. The
    /// Endless Conveyor rolls its dish in CalculateVars and nothing else touches it, so
    /// Observe the Chef upgraded the wrong card against a live capture.
    /// </remarks>
    public static void BeginEvent(RunState state, int eventId)
    {
        state.EventValue0 = null;
        state.EventValue1 = null;
        state.EventPage = 0;
        state.EventRandomOffer = [];
        state.CrystalSphere = null;
        state.CrystalSphereRng = null;
        state.EventRngStream = null;
        state.EventRngName = null;
        state.EventRelicStock = null;
        state.EventId = eventId;
        CalculateEventVars(state);
        state.Phase = RunPhase.Event;
    }

    public static void EnterEvent(RunState state)
    {
        state.EventValue0 = null;
        state.EventValue1 = null;
        state.EventPage = 0;
        state.EventRandomOffer = [];
        state.CrystalSphere = null;
        state.CrystalSphereRng = null;
        state.EventRngStream = null;
        state.EventRngName = null;
        state.EventRelicStock = null;
        if (
            state.EventSequenceIndex == 0
            && state.Relics.Any(relic => relic.DefId == RunConstants.RelicNewLeaf)
            && IsEventAllowed(state, RunConstants.EventTheLegendsWereTrue)
        )
        {
            BeginEvent(state, RunConstants.EventTheLegendsWereTrue);
            return;
        }

        // RoomSet.EnsureNextEventIsValid: NextEvent is events[visited % count], and the
        // cursor is walked forward while the candidate is disallowed OR already seen.
        // The sequence WRAPS -- it is a ring, not a list that runs out -- and the game
        // only gives up after a full lap, logging "all unique events exhausted".
        List<int> eventPool = [];
        if (state.EventSequence.Length > 0)
        {
            for (int i = 0; i < state.EventSequence.Length; i++)
            {
                int eventId = state.EventSequence[
                    state.EventSequenceIndex % state.EventSequence.Length
                ];
                if (IsEventAllowed(state, eventId) && !state.VisitedEventIds.Contains(eventId))
                {
                    // PullNextEvent records it; MarkRoomVisited moves the cursor past it.
                    state.VisitedEventIds.Add(eventId);
                    state.EventSequenceIndex++;
                    BeginEvent(state, eventId);
                    return;
                }

                state.EventSequenceIndex++;
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

        // The fallback pool is hand-written and had no eligibility check of any kind, so
        // it could hand out an event the run is not entitled to -- including the act-2
        // ones it lists by name. Everything the sequence is filtered by applies here too.
        var allowed = eventPool.Where(id => IsEventAllowed(state, id)).ToList();
        BeginEvent(
            state,
            state.Rng.UpFront.NextItem(
                allowed.Count > 0 ? allowed : [RunConstants.EventSimpleReward]
            )
        );
    }

    /// <summary>
    /// Events whose <c>IsAllowed</c> reads <c>CurrentActIndex</c> and refuses index 0.
    /// The emulator models Act 1 and only Act 1 -- a run ends at its boss -- so every one
    /// of these is unreachable, and rolling one puts a room in front of an agent that the
    /// game would never have shown it.
    ///
    /// Eight of the nine were not gated at all. The ninth, the Crystal Sphere, was gated
    /// on <c>state.Act > ActOvergrowth</c> -- but Act is which of the two Act-1 acts the
    /// run drew (Overgrowth 1, Underdocks 2), not an act INDEX, so the test was true for
    /// every Underdocks run and let the sphere into half of Act 1.
    /// </summary>
    private static readonly int[] ActTwoAndLaterEvents =
    [
        RunConstants.EventCrystalSphere,
        RunConstants.EventDollRoom,
        RunConstants.EventFakeMerchant,
        RunConstants.EventPotionCourier,
        RunConstants.EventRanwidTheElder,
        RunConstants.EventRelicTrader,
        RunConstants.EventStoneOfAllTime,
        RunConstants.EventSymbiote,
        RunConstants.EventWelcomeToWongos,
    ];

    /// <summary>Test seam for <see cref="IsEventAllowed"/>.</summary>
    public static bool IsEventAllowedForTests(RunState state, int eventId) =>
        IsEventAllowed(state, eventId);

    internal static bool IsEventAllowed(RunState state, int eventId)
    {
        if (ActTwoAndLaterEvents.Contains(eventId))
        {
            return false;
        }

        return eventId switch
        {
            RunConstants.EventMorphicGrove => state.Gold >= 100 && state.Deck.Count >= 2,
            RunConstants.EventLuminousChoir => state.Gold >= 100 || state.Deck.Count >= 3,
            RunConstants.EventWoodCarvings => state.Deck.Any(card =>
                GeneratedData.Cards.Get(card.DefId).Rarity == CardRarity.Basic
            ),
            RunConstants.EventDrowningBeacon => state.PlayerHp > 13,
            // Gold >= 120, not 40 -- the belt is a rich man's event.
            RunConstants.EventEndlessConveyor => state.Gold >= 120,
            // Gold >= 55 and nothing else. The `|| Deck.Count > 0` made this always true,
            // because a run always has a deck.
            RunConstants.EventWaterloggedScriptorium => state.Gold >= 55,
            RunConstants.EventWhisperingHollow => state.Gold >= 44,
            RunConstants.EventTrashHeap => state.PlayerHp > 5,
            RunConstants.EventSpiralingWhirlpool => state.Deck.Any(card =>
                Enchantments.CanEnchant(card, Enchantment.Spiral)
            ),
            // TotalFloor is floors across the whole run; the emulator models one act, so
            // within it that is Floor.
            RunConstants.EventPunchOff => state.Floor >= 6,
            RunConstants.EventSlipperyBridge => state.Floor > 6 && state.Deck.Any(IsRemovable),
            // WarHistorianRepy.IsAllowed is `return false` outright: it is never drawn
            // from the sequence, only reached by its own means. Falling through to the
            // default handed it out as an ordinary event.
            RunConstants.EventWarHistorianRepy => false,
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
            RunConstants.EventSpiritGrafter => state.Deck.Count > 0,
            // Reflections and TinkerTime declare no IsAllowed at all: both were gated on
            // a non-empty deck, which is a rule neither model has. The deck-size gates
            // came in with placeholder options that no longer stand.
            RunConstants.EventReflections => true,
            RunConstants.EventTinkerTime => true,
            // The weaver only turns up for a run that can afford EmotionalAwarenessCost:
            // `Players.All(p => p.Gold >= 125)`. The upgradable-or-hurt test belonged to
            // the placeholder options.
            RunConstants.EventZenWeaver => state.Gold >= RunConstants.ZenWeaverEmotionalCost,
            // The site only turns up on a run that is actually hurt: CurrentHp <= 70% of
            // max. It was grouped with the unconditional events.
            RunConstants.EventUnrestSite => state.PlayerHp <= state.PlayerMaxHp * 0.70m,
            // ByrdonisNest is `!HasEventPet()`. Pets are not modelled, and the sequence
            // offers each event at most once per act, so within an act it cannot matter.
            // BrainLeech and RoomFullOfCheese are `CurrentActIndex < 2`, and Dense
            // Vegetation only refuses in multiplayer. The emulator models a run's first
            // act, single player, so all three are open -- transcribed rather than left
            // to the default so the gate says so.
            RunConstants.EventBrainLeech
            or RunConstants.EventRoomFullOfCheese
            or RunConstants.EventDenseVegetation
            or RunConstants.EventByrdonisNest
            or RunConstants.EventAromaOfChaos
            or RunConstants.EventSimpleReward => true,
            // Hive and Glory events. The emulator models a run's FIRST act, so none of
            // these is in a pool it draws from. Refused rather than allowed, so adding a
            // later act's pool surfaces them instead of silently letting them through.
            RunConstants.EventColorfulPhilosophers
            or RunConstants.EventColossalFlower
            or RunConstants.EventGraveOfTheForgotten
            or RunConstants.EventRoundTeaParty => false,
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
    /// <summary>
    /// Whether <see cref="BeginDeckSelection"/> would open rather than refuse. The action
    /// mask has to ask the same question the step will, or the two disagree about an
    /// option and the agent is offered a move it cannot make -- which is the single most
    /// common defect this layer has had.
    /// </summary>
    public static bool CanBeginDeckSelection(RunState state, DeckSelection kind, int arg)
    {
        var savedKind = state.PendingSelectionKind;
        int savedArg = state.PendingSelectionArg;
        state.PendingSelectionKind = kind;
        state.PendingSelectionArg = arg;
        bool any = Enumerable.Range(0, state.Deck.Count).Any(i => CanSelectCard(state, i));
        state.PendingSelectionKind = savedKind;
        state.PendingSelectionArg = savedArg;
        return any;
    }

    public static bool BeginDeckSelection(
        RunState state,
        DeckSelection kind,
        int arg,
        int count = 1,
        int followUpCard = 0,
        int followUpCount = 0,
        int followUpHpLoss = 0,
        string? eventEntry = null,
        int enchantAmount = 0,
        SelectionReturn returnTo = SelectionReturn.EventResult
    )
    {
        state.PendingSelectionEventEntry = eventEntry;
        state.PendingSelectionKind = kind;
        state.PendingSelectionArg = arg;
        state.PendingSelectionCount = count;
        state.PendingSelectionFollowUpCard = followUpCard;
        state.PendingSelectionFollowUpCount = followUpCount;
        state.PendingSelectionFollowUpHpLoss = followUpHpLoss;
        state.PendingSelectionEnchantAmount = enchantAmount;
        state.PendingSelectionReturn = returnTo;
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
        state.PendingSelectionEnchantAmount = 0;
        state.PendingSelectionReturn = SelectionReturn.EventResult;
        state.PendingSelectionReturnsToEvent = false;
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
            // Dolly's Mirror excludes only Quest cards; a curse is a legal copy.
            DeckSelection.Duplicate => GeneratedData.Cards.Get(card.DefId).Type
                != CardType.Quest,
            // `FromDeckForRemoval` filters on `c.IsRemovable && filter(c)`, so the Eternal
            // check applies to this one too.
            DeckSelection.RemoveUpgradable => RunConstants.IsRunCardUpgradable(card)
                && !GeneratedData.Cards.Get(card.DefId).Eternal,
            // `CardSelectCmd.FromDeckForRemoval` filters on `c.IsRemovable`, which is
            // `!Keywords.Contains(CardKeyword.Eternal)` -- so the game will not so much as
            // OFFER an Eternal card for removal. Seven curses carry it, Ascender's Bane
            // among them, and the emulator let a run delete every one.
            DeckSelection.Remove => !GeneratedData.Cards.Get(card.DefId).Eternal,
            DeckSelection.TransformToRandom => true,
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
                    EnchantAmount =
                        state.PendingSelectionEnchantAmount > 0
                            ? state.PendingSelectionEnchantAmount
                            : SelfHelpBookAmount((Enchantment)state.PendingSelectionArg),
                };
                break;
            case DeckSelection.Duplicate:
                AddCardToDeck(state, card);
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
                // Every event hands CardCmd.TransformToRandom its OWN base.Rng; only
                // NewLeaf, which does not come through here, uses Rng.Niche. Rolling the
                // new card off Niche gave the event a card from a stream the game never
                // touched for it.
                TransformCardAt(
                    state,
                    deckIndex,
                    state.PendingSelectionEventEntry is null
                        ? state.Rng.Niche
                        : EventRng(state, state.PendingSelectionEventEntry)
                );
                break;
            case DeckSelection.Remove:
            case DeckSelection.RemoveUpgradable:
                state.Deck.RemoveAt(deckIndex);
                break;
            default:
                return false;
        }

        state.PendingSelectionCount--;
        return true;
    }

    /// <summary>
    /// What amount an enchantment lands at when nothing overrides it.
    /// </summary>
    /// <remarks>
    /// Self-Help Book applies Sharp, Nimble and Swift at 2 (its Enchantment*Amount vars);
    /// every event enchantment is <c>CardCmd.Enchant&lt;T&gt;(card, 1m)</c>. Pael's Growth
    /// is the exception that made this an override rather than a rule — it applies Clone
    /// at FOUR — so a caller that knows its own amount passes it.
    /// </remarks>
    private static int SelfHelpBookAmount(Enchantment enchantment) =>
        enchantment is Enchantment.Sharp or Enchantment.Nimble or Enchantment.Swift ? 2 : 1;

    /// <summary>
    /// What Luminous Choir asks for its tribute. The event starts from a GoldVar of 149
    /// and, on generate, takes off <c>Rng.NextInt(0, 50)</c> from its own stream -- so
    /// the price is somewhere in 100..149 and the option is locked below it.
    /// </summary>
    public static int LuminousChoirTributeCost(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateLuminousChoirVars(state);
        }

        return state.EventValue0!.Value;
    }

    /// <summary>
    /// <c>LuminousChoir.CalculateVars</c> takes GoldVar(149) and subtracts
    /// <c>Rng.NextInt(0, 50)</c> -- once, as the event is generated. Reading it re-rolled
    /// it, which was invisible only because every draw came off a fresh stream.
    /// </summary>
    private static void CalculateLuminousChoirVars(RunState state) =>
        state.EventValue0 = 149 - EventRng(state, "LUMINOUS_CHOIR").NextInt(0, 50);

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
            case RunConstants.EventLuminousChoir:
                CalculateLuminousChoirVars(state);
                break;
            case RunConstants.EventCrystalSphere:
                // The cost is CalculateVars' only draw, and the board comes off the same
                // stream straight after -- so it has to be spent on entry, not on the
                // first read. Reading the mask would otherwise roll it.
                EnsureCrystalSphereVars(state);
                break;
            case RunConstants.EventSlipperyBridge:
                RollSlipperyBridgeCard(state);
                break;
            case RunConstants.EventRanwidTheElder:
                CalculateRanwidVars(state);
                break;
            case RunConstants.EventWelcomeToWongos:
                // The featured item is pulled when the options are generated, so it comes
                // off the bag on entry whether or not the player buys it.
                WongosFeaturedItem(state);
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

    /// <summary>
    /// The three relics the trader is willing to take, in the order it offers them:
    /// <c>player.Relics.Where(IsTradable).StableShuffle(Rng).Take(3)</c>. StableShuffle
    /// sorts by ModelId -- Category then Entry, as ordinal strings -- before Fisher-Yates,
    /// which is why RelicDef carries an Entry now: our own numeric ids sort differently,
    /// and a different pre-shuffle order is a different shuffle from the same stream.
    ///
    /// Returns indices into <c>state.Relics</c>, because the trade removes the relic the
    /// player owns rather than a copy.
    /// </summary>
    public static List<int> RelicTraderStock(RunState state)
    {
        state.EventRelicStock ??= RollRelicTraderStock(state);
        return state.EventRelicStock;
    }

    /// <summary>
    /// <c>RelicTrader._ownedRelics</c> is built once and kept: the shelf does not reshuffle
    /// itself every time the player looks at it.
    /// </summary>
    private static List<int> RollRelicTraderStock(RunState state)
    {
        var tradable = Enumerable
            .Range(0, state.Relics.Count)
            .Where(i => IsTradableRelic(state, state.Relics[i]))
            .OrderBy(
                i => GeneratedData.Relics.Get(state.Relics[i].DefId).Entry,
                StringComparer.Ordinal
            )
            .ToList();
        EventRng(state, "RELIC_TRADER").Shuffle(tradable);
        return tradable.Take(3).ToList();
    }

    /// <summary>
    /// The relic Ranwid asks for: <c>Rng.NextItem(Relics.Where(IsTradable))</c> off the
    /// event's own stream. Returns an index into <c>state.Relics</c>, or -1 when the run
    /// holds nothing the elder will take.
    /// </summary>
    public static int RanwidTradeIndex(RunState state)
    {
        if (state.EventValue0 is null)
        {
            CalculateRanwidVars(state);
        }

        return state.EventValue0!.Value;
    }

    /// <summary>
    /// <c>RanwidTheElder.GenerateInitialOptions</c> draws twice: a potion, then a relic.
    /// </summary>
    /// <remarks>
    /// The potion draw was never spent here, so the relic was read one position early
    /// whenever the run held a potion. <c>Rng.NextItem</c> returns default and consumes
    /// NOTHING on an empty sequence, and <c>Player.Potions</c> filters empty slots out --
    /// so a run carrying no potions really does draw the relic first.
    /// </remarks>
    private static void CalculateRanwidVars(RunState state)
    {
        var rng = EventRng(state, "RANWID_THE_ELDER");
        int potions = state.PotionSlots.Count(slot => slot != 0);
        if (potions > 0)
        {
            rng.NextInt(0, potions);
        }

        var tradable = Enumerable
            .Range(0, state.Relics.Count)
            .Where(i => IsTradableRelic(state, state.Relics[i]))
            .ToList();
        state.EventValue0 = tradable.Count == 0 ? -1 : rng.NextItem(tradable);
    }

    /// <summary>
    /// <c>RelicModel.IsTradable</c>: Starter, Event and Ancient relics are never traded,
    /// nor is one whose pickup already paid out, nor one with a pet attached.
    /// </summary>
    public static bool IsTradableRelic(RunState state, RelicInstance relic) =>
        GeneratedData.Relics.Get(relic.DefId).IsTradable
        && !state.UsedUpRelics.Contains(relic.DefId);

    /// <summary>
    /// A curse rolled the way anything that hands one out rolls it: the curse pool,
    /// filtered to cards that may be generated -- eight of the eighteen refuse -- ordered
    /// by ModelId, and picked off the Niche stream.
    ///
    /// Ascender's Bane stood here for every such roll, which is not merely the wrong curse:
    /// it is one of the eight the game will never generate.
    /// </summary>
    public static int RollGeneratableCurse(RunState state)
    {
        var curses = GeneratedData
            .CardPools.Curse.ToArray()
            .Where(id => GeneratedData.Cards.Get(id).CanBeGeneratedByModifiers)
            .OrderBy(id => GeneratedData.Cards.Get(id).Entry, StringComparer.Ordinal)
            .ToArray();
        return curses.Length == 0
            ? RunConstants.CursePlaceholderCard
            : state.Rng.Niche.NextItem(curses);
    }

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
    /// What one immersion in the Abyssal Baths costs. <c>OnImmerse</c> gains MaxHpVar(2),
    /// takes DamageVar and then raises that damage by one, so the sequence is 3, 4, 5 and
    /// on -- and the Max HP arrives first, carrying current HP up with it, which is why
    /// the first dip is a net loss of only one.
    /// </summary>
    public static void Immerse(RunState state)
    {
        GainMaxHp(state, 2);
        state.PlayerHp = Math.Max(0, state.PlayerHp - AbyssalBathsDamage(state));
        state.EventPage++;
    }

    /// <summary>
    /// What the next Decipher costs at the Tablet of Truth. The tablet is not one choice
    /// but five, and the price doubles each time before the last one asks for everything:
    /// 3, 6, 12, 24, then MaxHp - 1. <c>GetDecipherCost</c> reads the count AFTER it moves,
    /// which is why the first is the DynamicVar's own 3 rather than anything the switch
    /// returns.
    /// </summary>
    public static int TabletOfTruthCost(RunState state) =>
        state.EventPage switch
        {
            0 => 3,
            1 => 6,
            2 => 12,
            3 => 24,
            _ => state.PlayerMaxHp - 1,
        };

    /// <summary>
    /// One Decipher: pay the Max HP and upgrade. Every stage but the last upgrades ONE
    /// card rolled off the event's stream; the fifth upgrades every upgradable card in the
    /// deck, which is the whole reason to have kept paying.
    ///
    /// <c>LoseMaxHpAndUpgrade</c> also kills outright when the price is not less than Max
    /// HP -- it takes MaxHp - 1 and then calls Kill -- so a tablet the run cannot afford
    /// ends it.
    /// </summary>
    public static void Decipher(RunState state)
    {
        int cost = TabletOfTruthCost(state);
        bool lethal = cost >= state.PlayerMaxHp;
        LoseMaxHp(state, lethal ? state.PlayerMaxHp - 1 : cost);
        if (lethal)
        {
            state.PlayerHp = 0;
            return;
        }

        if (state.EventPage == 4)
        {
            for (int i = 0; i < state.Deck.Count; i++)
            {
                if (RunConstants.IsRunCardUpgradable(state.Deck[i]))
                {
                    state.Deck[i] = state.Deck[i] with { Upgraded = true };
                }
            }
        }
        else
        {
            UpgradeRandomCard(state, "TABLET_OF_TRUTH");
        }

        state.EventPage++;
    }

    /// <summary>The tablet has five secrets and no more.</summary>
    public static bool TabletHasMoreToSay(RunState state) => state.EventPage < 5;

    /// <summary>The damage the NEXT immersion will do: DamageVar(3), plus one per dip.</summary>
    public static int AbyssalBathsDamage(RunState state) => 3 + state.EventPage;

    /// <summary>
    /// What the next Hold On costs: <c>CurrentHpLoss => 3 + NumberOfHoldOns</c>, read
    /// before the counter moves.
    /// </summary>
    public static int SlipperyBridgeHpLoss(RunState state) => 3 + state.EventPage;

    /// <summary>
    /// The card the Slippery Bridge is holding over the player. It prefers a non-Basic
    /// card and falls back to any removable one, which on a starter deck is the whole
    /// deck bar Ascender's Bane -- so it is a roll, not a fixed pick.
    /// </summary>
    public static int SlipperyBridgeCardIndex(RunState state)
    {
        if (state.EventValue0 is null)
        {
            RollSlipperyBridgeCard(state);
        }

        return state.EventValue0!.Value;
    }

    /// <summary>
    /// <c>SlipperyBridge.GetNewRandomCard</c>: called once from GenerateInitialOptions and
    /// again from every Hold On, storing the result in <c>RandomCardToLose</c>. Recomputing
    /// it per read drew a new card each time the mask or a test looked at it.
    /// </summary>
    public static void RollSlipperyBridgeCard(RunState state) =>
        state.EventValue0 = PickSlipperyBridgeCard(state);

    private static int PickSlipperyBridgeCard(RunState state)
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

        if (candidates.Count == 0)
        {
            return -1;
        }

        // GetNewRandomCard runs again on every Hold On, so the threatened card moves with
        // the counter rather than staying whatever the first roll picked.
        var rng = EventRng(state, "SLIPPERY_BRIDGE");
        int index = rng.NextItem(candidates);
        for (int i = 0; i < state.EventPage; i++)
        {
            index = rng.NextItem(candidates);
        }

        return index;
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

    /// <summary>
    /// <c>FishingRod.AfterCombatEnd</c>: every third MONSTER room upgrades a card.
    /// </summary>
    /// <remarks>
    /// Elites, bosses and the fights an event starts are all skipped -- the relic returns
    /// early on anything that is not <c>RoomType.Monster</c>, so they do not even advance
    /// the counter. The card is a plain <c>Rng.Niche.NextItem</c> over the upgradable
    /// ones, and the counter lives on the relic so it survives a save.
    /// </remarks>
    public static void TriggerFishingRod(RunState state)
    {
        if (state.LastResolvedRoomType != RunConstants.NodeNormal)
        {
            return;
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == RunConstants.RelicFishingRod);
        if (index < 0)
        {
            return;
        }

        int seen = state.Relics[index].Counter + 1;
        state.Relics[index] = state.Relics[index] with { Counter = seen };
        if (seen % RunConstants.FishingRodCombats != 0)
        {
            return;
        }

        var upgradable = Enumerable
            .Range(0, state.Deck.Count)
            .Where(i => RunConstants.IsRunCardUpgradable(state.Deck[i]))
            .ToList();
        if (upgradable.Count == 0)
        {
            return;
        }

        int pick = state.Rng.Niche.NextItem(upgradable);
        state.Deck[pick] = state.Deck[pick] with { Upgraded = true };
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

    /// <summary>
    /// The dolls the second page puts in front of the player: the three shuffled, then
    /// two of them for Take Some Time and all three for Examine. StableShuffle sorts by
    /// ModelId -- Category then Entry as ordinal strings -- before Fisher-Yates, which is
    /// what RelicDef.Entry is for.
    /// </summary>
    public static List<int> DollRoomOffer(RunState state, int count)
    {
        var dolls = DollRoomDolls
            .Select(ResolveRelic)
            .OrderBy(id => GeneratedData.Relics.Get(id).Entry, StringComparer.Ordinal)
            .ToList();
        EventRng(state, "DOLL_ROOM").Shuffle(dolls);
        return dolls.Take(count).ToList();
    }

    /// <summary>
    /// The Rare relic Wongo has on display. It is pulled in GenerateInitialOptions -- when
    /// the event OPENS, not when it is bought -- so the option can name it, and it leaves
    /// the bag either way. Cached in EventValue1 so reading it twice does not pull twice.
    /// </summary>
    public static int WongosFeaturedItem(RunState state)
    {
        state.EventValue1 ??= RunRewardGenerator.NextShopRelicOfRarity(state, RelicRarity.Rare);
        return state.EventValue1.Value;
    }

    /// <summary>The Lantern Key card, which War Historian Repy spends to open anything.</summary>
    public static int LanternKeyCard => ResolveCard("LanternKey");

    /// <summary>
    /// Repy gives a SECOND reward when the run still holds a Lantern Key after the first
    /// is spent -- <c>ShouldGetSecondReward</c> -- which for a solo run means it arrived
    /// holding two. The first choice spends one key; the page that follows offers only
    /// the door the player did not take.
    /// </summary>
    public static bool RepyOwesASecondReward(RunState state) =>
        state.Deck.Any(card => card.DefId == LanternKeyCard);

    /// <summary>Spends one Lantern Key, if the run has one to spend.</summary>
    public static void SpendLanternKey(RunState state)
    {
        int index = state.Deck.FindIndex(card => card.DefId == LanternKeyCard);
        if (index >= 0)
        {
            state.Deck.RemoveAt(index);
        }
    }

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

        // The event's stream is persistent now, so it is already where this roll belongs.
        // This used to fast-forward a freshly built stream to grabs-minus-the-fifths --
        // the fifth grab returns above without drawing -- which became a REWIND as soon
        // as anything else drew from the event, and threw.
        var rng = EventRng(state, "ENDLESS_CONVEYOR");

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
    /// <summary>An event's own stream, for callers outside this class.</summary>
    public static GameRng EventStream(RunState state, string eventEntry) =>
        EventRng(state, eventEntry);

    /// <summary>
    /// The event's own Rng stream -- one per event, not one per draw.
    /// </summary>
    /// <remarks>
    /// An event holds a single <c>base.Rng</c>, so its draws are a sequence: the Endless
    /// Conveyor rolls its dish in CalculateVars and then picks the card Observe the Chef
    /// upgrades, and that pick reads the SECOND value. Building a fresh stream per call
    /// handed it the first, which upgraded the wrong card against a live capture. Keyed
    /// by entry so changing events starts a new stream rather than inheriting one.
    /// </remarks>
    private static GameRng EventRng(RunState state, string eventEntry)
    {
        if (state.EventRngStream is not null && state.EventRngName == eventEntry)
        {
            return state.EventRngStream;
        }

        uint eventSeed = unchecked(
            state.Rng.Seed + (uint)DeterministicHash.GetDeterministicHashCode(eventEntry)
        );
        state.EventRngName = eventEntry;
        state.EventRngStream = new GameRng(eventSeed);
        return state.EventRngStream;
    }

    /// <summary>
    /// An ancient's three blessings: one drawn from each of its pools, off the ancient's
    /// own Rng, in pool order.
    /// </summary>
    /// <remarks>
    /// All three of Hive's ancients share this shape and differ only in their pools and
    /// in which entries are CONDITIONAL on the run. Neow does not come through here — it
    /// has its own generator, and a different shape (a curse plus a shuffled positive
    /// list) rather than three pools.
    /// </remarks>
    public static int[] GenerateAncientOptions(RunState state, string ancient)
    {
        var rng = EventStream(state, ancient);
        return ancient switch
        {
            RunConstants.AncientOrobas => OrobasOptions(state, rng),
            RunConstants.AncientPael => PaelOptions(state, rng),
            RunConstants.AncientTezcatara => TezcataraOptions(state, rng),
            _ => [],
        };
    }

    /// <summary>
    /// Orobas spends TWO draws before its pools: one picking a character other than the
    /// player's, to brand a Sea Glass with, and one deciding whether pool 1 gets the Sea
    /// Glass or a Prismatic Gem instead — <c>NextFloat() &lt; 1/3</c> for the gem.
    /// </summary>
    /// <remarks>
    /// The character draw's RESULT does not matter here (it only brands the relic, which
    /// the emulator does not model), but the draw does: skipping it would shift every pool
    /// pick after it. The list it chooses from is the unlocked characters minus the
    /// player's own, which is four on a mature profile.
    /// </remarks>
    private static int[] OrobasOptions(RunState state, GameRng rng)
    {
        // The character is kept, not just spent: if the Sea Glass is offered AND taken,
        // its cards come from that character's pool.
        state.SeaGlassCharacter = rng.NextInt(RunConstants.OtherCharacterCount);
        bool prismaticGem = rng.NextDouble() < 1.0 / 3.0;

        var pool1 = RunConstants.OrobasPool1.ToArray().ToList();
        pool1.Add(prismaticGem ? RunConstants.RelicPrismaticGemOption : RunConstants.RelicSeaGlass);

        // Pool 3 holds Touch of Orobas and Archaic Tooth, each present only if it can be
        // set up for the player -- one needs a starter relic, the other a transcendable
        // starter card, and the Ironclad has both.
        return
        [
            rng.NextItem(pool1),
            rng.NextItem(RunConstants.OrobasPool2.ToArray()),
            rng.NextItem(RunConstants.OrobasPool3.ToArray()),
        ];
    }

    /// <summary>
    /// Pael's second pool is a weighting trick: the conditional entries are added, then
    /// <c>list.AddRange(list)</c> DOUBLES everything so far, and only then is Growth
    /// appended — so Growth is half as likely as anything else in the pool.
    /// </summary>
    private static int[] PaelOptions(RunState state, GameRng rng)
    {
        var pool2 = RunConstants.PaelPool2.ToArray().ToList();
        if (state.Deck.Count(CanTakeAnEnchantment) >= 3)
        {
            pool2.Add(RunConstants.RelicPaelsClaw);
        }

        if (state.Deck.Count(IsRemovableCard) >= 5)
        {
            pool2.Add(RunConstants.RelicPaelsTooth);
        }

        pool2.AddRange(pool2);
        pool2.Add(RunConstants.RelicPaelsGrowth);

        var pool3 = RunConstants.PaelPool3.ToArray().ToList();
        // HasEventPet is not modelled, and no run has one yet.
        pool3.Add(RunConstants.RelicPaelsLegion);

        return
        [
            rng.NextItem(RunConstants.PaelPool1.ToArray()),
            rng.NextItem(pool2),
            rng.NextItem(pool3),
        ];
    }

    /// <summary>
    /// Tezcatara adds Nutritious Soup to its first pool when the deck holds a BASIC
    /// Strike — which every starting deck does, and keeps doing unless every Strike is
    /// removed or transformed.
    /// </summary>
    private static int[] TezcataraOptions(RunState state, GameRng rng)
    {
        var pool1 = RunConstants.TezcataraPool1.ToArray().ToList();
        if (state.Deck.Any(IsBasicStrike))
        {
            pool1.Add(RunConstants.RelicNutritiousSoup);
        }

        return
        [
            rng.NextItem(pool1),
            rng.NextItem(RunConstants.TezcataraPool2.ToArray()),
            rng.NextItem(RunConstants.TezcataraPool3.ToArray()),
        ];
    }

    /// <summary>
    /// <c>c.Tags.Contains(CardTag.Strike) &amp;&amp; c.Rarity == Basic</c>. Card tags are
    /// not extracted, so this reads the entry name instead — every Strike-tagged card the
    /// game has is named for it, and at Basic rarity there is only the starter Strike.
    /// </summary>
    private static bool IsBasicStrike(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(Math.Abs(card.DefId));
        return def.Rarity == CardRarity.Basic
            && def.Entry.Contains("STRIKE", StringComparison.Ordinal);
    }

    /// <summary>
    /// Stands in for <c>Goopy.CanEnchant</c>, which is not modelled — the emulator has no
    /// Goopy enchantment at all.
    /// </summary>
    /// <remarks>
    /// Only ever used as a COUNT against a threshold of three, and every deck a run can
    /// hold clears that on its starting cards alone, so the approximation has no reachable
    /// effect on which option Pael offers. It is still an approximation: if Goopy turns
    /// out to refuse a card type this counts, a deck stripped down to two enchantable
    /// cards would disagree.
    /// </remarks>
    private static bool CanTakeAnEnchantment(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(Math.Abs(card.DefId));
        return def.Type is not (CardType.Curse or CardType.Status);
    }

    /// <summary>
    /// Stands in for <c>CardModel.IsRemovable</c>, which is not extracted.
    /// </summary>
    /// <remarks>
    /// Ascender's Bane is the one card every run carries that the game refuses to remove;
    /// Eternal cards are the other case and are not modelled. Same caveat as above: the
    /// threshold is five and a starting deck has ten removable cards.
    /// </remarks>
    private static bool IsRemovableCard(CardInstance card) =>
        Math.Abs(card.DefId) != RunConstants.CardAscendersBane;

    /// <summary>
    /// Enchant every card in the deck that will take it — the shape Pael's Claw and
    /// Nutritious Soup share, where the relic offers no choice at all.
    /// </summary>
    private static void EnchantEveryCard(
        RunState state,
        Enchantment enchantment,
        int amount,
        Func<CardInstance, bool>? extraFilter = null
    )
    {
        for (int i = 0; i < state.Deck.Count; i++)
        {
            var card = state.Deck[i];
            if (!Enchantments.CanEnchant(card, enchantment))
            {
                continue;
            }

            if (extraFilter is not null && !extraFilter(card))
            {
                continue;
            }

            state.Deck[i] = card with { Enchantment = enchantment, EnchantAmount = amount };
        }
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

    /// <summary>
    /// Doors of Light and Dark's Light door:
    /// <c>Deck.Cards.Where(IsUpgradable).StableShuffle(base.Rng).Take(Cards)</c>.
    /// </summary>
    /// <remarks>
    /// Two things here are the whole of it, and the emulator had both wrong. The stream is
    /// the EVENT's own <c>base.Rng</c>, not <c>Rng.Niche</c> -- the same mistake as E14,
    /// where every event's transform was rolled off Niche. And <c>StableShuffle</c> sorts
    /// by ModelId before it shuffles, which compares the slugified class name as an
    /// ordinal string; sorting by the emulator's own numeric ids puts a different card
    /// under the same draw. A live capture upgraded Whirlwind where this took Shrug It Off.
    /// </remarks>
    /// <summary>
    /// <c>Deck.Where(IsUpgradable).StableShuffle(rng).Take(count)</c>, upgraded.
    /// </summary>
    /// <remarks>
    /// The two things that matter are the STREAM, which differs per caller, and the sort:
    /// <c>StableShuffle</c> orders by ModelId — the slugified class name, compared
    /// ordinally — before it shuffles, so sorting by the emulator's own numeric ids puts a
    /// different card under the same draw. Both were wrong in the Light Door once (E40).
    /// </remarks>
    public static void UpgradeRandomCards(RunState state, int count, GameRng rng)
    {
        var indexes = state
            .Deck.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .OrderBy(
                index => GeneratedData.Cards.Get(state.Deck[index].DefId).Entry,
                StringComparer.Ordinal
            )
            .ToList();
        rng.Shuffle(indexes);
        foreach (int index in indexes.Take(count))
        {
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
        }
    }

    /// <summary>
    /// Reflections' Touch a Mirror: downgrade up to two upgraded cards, then upgrade up
    /// to four upgradable ones.
    /// </summary>
    /// <remarks>
    /// The upgradable list is derived AFTER the downgrades, not alongside them, so the
    /// two cards the mirror just knocked down are candidates to come straight back up.
    /// Each pick is <c>Rng.NextItem</c> over the remaining list with the pick removed --
    /// a draw per card, and only while the list has something in it, which is why both
    /// loops break rather than clamp.
    /// </remarks>
    public static void ReflectionsTouchAMirror(RunState state)
    {
        var rng = EventRng(state, "REFLECTIONS");

        var upgraded = Enumerable
            .Range(0, state.Deck.Count)
            .Where(index => state.Deck[index].Upgraded)
            .ToList();
        for (int i = 0; i < 2 && upgraded.Count > 0; i++)
        {
            int index = rng.NextItem(upgraded);
            upgraded.Remove(index);
            state.Deck[index] = state.Deck[index] with { Upgraded = false };
        }

        var upgradable = Enumerable
            .Range(0, state.Deck.Count)
            .Where(index => RunConstants.IsRunCardUpgradable(state.Deck[index]))
            .ToList();
        for (int i = 0; i < 4 && upgradable.Count > 0; i++)
        {
            int index = rng.NextItem(upgradable);
            upgradable.Remove(index);
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
        }
    }

    /// <summary>
    /// Reflections' Shatter: a copy of every card in the deck, then a Bad Luck.
    /// </summary>
    /// <remarks>
    /// The loop runs to the deck's size as it was BEFORE any copy was added, which is
    /// what stops it copying its own copies forever. CloneCard preserves the card as it
    /// stands -- upgrades and enchantment included -- and costs no draws.
    /// </remarks>
    public static void ReflectionsShatter(RunState state)
    {
        int originalDeckSize = state.Deck.Count;
        for (int i = 0; i < originalDeckSize; i++)
        {
            // Not AddCardToDeck: this is CloneCard, so the egg relics do not get a second
            // look at a card that is already in the deck.
            state.Deck.Add(state.Deck[i]);
        }

        AddCardToDeck(state, new CardInstance(RunConstants.CardBadLuck, Upgraded: false));
    }

    /// <summary>
    /// Tinker Time's second page: two of Attack, Skill and Power.
    /// </summary>
    /// <remarks>
    /// <c>TakeRandom(2, Rng)</c> is <c>UnstableShuffle(rng).Take(2)</c>, so all three are
    /// permuted -- two draws for a list of three -- and the first two are shown. Taking
    /// only the draws the two shown cards need would leave the rider page reading the
    /// stream one value early.
    /// </remarks>
    public static void BeginTinkerCardTypePage(RunState state)
    {
        var types = new List<int>
        {
            (int)CardType.Attack,
            (int)CardType.Skill,
            (int)CardType.Power,
        };
        EventRng(state, "TINKER_TIME").Shuffle(types);
        state.EventRandomOffer = [.. types.Take(2)];
        state.EventPage = 1;
    }

    /// <summary>
    /// Tinker Time's third page: two of the three riders that belong to the chosen type.
    /// </summary>
    public static void BeginTinkerRiderPage(RunState state)
    {
        var riders = state.TinkerCardType switch
        {
            CardType.Attack => new List<int>
            {
                (int)TinkerRider.Sapping,
                (int)TinkerRider.Violence,
                (int)TinkerRider.Choking,
            },
            CardType.Skill => new List<int>
            {
                (int)TinkerRider.Energized,
                (int)TinkerRider.Wisdom,
                (int)TinkerRider.Chaos,
            },
            _ => new List<int>
            {
                (int)TinkerRider.Expertise,
                (int)TinkerRider.Curious,
                (int)TinkerRider.Improvement,
            },
        };
        // The same stream the type page used: one event, one Rng.
        EventRng(state, "TINKER_TIME").Shuffle(riders);
        state.EventRandomOffer = [.. riders.Take(2)];
        state.EventPage = 2;
    }

    public static void UpgradeTwoRandomCardsForLightDoor(RunState state)
    {
        var indexes = state
            .Deck.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .OrderBy(
                index => GeneratedData.Cards.Get(state.Deck[index].DefId).Entry,
                StringComparer.Ordinal
            )
            .ToList();
        EventRng(state, "DOORS_OF_LIGHT_AND_DARK").Shuffle(indexes);
        foreach (int index in indexes.Take(2))
        {
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
        }
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
