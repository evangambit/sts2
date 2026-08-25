using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The four enchantments the act-2 ancients hand out, in a FIGHT.
/// </summary>
/// <remarks>
/// Enchantments are not act-specific — a card goopied in act 2 is goopied in act 3 and
/// would be in act 1 if anything there applied it — so these are ordinary act-1 combats.
/// What is being checked is the enchantment, not where it came from.
/// </remarks>
public class EnchantmentCombatTests
{
    private static CardInstance Enchanted(int defId, Enchantment enchantment, int amount) =>
        new(defId, false) { Enchantment = enchantment, EnchantAmount = amount };

    /// <summary>Play the first card and hand back the state it left behind.</summary>
    private static CombatState Played(Fight fight)
    {
        fight.Play(0);
        return fight.State;
    }

    // ---- Tezcatara's Ember ----------------------------------------------------------

    /// <summary>
    /// <c>OnEnchant</c> does <c>EnergyCost.UpgradeBy(-cost)</c>: the card's printed cost
    /// is gone for good, which is not the same as the free-this-turn flag.
    /// </summary>
    [Fact]
    public void EmberMakesItsCardFree()
    {
        // The control first, so this test cannot pass by the harness ignoring cost: a
        // PLAIN Strike at zero energy does not land at all.
        var plain = Played(Fight.Hand(Card(IC.StrikeIronclad)).Energy(0).Enemy(hp: 40));
        Assert.Equal(40, plain.Enemies[0].Hp);

        var embered = Played(
            Fight
                .Hand(Enchanted(IC.StrikeIronclad, Enchantment.TezcatarasEmber, 3))
                .Energy(0)
                .Enemy(hp: 40)
        );

        Assert.Equal(0, embered.Energy);
        Assert.True(embered.Enemies[0].Hp < 40, "the embered Strike should have landed");
    }

    /// <summary>
    /// <c>EnchantDamageAdditive</c> adds its amount to a powered attack, every play — the
    /// same shape as Sharp, with no once-only status of its own.
    /// </summary>
    [Fact]
    public void EmberAddsItsAmountToTheAttack()
    {
        var plain = Played(Fight.Hand(Card(IC.StrikeIronclad)).Energy(3).Enemy(hp: 40));
        var embered = Played(
            Fight
                .Hand(Enchanted(IC.StrikeIronclad, Enchantment.TezcatarasEmber, 3))
                .Energy(3)
                .Enemy(hp: 40)
        );

        Assert.Equal(plain.Enemies[0].Hp - 3, embered.Enemies[0].Hp);
    }

    // ---- Goopy ----------------------------------------------------------------------

    /// <summary>
    /// <c>OnEnchant</c> adds Exhaust, so a goopied Defend leaves the fight whatever its
    /// printed keywords say — a plain Defend discards.
    /// </summary>
    [Fact]
    public void GoopyExhaustsItsCard()
    {
        var plain = Played(Fight.Hand(Card(IC.DefendIronclad)).Energy(3).Enemy(hp: 40));
        Assert.Empty(plain.ExhaustPile);
        Assert.Single(plain.DiscardPile);

        var goopy = Played(
            Fight.Hand(Enchanted(IC.DefendIronclad, Enchantment.Goopy, 1)).Energy(3).Enemy(hp: 40)
        );

        Assert.Single(goopy.ExhaustPile);
        Assert.Empty(goopy.DiscardPile);
    }

    /// <summary>
    /// <c>EnchantBlockAdditive</c> is <c>Amount - 1</c>, so a freshly goopied card adds
    /// NOTHING and only starts paying once it has been played.
    /// </summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 4)]
    public void GoopyAddsOneLessBlockThanItsAmount(int amount, int extraBlock)
    {
        var plain = Played(Fight.Hand(Card(IC.DefendIronclad)).Energy(3).Enemy(hp: 40));
        var goopy = Played(
            Fight
                .Hand(Enchanted(IC.DefendIronclad, Enchantment.Goopy, amount))
                .Energy(3)
                .Enemy(hp: 40)
        );

        Assert.Equal(plain.PlayerBlock + extraBlock, goopy.PlayerBlock);
    }

