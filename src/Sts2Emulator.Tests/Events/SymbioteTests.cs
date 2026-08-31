using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Symbiote: Approach enchants one card with Corrupted, or Kill with Fire transforms one
/// the player picks.
/// </summary>
/// <remarks>
/// The step was already right -- both options open a selection screen off the event's own
/// stream -- and the MASK was not: it offered Approach for any deck holding an Attack,
/// where the game's locked variant tests `CanEnchant`. That is the whole enchantment rule,
/// not just the card type, so a deck whose only Attacks already carry an enchantment was
/// offered an option its own step then refused.
/// </remarks>
[CoversEvent("Symbiote")]
public class SymbioteTests
{
    private static RunEngine At()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventSymbiote;
        return engine;
    }

    private static int[] Mask(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return mask;
    }

    [Fact]
    public void ApproachEnchantsOneChosenCardWithCorrupted()
    {
        var engine = At();

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        engine.Step(index, -1, out _, out _, out _);

        var corrupted = engine.State.Deck.Where(card =>
            card.Enchantment == Enchantment.Corrupted
        );
        Assert.Single(corrupted);
        // `CardCmd.Enchant<Corrupted>(cardModel, 1m)` -- every event enchantment is 1.
        Assert.Equal(1, corrupted.First().EnchantAmount);
    }

    /// <summary>Corrupted is Attacks only, so the screen offers nothing else.</summary>
    [Fact]
    public void TheApproachScreenOffersOnlyAttacks()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            if (RunNonCombatEffects.CanSelectCard(engine.State, i))
            {
                Assert.Equal(
                    CardType.Attack,
                    GeneratedData.Cards.Get(engine.State.Deck[i].DefId).Type
                );
            }
        }
    }

    /// <summary>`CardsVar(1)`: one card burns, and the player says which.</summary>
    [Fact]
    public void KillWithFireTransformsOneChosenCard()
    {
        var engine = At();
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        int burned = engine.State.Deck[index].DefId;
        int copiesBefore = engine.State.Deck.Count(card => card.DefId == burned);
        engine.Step(index, -1, out _, out _, out _);

        // One in, one out: the transform replaces rather than adds, and the replacement
        // is not the card it ate.
        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.Equal(
            copiesBefore - 1,
            engine.State.Deck.Count(card => card.DefId == burned)
        );
    }

    /// <summary>
    /// The locked variant is `!pile.Cards.Any(CanEnchant)`, which is the whole rule. An
    /// Attack that already carries an enchantment cannot take Corrupted, so a deck of
    /// them locks Approach even though it is full of Attacks.
    /// </summary>
    [Fact]
    public void ApproachIsLockedWhenEveryAttackIsAlreadyEnchanted()
    {
        var engine = At();
        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            if (GeneratedData.Cards.Get(engine.State.Deck[i].DefId).Type == CardType.Attack)
            {
                engine.State.Deck[i] = engine.State.Deck[i] with
                {
                    Enchantment = Enchantment.Sharp,
                    EnchantAmount = 2,
                };
            }
        }

        Assert.Equal(0, Mask(engine)[0]);
        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));
    }

    /// <summary>Kill with Fire has no locked variant at all -- the game builds it flat.</summary>
    [Fact]
    public void KillWithFireIsNeverLocked()
    {
        var engine = At();
        engine.State.Deck.RemoveAll(card =>
            GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
        );

        Assert.Equal(0, Mask(engine)[0]);
        Assert.Equal(1, Mask(engine)[1]);
    }

    /// <summary>
    /// `CurrentActIndex > 0`: the symbiote is an act-2 sight, which is why the emulator's
    /// act-1 run never rolls it.
    /// </summary>
    [Fact]
    public void ItIsNeverOfferedInActOne()
    {
        var engine = At();

        Assert.False(
            RunNonCombatEffects.IsEventAllowedForTests(engine.State, RunConstants.EventSymbiote)
        );
    }
}
