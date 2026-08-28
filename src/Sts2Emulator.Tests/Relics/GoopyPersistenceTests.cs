using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Goopy's block is permanent: the game bumps the DECK version's amount, not only the
/// copy in the fight.
/// </summary>
/// <remarks>
/// <c>Goopy.AfterCardPlayed</c> does <c>Amount++</c> and then
/// <c>Card.DeckVersion.Enchantment.Amount++</c>. The emulator has no link from a combat
/// card back to the deck card it was copied from, so the amounts are matched by identity
/// at combat end — which is why this is worth a test of its own rather than trusting the
/// in-combat one.
/// </remarks>
public class GoopyPersistenceTests
{
    private static RunEngine WithGoopyDeck(params int[] amounts)
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        engine.State.Deck.Clear();
        foreach (int amount in amounts)
        {
            engine.State.Deck.Add(
                new CardInstance(131, false)
                {
                    Enchantment = Enchantment.Goopy,
                    EnchantAmount = amount,
                }
            );
        }

        return engine;
    }

    /// <summary>A fight that grew a goopied card leaves the deck holding the bigger one.</summary>
    [Fact]
    public void TheGrownAmountReachesTheRunDeck()
    {
        var engine = WithGoopyDeck(1, 1);
        var combat = CombatFactory.NewCombat(seed: 0);
        combat.DiscardPile.Clear();
        combat.DrawPile.Clear();
        combat.Hand.Clear();
        combat.ExhaustPile.Clear();
        // One of the two was played and grew; the other did not.
        combat.ExhaustPile.Add(
            new CardInstance(131, false) { Enchantment = Enchantment.Goopy, EnchantAmount = 2 }
        );
        combat.DrawPile.Add(
            new CardInstance(131, false) { Enchantment = Enchantment.Goopy, EnchantAmount = 1 }
        );
        engine.State.ActiveCombat = combat;

        engine.SyncAfterCombatForTest();

        Assert.Equal(
            [2, 1],
            engine.State.Deck.Select(c => c.EnchantAmount).OrderByDescending(a => a)
        );
    }

    /// <summary>A deck with no Goopy in it is left alone.</summary>
    [Fact]
    public void ADeckWithoutGoopyIsUntouched()
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        var before = engine.State.Deck.Select(c => c.EnchantAmount).ToList();
        var combat = CombatFactory.NewCombat(seed: 0);
        engine.State.ActiveCombat = combat;

        engine.SyncAfterCombatForTest();

        Assert.Equal(before, engine.State.Deck.Select(c => c.EnchantAmount));
    }
}

/// <summary>
/// Clone's whole effect: the rest option Pael's Growth adds.
/// </summary>
/// <remarks>
/// The enchantment itself overrides nothing and does nothing in a fight — so a run that
/// takes Pael's Growth and never rests gets nothing from it at all. The option is offered
/// whenever the RELIC is held, not when the deck happens to hold a Clone card:
/// <c>TryModifyRestSiteOptions</c> adds it unconditionally.
/// </remarks>
public class CloneRestSiteTests
{
    private static RunEngine AtARestSite(bool withRelic, params Enchantment[] deck)
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        engine.State.Deck.Clear();
        foreach (var enchantment in deck)
        {
            engine.State.Deck.Add(
                new CardInstance(131, false) { Enchantment = enchantment, EnchantAmount = 4 }
            );
        }

        if (withRelic)
        {
            engine.State.Relics.Add(new RelicInstance(RunConstants.RelicPaelsGrowth));
        }

        engine.State.Phase = RunPhase.Rest;
        return engine;
    }

    [Fact]
    public void TheOptionIsOfferedOnlyWhilePaelsGrowthIsHeld()
    {
        var without = AtARestSite(withRelic: false, Enchantment.Clone);
        var with = AtARestSite(withRelic: true, Enchantment.Clone);

        Assert.False(Masked(without, RunConstants.RestCloneAction));
        Assert.True(Masked(with, RunConstants.RestCloneAction));
    }

    [Fact]
    public void ItCopiesEveryCloneEnchantedCard()
    {
        var engine = AtARestSite(
            withRelic: true,
            Enchantment.Clone,
            Enchantment.None,
            Enchantment.Clone
        );

        engine.Step(RunConstants.RestCloneAction, -1, out _, out _, out _);

        // Two clones copied, the plain card left alone.
        Assert.Equal(5, engine.State.Deck.Count);
        Assert.Equal(4, engine.State.Deck.Count(c => c.Enchantment == Enchantment.Clone));
    }

    /// <summary>Holding the relic with nothing enchanted copies nothing, and is still offered.</summary>
    [Fact]
    public void WithNoCloneCardsItCopiesNothing()
    {
        var engine = AtARestSite(withRelic: true, Enchantment.None, Enchantment.None);

        Assert.True(Masked(engine, RunConstants.RestCloneAction));
        engine.Step(RunConstants.RestCloneAction, -1, out _, out _, out _);

        Assert.Equal(2, engine.State.Deck.Count);
    }

    private static bool Masked(RunEngine engine, int action)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return mask[action] != 0;
    }
}
