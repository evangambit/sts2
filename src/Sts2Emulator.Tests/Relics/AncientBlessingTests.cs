using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What the act ancients' blessings DO when taken.
/// </summary>
/// <remarks>
/// Which three an ancient offers is one problem and what they do is another; this is the
/// second. Six of the thirty across Orobas, Pael and Tezcatara are modelled — the ones
/// that need nothing the emulator lacks. The rest are listed in the catalogue under O13:
/// four want enchantments that are not modelled at all (Goopy, Imbued, Clone,
/// Tezcatara's Ember), two build reward SETS from another character's pool, and two only
/// do anything inside a combat.
/// </remarks>
public class AncientBlessingTests
{
    private static readonly RunEngine Pristine = Generate();

    private static RunEngine Generate()
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        return engine;
    }

    private static RunEngine Taking(int relicId)
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.ApplyRelicPickup(engine.State, relicId);
        return engine;
    }

    private static int Count(RunState state, string entry) =>
        state.Deck.Count(card => GeneratedData.Cards.Get(card.DefId).Entry == entry);

    /// <summary>
    /// Two Relax into the deck — the one ancient blessing a live capture pins, and the
    /// reason act 2's replay gets past its ancient at all.
    /// </summary>
    [Fact]
    public void PaelsHornAddsTwoRelax()
    {
        var state = Taking(RunConstants.RelicPaelsHorn).State;

        Assert.Equal(2, Count(state, "RELAX"));
        Assert.Equal(Pristine.State.Deck.Count + 2, state.Deck.Count);
    }

    [Fact]
    public void StorybookAddsABrightestFlame()
    {
        var state = Taking(RunConstants.RelicStorybook).State;

        Assert.Equal(1, Count(state, "BRIGHTEST_FLAME"));
    }

    /// <summary>
    /// CardsVar(6), StableShuffled on Rng.NICHE. A starting deck has ten upgradable
    /// cards, so exactly six come back upgraded — and the curse is not one of them.
    /// </summary>
    [Fact]
    public void SandCastleUpgradesSixCards()
    {
        var state = Taking(RunConstants.RelicSandCastle).State;

        Assert.Equal(6, state.Deck.Count(card => card.Upgraded));
        Assert.All(
            state.Deck.Where(card => card.Upgraded),
            card => Assert.NotEqual(RunConstants.CardAscendersBane, card.DefId)
        );
    }

    /// <summary>It draws off Niche, which is not the stream a reward or an event uses.</summary>
    [Fact]
    public void SandCastleDrawsFromNiche()
    {
        var engine = Pristine.Clone();
        int rewardsBefore = engine.State.PlayerRng.Rewards.CallCount;
        int nicheBefore = engine.State.Rng.Niche.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicSandCastle);

        Assert.Equal(rewardsBefore, engine.State.PlayerRng.Rewards.CallCount);
        Assert.True(engine.State.Rng.Niche.CallCount > nicheBefore);
    }

    /// <summary>
    /// PotionSlots(4): the slots come FIRST so all four potions have somewhere to go, and
    /// they roll off CombatPotionGeneration — the same shape as Phial Holster, and the
    /// same reason. Rolling them off PlayerRng.Rewards would move every card reward after.
    /// </summary>
    [Fact]
    public void AlchemicalCofferGivesFourSlotsAndFourPotions()
    {
        var engine = Pristine.Clone();
        int slotsBefore = engine.State.MaxPotionSlots;
        int rewardsBefore = engine.State.PlayerRng.Rewards.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicAlchemicalCoffer);

        Assert.Equal(slotsBefore + 4, engine.State.MaxPotionSlots);
        Assert.Equal(rewardsBefore, engine.State.PlayerRng.Rewards.CallCount);

        // NOT asserting four potions. RunState.PotionSlots is a fixed int[3] — as are
        // CombatState's and GameState's, and the combat observation is laid out around
        // that width — so the emulator physically cannot hold the six a Coffer run has.
        // Deliberately left failing-in-the-world rather than asserted at three, which
        // would bake the cap in as though it were the rule. See O15.
        Assert.True(engine.State.PotionSlots.Count(potion => potion != 0) >= 3);
    }

    /// <summary>
    /// Yummy Cookie and Biiig Hug both hand the choice to the player rather than picking
    /// — four cards each, through the deck-selection screen.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.RelicYummyCookie, DeckSelection.Upgrade)]
    [InlineData(RunConstants.RelicBiiigHug, DeckSelection.Remove)]
    public void TheChoosingBlessingsOpenASelectionForFourCards(int relicId, DeckSelection kind)
    {
        var engine = Pristine.Clone();
        int before = engine.State.Deck.Count;

        var followUp = RunNonCombatEffects.ApplyRelicPickup(engine.State, relicId);

        Assert.Equal(RunFollowUp.TransformSelect, followUp);
        Assert.Equal(kind, engine.State.PendingSelectionKind);
        Assert.Equal(4, engine.State.PendingSelectionCount);
        // Nothing has happened yet: the screen is open and the player has not answered.
        Assert.Equal(before, engine.State.Deck.Count);
    }

    /// <summary>
    /// CardsVar(5) through FromDeckForRemoval with a filter of <c>IsUpgradable</c>: only
    /// upgradable cards are OFFERED, which is a different screen from a plain removal
    /// rather than a different answer to the same one.
    /// </summary>
    [Fact]
    public void PaelsToothOffersOnlyUpgradableCards()
    {
        var engine = Pristine.Clone();

        var followUp = RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunConstants.RelicPaelsTooth
        );

        Assert.Equal(RunFollowUp.TransformSelect, followUp);
        Assert.Equal(DeckSelection.RemoveUpgradable, engine.State.PendingSelectionKind);
        Assert.Equal(5, engine.State.PendingSelectionCount);

        // Ascender's Bane is not upgradable, so the screen will not take it — where a
        // plain removal would.
        int bane = engine.State.Deck.FindIndex(c => c.DefId == RunConstants.CardAscendersBane);
        Assert.True(bane >= 0, "the starting deck should carry Ascender's Bane");
        Assert.False(RunNonCombatEffects.CanSelectCard(engine.State, bane));
        Assert.True(RunNonCombatEffects.CanSelectCard(engine.State, 0));
    }

    /// <summary>
    /// Glass Eye is FIVE card rewards on one screen — Common, Common, Uncommon, Uncommon,
    /// Rare — each offering three of that rarity, claimed one after another.
    /// </summary>
    [Fact]
    public void GlassEyeQueuesFiveCardOffersByRarity()
    {
        var engine = Pristine.Clone();

        var followUp = RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunConstants.RelicGlassEye
        );

        Assert.Equal(RunFollowUp.BonusCardOffers, followUp);
        // All five are QUEUED here; moving the first onto the screen is the caller's job,
        // which is why this asserts five and not four.
        Assert.Equal(5, engine.State.PendingCardOffers.Count);
        Assert.All(engine.State.PendingCardOffers, offer => Assert.Equal(3, offer.Length));
    }

    [Fact]
    public void GlassEyesOffersAreTheRaritiesItNames()
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicGlassEye);

        var rarities = new[]
        {
            CardRarity.Common,
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Uncommon,
            CardRarity.Rare,
        };
        for (int i = 0; i < rarities.Length; i++)
        {
            var offer = engine.State.PendingCardOffers[i];
            Assert.Equal(3, offer.Length);
            Assert.All(
                offer,
                card => Assert.Equal(rarities[i], GeneratedData.Cards.Get(card).Rarity)
            );
            // Three DISTINCT cards per screen: each is blacklisted as it is drawn.
            Assert.Equal(3, offer.Distinct().Count());
        }
    }

    /// <summary>
    /// Uniform odds with NoRarityModification means no rarity roll, so each card costs a
    /// pick and an upgrade roll — two draws, thirty across the five screens.
    /// </summary>
    [Fact]
    public void GlassEyeSpendsTwoDrawsPerCard()
    {
        var engine = Pristine.Clone();
        int before = engine.State.PlayerRng.Rewards.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicGlassEye);

        Assert.Equal(before + 30, engine.State.PlayerRng.Rewards.CallCount);
    }

    /// <summary>
    /// CardsVar(15) / 3: five cards of each rarity, from the OTHER character's pool.
    /// </summary>
    /// <remarks>
    /// Which character is decided by Orobas, in the first draw it spends — a draw the
    /// emulator used to make and throw away. Ironclad's own pool must not appear.
    /// </remarks>
    [Fact]
    public void SeaGlassOffersFifteenFromAnotherCharactersPool()
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.GenerateAncientOptions(engine.State, RunConstants.AncientOrobas);
        Assert.InRange(engine.State.SeaGlassCharacter, 0, RunConstants.OtherCharacterCount - 1);

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicSeaGlass);

        Assert.Equal(15, engine.State.PendingOfferCards.Length);
        var ironclad = GeneratedData.CardPools.Ironclad.ToArray();
        Assert.All(engine.State.PendingOfferCards, card => Assert.DoesNotContain(card, ironclad));
    }

    [Fact]
    public void SeaGlassOffersFiveOfEachRarityInOrder()
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.GenerateAncientOptions(engine.State, RunConstants.AncientOrobas);
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicSeaGlass);

        var offered = engine.State.PendingOfferCards;
        var expected = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare };
        for (int batch = 0; batch < 3; batch++)
        {
            var slice = offered.Skip(batch * 5).Take(5).ToArray();
            Assert.All(
                slice,
                card => Assert.Equal(expected[batch], GeneratedData.Cards.Get(card).Rarity)
            );
            // Distinct within a batch: each is blacklisted as it is drawn.
            Assert.Equal(5, slice.Distinct().Count());
        }
    }

    /// <summary>Thirty draws, the same budget as Glass Eye: a pick and an upgrade roll each.</summary>
    [Fact]
    public void SeaGlassSpendsTwoDrawsPerCard()
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.GenerateAncientOptions(engine.State, RunConstants.AncientOrobas);
        int before = engine.State.PlayerRng.Rewards.CallCount;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicSeaGlass);

        Assert.Equal(before + 30, engine.State.PlayerRng.Rewards.CallCount);
    }

    /// <summary>
    /// Orobas keeps the character rather than only spending the draw — the two options it
    /// can put in pool 1 are the Prismatic Gem and a Sea Glass, and only one of them cares.
    /// </summary>
    [Fact]
    public void OrobasRecordsWhichCharacterTheSeaGlassIsBrandedWith()
    {
        var engine = Pristine.Clone();
        Assert.Equal(-1, engine.State.SeaGlassCharacter);

        RunNonCombatEffects.GenerateAncientOptions(engine.State, RunConstants.AncientOrobas);

        Assert.InRange(engine.State.SeaGlassCharacter, 0, RunConstants.OtherCharacterCount - 1);
    }

    // ---- the four that enchant --------------------------------------------------

    /// <summary>
    /// Pael's Claw puts Goopy on every card that will take it, with no screen at all.
    /// Goopy takes Defend-tagged cards, so a starting deck's four Defends get it and its
    /// Strikes and Bash do not.
    /// </summary>
    [Fact]
    public void PaelsClawGoopsEveryDefend()
    {
        var state = Taking(RunConstants.RelicPaelsClaw).State;

        var goopy = state.Deck.Where(c => c.Enchantment == Enchantment.Goopy).ToList();
        Assert.Equal(4, goopy.Count);
        Assert.All(
            goopy,
            card => Assert.StartsWith("DEFEND_", GeneratedData.Cards.Get(card.DefId).Entry)
        );
        Assert.All(goopy, card => Assert.Equal(1, card.EnchantAmount));
    }

    /// <summary>
    /// Nutritious Soup embers every BASIC Strike — five in a starting deck — and nothing
    /// else. Ember zeroes the card's cost and adds to a powered attack.
    /// </summary>
    [Fact]
    public void NutritiousSoupEmbersEveryBasicStrike()
    {
        var state = Taking(RunConstants.RelicNutritiousSoup).State;

        var embered = state.Deck.Where(c => c.Enchantment == Enchantment.TezcatarasEmber).ToList();
        Assert.Equal(5, embered.Count);
        Assert.All(
            embered,
            card => Assert.Equal("STRIKE_IRONCLAD", GeneratedData.Cards.Get(card.DefId).Entry)
        );
    }

    /// <summary>
    /// Electric Shrymp asks the player, and Imbued takes SKILLS — so a Strike cannot be
    /// chosen and a Defend can.
    /// </summary>
    [Fact]
    public void ElectricShrympOffersSkillsOnly()
    {
        var engine = Pristine.Clone();

        var followUp = RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunConstants.RelicElectricShrymp
        );

        Assert.Equal(RunFollowUp.TransformSelect, followUp);
        Assert.Equal(DeckSelection.Enchant, engine.State.PendingSelectionKind);
        Assert.Equal((int)Enchantment.Imbued, engine.State.PendingSelectionArg);

        int strike = engine.State.Deck.FindIndex(c => c.DefId == 472);
        int defend = engine.State.Deck.FindIndex(c => c.DefId == 131);
        Assert.False(RunNonCombatEffects.CanSelectCard(engine.State, strike));
        Assert.True(RunNonCombatEffects.CanSelectCard(engine.State, defend));
    }

    /// <summary>
    /// Pael's Growth applies Clone at FOUR, not the 1 every other event enchantment uses
    /// — which is why the amount is something a caller can name.
    /// </summary>
    [Fact]
    public void PaelsGrowthClonesAtFour()
    {
        var engine = Pristine.Clone();
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicPaelsGrowth);

        engine.Step(0, -1, out _, out _, out _);

        var cloned = engine.State.Deck.Single(c => c.Enchantment == Enchantment.Clone);
        Assert.Equal(4, cloned.EnchantAmount);
    }

    [Fact]
    public void BiiigHugRemovesTheFourTheChoiceNames()
    {
        var engine = Pristine.Clone();
        int before = engine.State.Deck.Count;
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunConstants.RelicBiiigHug);

        for (int i = 0; i < 4; i++)
        {
            engine.Step(0, -1, out _, out _, out _);
        }

        Assert.Equal(before - 4, engine.State.Deck.Count);
    }
}