    /// <summary>
    /// <c>AfterCardPlayed</c> bumps the amount — the card is worth one more block next
    /// time. The game bumps the DECK version too, which is what makes it permanent; see
    /// O18, that half is not carried out of the fight yet.
    /// </summary>
    [Fact]
    public void GoopyGrowsWhenItIsPlayed()
    {
        var state = Played(
            Fight.Hand(Enchanted(IC.DefendIronclad, Enchantment.Goopy, 1)).Energy(3).Enemy(hp: 40)
        );

        var played = state.ExhaustPile.Single();
        Assert.Equal(Enchantment.Goopy, played.Enchantment);
        Assert.Equal(2, played.EnchantAmount);
    }

    // ---- Imbued ---------------------------------------------------------------------

    /// <summary>
    /// <c>ShouldStartAtBottomOfDrawPile</c> is true for Imbued alone, and the turn-1
    /// reorder was written against that rule long before the enchantment existed.
    /// </summary>
    [Fact]
    public void ImbuedSinksToTheBottomOfTheDrawPile()
    {
        var pile = new List<CardInstance>
        {
            Enchanted(IC.DefendIronclad, Enchantment.Imbued, 1),
            Card(IC.StrikeIronclad),
            Card(IC.StrikeIronclad),
        };

        CombatFactory.ApplyTurnOneDrawPileReorder(pile, handDraw: 1);

        Assert.Equal(Enchantment.Imbued, pile[^1].Enchantment);
        Assert.All(pile.Take(2), card => Assert.Equal(Enchantment.None, card.Enchantment));
    }

    /// <summary>
    /// <c>AfterAutoPrePlayPhaseEntered</c> plays the card on turn 1 — from the BOTTOM of
    /// the draw pile, where the reorder just put it, and before the player has moved.
    /// </summary>
    [Fact]
    public void ImbuedAutoPlaysItselfOnTurnOne()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad))
            .Draw(Enchanted(IC.DefendIronclad, Enchantment.Imbued, 1))
            .Energy(3)
            .Enemy(hp: 40);
        // The harness builds a state directly rather than through CombatFactory, so the
        // combat-start queueing has to be asked for; a real fight gets it from Reset.
        CombatFactory.QueueCombatStartAutoPlays(fight.State);

        // The player's first action is the Strike; the Defend should already have gone
        // off by the time it resolves.
        fight.Play(0);

        Assert.DoesNotContain(fight.State.DrawPile, card => card.Enchantment == Enchantment.Imbued);
        Assert.True(fight.State.PlayerBlock > 0, "the imbued Defend should have played");
    }

    /// <summary>An auto-play is free — it does not spend the player's energy.</summary>
    [Fact]
    public void ImbuedsAutoPlayCostsNoEnergy()
    {
        var withImbued = Fight
            .Hand(Card(IC.StrikeIronclad))
            .Draw(Enchanted(IC.DefendIronclad, Enchantment.Imbued, 1))
            .Energy(3)
            .Enemy(hp: 40);
        CombatFactory.QueueCombatStartAutoPlays(withImbued.State);
        withImbued.Play(0);

        var plain = Fight
            .Hand(Card(IC.StrikeIronclad))
            .Draw(Card(IC.DefendIronclad))
            .Energy(3)
            .Enemy(hp: 40);
        plain.Play(0);

        Assert.Equal(plain.State.Energy, withImbued.State.Energy);
    }

    // ---- Clone ----------------------------------------------------------------------

    /// <summary>
    /// Clone is an EMPTY <c>EnchantmentModel</c> — it overrides nothing, so it changes
    /// nothing about playing the card. Its effect is at a rest site, where
    /// <c>CloneRestSiteOption</c> copies every Clone-enchanted card into the deck. A test
    /// that it does nothing here is worth having: "the emulator ignores it" and "the game
    /// ignores it" look identical until someone writes the difference down.
    /// </summary>
    [Fact]
    public void CloneChangesNothingInAFight()
    {
        var plain = Played(Fight.Hand(Card(IC.StrikeIronclad)).Energy(3).Enemy(hp: 40));
        var cloned = Played(
            Fight.Hand(Enchanted(IC.StrikeIronclad, Enchantment.Clone, 4)).Energy(3).Enemy(hp: 40)
        );

        Assert.Equal(plain.Enemies[0].Hp, cloned.Enemies[0].Hp);
        Assert.Equal(plain.Energy, cloned.Energy);
        Assert.Equal(plain.DiscardPile.Count, cloned.DiscardPile.Count);
        Assert.Empty(cloned.ExhaustPile);
    }
}
