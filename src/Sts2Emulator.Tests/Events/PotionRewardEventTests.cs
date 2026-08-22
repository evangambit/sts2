using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The events that offer potions, driven past the reward screen.
///
/// A potion is OFFERED, not given: the game routes to a reward screen the player can
/// decline, or make room on by dropping something. The emulator used to push the potion
/// straight into the belt, so a full belt silently swallowed it and the player never got
/// the choice.
///
/// As with the card selections, the live fixtures stop at the screen -- they see that a
/// screen opened but not what is on it, so how MANY potions were queued and whether they
/// arrive one after another is invisible to <c>EventOutcomeTests</c>. Two regressions in
/// that code passed the whole fixture suite before this was written.
/// </summary>
public class PotionRewardEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int HeldPotions(RunState state) => state.PotionSlots.Count(slot => slot != 0);

    /// <summary>Claim whatever is on the reward screen until it is empty.</summary>
    private static void ClaimEverything(RunEngine engine)
    {
        for (int i = 0; i < 8 && engine.State.Phase == RunPhase.RelicReward; i++)
        {
            if (engine.Step(0, -1, out _, out _, out _) != 0)
            {
                break;
            }
        }
    }

    // ── Whispering Hollow ────────────────────────────────────────────────────

    [Fact]
    public void ExchangingGoldSpendsItAndOffersTwoPotions()
    {
        var engine = At(RunConstants.EventWhisperingHollow);
        int cost = RunNonCombatEffects.WhisperingHollowGold(engine.State);
        Assert.InRange(cost, 26, 44);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(99 - cost, engine.State.Gold);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.NotEqual(0, engine.State.RewardPotion);
        Assert.Single(engine.State.PendingPotionRewards);
        Assert.Equal(0, HeldPotions(engine.State));

        ClaimEverything(engine);
        Assert.Equal(2, HeldPotions(engine.State));
    }

    [Fact]
    public void ARunThatCannotAffordTheHollowKeepsItsGold()
    {
        var engine = At(RunConstants.EventWhisperingHollow);
        engine.State.Gold = 10;

        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(10, engine.State.Gold);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    /// <summary>
    /// Hug transforms the card the player picks and only THEN charges the 9 HP, which is
    /// why the live capture shows full health while the selector is open.
    /// </summary>
    [Fact]
    public void HuggingTheTreeChargesItsHpAfterTheTransform()
    {
        var engine = At(RunConstants.EventWhisperingHollow);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(64, engine.State.PlayerHp);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(55, engine.State.PlayerHp);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    // ── Potion Courier ───────────────────────────────────────────────────────

    [Fact]
    public void GrabbingPotionsOffersThreeFoulPotionsOneAtATime()
    {
        var engine = At(RunConstants.EventPotionCourier);
        int foul = RunNonCombatEffects.NamedPotion("FoulPotion");

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(foul, engine.State.RewardPotion);
        Assert.Equal([foul, foul], engine.State.PendingPotionRewards);

        // The belt holds two, so the third has nowhere to go -- and the player is not
        // forced to take any of them.
        ClaimEverything(engine);
        Assert.Equal(2, HeldPotions(engine.State));
        Assert.All(engine.State.PotionSlots.Where(slot => slot != 0), slot => Assert.Equal(foul, slot));
    }

    [Fact]
    public void RansackingOffersOneUncommonPotion()
    {
        var engine = At(RunConstants.EventPotionCourier);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.NotEqual(0, engine.State.RewardPotion);
        Assert.Empty(engine.State.PendingPotionRewards);
    }

    [Fact]
    public void SkippingTheScreenAbandonsEveryQueuedPotion()
    {
        var engine = At(RunConstants.EventPotionCourier);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(0, engine.Step(RunConstants.RewardSkipAction, -1, out _, out _, out _));

        Assert.Empty(engine.State.PendingPotionRewards);
        Assert.Equal(0, HeldPotions(engine.State));
    }

    // ── The Legends Were True / the Wellspring ───────────────────────────────

    [Fact]
    public void SlowlyFindingAnExitCostsEightHpAndOffersOnePotion()
    {
        var engine = At(RunConstants.EventTheLegendsWereTrue);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(56, engine.State.PlayerHp);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.NotEqual(0, engine.State.RewardPotion);
        Assert.Empty(engine.State.PendingPotionRewards);
    }

    [Fact]
    public void BottlingTheWellspringOffersOnePotionAndCostsNothing()
    {
        var engine = At(RunConstants.EventWellspring);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(99, engine.State.Gold);
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Empty(engine.State.PendingPotionRewards);
    }

    /// <summary>
    /// A potion is offered, never forced. A run with a full belt keeps what it holds and
    /// the offer simply goes unclaimed.
    /// </summary>
    [Fact]
    public void AFullBeltIsNotOverwrittenByAnOffer()
    {
        var engine = At(RunConstants.EventWellspring);
        int held = RunNonCombatEffects.NamedPotion("FoulPotion");
        engine.State.PotionSlots[0] = held;
        engine.State.PotionSlots[1] = held;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        ClaimEverything(engine);

        Assert.Equal(2, HeldPotions(engine.State));
        Assert.All(
            engine.State.PotionSlots.Where(slot => slot != 0),
            slot => Assert.Equal(held, slot)
        );
    }

    // ── The Stone of All Time ────────────────────────────────────────────────

    /// <summary>
    /// Lift has no fixture -- the capture was taken with an empty belt, which locks it --
    /// so its numbers come from the event's own DynamicVars: DrinkMaxHpGain is 10.
    /// </summary>
    [Fact]
    public void LiftingTheStoneDrinksAPotionForTenMaxHp()
    {
        var engine = At(RunConstants.EventStoneOfAllTime);
        engine.State.PotionSlots[0] = RunNonCombatEffects.NamedPotion("FoulPotion");
        int maxHp = engine.State.PlayerMaxHp;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(maxHp + 10, engine.State.PlayerMaxHp);
        Assert.Equal(0, HeldPotions(engine.State));
    }

    [Fact]
    public void LiftingIsRefusedWithNothingToDrink()
    {
        var engine = At(RunConstants.EventStoneOfAllTime);

        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(80, engine.State.PlayerMaxHp);
    }

    [Fact]
    public void PushingCostsSixHpAndEnchantsAnAttackWithVigorous()
    {
        var engine = At(RunConstants.EventStoneOfAllTime);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(58, engine.State.PlayerHp);
        Assert.Equal(DeckSelection.Enchant, engine.State.PendingSelectionKind);
        Assert.Equal((int)Enchantment.Vigorous, engine.State.PendingSelectionArg);

        int attack = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        Assert.Equal(CardType.Attack, GeneratedData.Cards.Get(engine.State.Deck[attack].DefId).Type);

        Assert.Equal(0, engine.Step(attack, -1, out _, out _, out _));
        Assert.Equal(Enchantment.Vigorous, engine.State.Deck[attack].Enchantment);
    }

    /// <summary>Vigorous only takes Attacks, so a deck of Skills refuses Push outright.</summary>
    [Fact]
    public void PushingIsRefusedWithNoAttackToEnchant()
    {
        var engine = At(RunConstants.EventStoneOfAllTime);
        engine.State.Deck.RemoveAll(card =>
            GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
        );

        Assert.Equal(-1, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }
}
