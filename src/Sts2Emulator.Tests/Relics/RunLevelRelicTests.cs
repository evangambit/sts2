using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Relics that never fire in combat, read off MegaCrit.Sts2.Core.Models.Relics: War Paint
/// and Whetstone CardsVar(2) upgraded on pickup, the three eggs upgrading a card of their
/// type as it joins the deck, White Beast Statue ShouldForcePotionReward, Membership Card
/// DynamicVar("Discount", 50m), Tiny Mailbox's two PotionRewards at a rest, Lizard Tail
/// HealVar(50m) on the death it refuses.
/// </summary>
public class RunLevelRelicTests
{
    private const int Strike = 472;
    private const int Defend = 131;
    private const int Inflame = 265;

    [Fact]
    public void WarPaintUpgradesTwoSkillsAndLeavesAttacksAlone()
    {
        var state = RunWithDeck(Strike, Strike, Defend, Defend, Defend);

        RunNonCombatEffects.ApplyRelicPickup(state, RunConstants.RelicWarPaint);

        Assert.Equal(2, state.Deck.Count(card => card.DefId == Defend && card.Upgraded));
        Assert.DoesNotContain(state.Deck, card => card.DefId == Strike && card.Upgraded);
    }

    [Fact]
    public void WhetstoneUpgradesTwoAttacksAndLeavesSkillsAlone()
    {
        var state = RunWithDeck(Strike, Strike, Strike, Defend, Defend);

        RunNonCombatEffects.ApplyRelicPickup(state, RunConstants.RelicWhetstone);

        Assert.Equal(2, state.Deck.Count(card => card.DefId == Strike && card.Upgraded));
        Assert.DoesNotContain(state.Deck, card => card.DefId == Defend && card.Upgraded);
    }

    /// <summary>Two is a maximum, not a promise: a deck with one Skill gets one.</summary>
    [Fact]
    public void WarPaintUpgradesWhatItCanFind()
    {
        var state = RunWithDeck(Strike, Defend);

        RunNonCombatEffects.ApplyRelicPickup(state, RunConstants.RelicWarPaint);

        Assert.Single(state.Deck.Where(card => card.Upgraded));
    }

    [Theory]
    [InlineData(RunConstants.RelicMoltenEgg, Strike, Defend)]
    [InlineData(RunConstants.RelicToxicEgg, Defend, Strike)]
    [InlineData(RunConstants.RelicFrozenEgg, Inflame, Strike)]
    public void AnEggUpgradesItsOwnCardTypeAsItJoinsTheDeck(int eggId, int matching, int other)
    {
        var state = RunWithDeck();
        state.Relics.Add(new RelicInstance(eggId));

        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(matching, Upgraded: false));
        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(other, Upgraded: false));

        Assert.True(state.Deck[0].Upgraded, "the egg's own type should arrive upgraded");
        Assert.False(state.Deck[1].Upgraded, "every other type should arrive as printed");
    }

    [Fact]
    public void NoEggMeansNothingArrivesUpgraded()
    {
        var state = RunWithDeck();

        RunNonCombatEffects.AddCardToDeck(state, new CardInstance(Strike, Upgraded: false));

        Assert.False(state.Deck[0].Upgraded);
    }

    [Fact]
    public void MembershipCardHalvesEveryPriceInTheShop()
    {
        var plain = ShopRun();
        var withCard = ShopRun();
        withCard.Relics.Add(new RelicInstance(RunConstants.RelicMembershipCard));

        RunRewardGenerator.EnterShop(plain);
        RunRewardGenerator.EnterShop(withCard);

        Assert.Contains(plain.ShopCosts, cost => cost > 0);
        Assert.Equal(plain.ShopCosts.Select(cost => cost / 2), withCard.ShopCosts);
    }

    [Fact]
    public void WhiteBeastStatueTurnsEveryCombatIntoAPotion()
    {
        var plain = CombatRewardRun(withStatue: false);
        var withStatue = CombatRewardRun(withStatue: true);

        Assert.Equal(0, plain.RewardPotion);
        Assert.NotEqual(0, withStatue.RewardPotion);
    }

    /// <summary>
    /// PotionRewardOdds.Roll draws its number and moves the pity counter whether or not
    /// the hook forces the reward — so a forced potion has to push the odds DOWN, the way
    /// a rolled one would, rather than skipping the roll and pushing them up.
    /// </summary>
    [Fact]
    public void WhiteBeastStatueStillMovesThePityCounter()
    {
        var plain = CombatRewardRun(withStatue: false);
        var withStatue = CombatRewardRun(withStatue: true);

        Assert.True(withStatue.PotionRewardOdds < plain.PotionRewardOdds);
    }

    [Fact]
    public void TinyMailboxFillsTheBeltWhenTheRunRests()
    {
        var plain = RestingRun(withMailbox: false);
        var withMailbox = RestingRun(withMailbox: true);

        Assert.All(plain.PotionSlots, slot => Assert.Equal(0, slot));
        Assert.Equal(2, withMailbox.PotionSlots.Count(slot => slot != 0));
    }

    /// <summary>
    /// The relic is spent for the run, not the combat, so a second combat cannot revive
    /// the player again — the combat rebuilds its relic list from ids either way.
    /// </summary>
    [Fact]
    public void LizardTailRefusesOneDeathAndHealsToHalf()
    {
        var combat = new CombatState();
        CombatFactory.Reset(
            combat,
            new Random(0),
            TestDeck.StarterDeckIds,
            encounterId: 1,
            [RelicEffects.LizardTail]
        );
        combat.PlayerHp = 4;

        CardEffects.DealDamageToPlayer(combat, 40);
        RelicEffects.ApplyAfterPlayerHpChanged(combat);

        Assert.Equal(combat.PlayerMaxHp / 2, combat.PlayerHp);

        combat.PlayerHp = 3;
        CardEffects.DealDamageToPlayer(combat, 40);
        RelicEffects.ApplyAfterPlayerHpChanged(combat);

        // A card-dealt hit does not floor at zero, so this is "dead", not "at zero".
        Assert.True(combat.PlayerHp <= 0, "the second death should stand");
    }

    [Fact]
    public void LizardTailStaysSpentIntoTheNextCombat()
    {
        var spent = new List<int>();
        var first = new CombatState();
        CombatFactory.Reset(
            first,
            new Random(0),
            TestDeck.StarterDeckIds,
            encounterId: 1,
            [RelicEffects.LizardTail]
        );
        first.PlayerHp = 1;
        CardEffects.DealDamageToPlayer(first, 40);
        RelicEffects.ApplyAfterPlayerHpChanged(first);
        RelicEffects.CollectUsedUpRelics(first, spent);

        Assert.Equal([RelicEffects.LizardTail], spent);

        var second = new CombatState();
        CombatFactory.Reset(
            second,
            new Random(0),
            TestDeck.StarterDeckIds,
            encounterId: 1,
            [RelicEffects.LizardTail]
        );
        RelicEffects.RestoreUsedUpRelics(second, spent);
        second.PlayerHp = 1;
        CardEffects.DealDamageToPlayer(second, 40);
        RelicEffects.ApplyAfterPlayerHpChanged(second);

        Assert.True(second.PlayerHp <= 0, "a spent Lizard Tail should not fire again");
    }

    private static RunState RunWithDeck(params int[] cardIds)
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.Deck = cardIds.Select(id => new CardInstance(id, false)).ToList();
        return engine.State;
    }

    private static RunState ShopRun()
    {
        var engine = new RunEngine();
        engine.Reset("0");
        return engine.State;
    }

    private static RunState CombatRewardRun(bool withStatue)
    {
        var engine = new RunEngine();
        engine.Reset("0");
        engine.State.CurrentNodeType = RunConstants.NodeNormal;
        if (withStatue)
        {
            engine.State.Relics.Add(new RelicInstance(RunConstants.RelicWhiteBeastStatue));
        }

        RunRewardGenerator.GenerateCombatRewards(engine.State);
        return engine.State;
    }

    private static RunState RestingRun(bool withMailbox)
    {
        var engine = new RunEngine();
        engine.Reset("0");
        if (withMailbox)
        {
            engine.State.Relics.Add(new RelicInstance(RelicEffects.TinyMailbox));
        }

        engine.State.Phase = RunPhase.Rest;
        engine.State.PlayerHp = 1;
        Array.Clear(engine.State.PotionSlots);
        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);
        return engine.State;
    }
}
