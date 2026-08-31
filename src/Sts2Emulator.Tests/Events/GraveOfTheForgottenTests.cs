using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Grave of the Forgotten: Confront pays a Decay AND enchants a card with Souls Power, or
/// Accept just takes Forgotten Soul.
/// </summary>
/// <remarks>
/// The placeholder had 13 damage and a rolled pool relic on one side, a Decay and 150 gold
/// on the other. The curse was the only piece of the event it actually had, and it was on
/// the wrong option -- the emulator charged for Accept and paid for Confront, exactly
/// backwards.
///
/// Souls Power is the enchantment this event exists to hand out, and it is unlike every
/// other one: its `CanEnchant` reads a KEYWORD rather than a card type, and its
/// `OnEnchant` REMOVES that keyword. It is the only enchantment that takes something away.
/// </remarks>
[CoversEvent("GraveOfTheForgotten")]
public class GraveOfTheForgottenTests
{
    /// <summary>
    /// Only an exhausting card can take Souls Power, and the basic deck has none -- so
    /// every probe here has to put one in. Afterlife exhausts and keeps doing so upgraded,
    /// which nineteen cards do not.
    /// </summary>
    private static RunEngine At(bool withAnExhaustingCard = true)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventGraveOfTheForgotten;
        if (withAnExhaustingCard)
        {
            engine.State.Deck.Add(
                new CardInstance(RunNonCombatEffects.NamedCard("Afterlife"), Upgraded: false)
            );
        }

        return engine;
    }

    [Fact]
    public void AcceptTakesForgottenSoulAndNothingElse()
    {
        var engine = At();
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;
        int deck = engine.State.Deck.Count;

        engine.Step(1, -1, out _, out _, out _);

        Assert.Contains(
            engine.State.Relics,
            relic => relic.DefId == RunNonCombatEffects.NamedRelic("ForgottenSoul")
        );
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(deck, engine.State.Deck.Count);
    }

    [Fact]
    public void ConfrontAddsADecayAndOpensTheEnchantScreen()
    {
        var engine = At();
        int deck = engine.State.Deck.Count;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(deck + 1, engine.State.Deck.Count);
        Assert.Contains(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.NamedCard("Decay")
        );
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
    }

    /// <summary>
    /// `AddCurseToDeck` is awaited BEFORE the selection screen opens and the event
    /// finishes whether or not a card comes back, so the curse is paid up front. The
    /// enchant is what the player may or may not get.
    /// </summary>
    [Fact]
    public void TheCurseIsPaidBeforeTheScreenAndTheEnchantLandsAfterIt()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        int index = Enumerable
            .Range(0, engine.State.Deck.Count)
            .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
        engine.Step(index, -1, out _, out _, out _);

        var enchanted = engine.State.Deck.Where(card =>
            card.Enchantment == Enchantment.SoulsPower
        );
        Assert.Single(enchanted);
        Assert.Equal(1, enchanted.First().EnchantAmount);
    }

    /// <summary>
    /// The enchantment IS the removal of Exhaust -- `OnEnchant` is `RemoveKeyword(Exhaust)`
    /// and nothing else, so the card it lands on stops exhausting for the rest of the run.
    /// </summary>
    [Fact]
    public void SoulsPowerStopsTheCardExhausting()
    {
        int afterlife = RunNonCombatEffects.NamedCard("Afterlife");
        var plain = new CardInstance(afterlife, Upgraded: false);
        Assert.True(plain.IsExhaust());

        Assert.False((plain with { Enchantment = Enchantment.SoulsPower }).IsExhaust());
    }

    /// <summary>
    /// Its `CanEnchant` narrows the base rule to cards that already HAVE Exhaust, which no
    /// other enchantment does -- every other one reads the card TYPE.
    /// </summary>
    [Fact]
    public void OnlyAnExhaustingCardCanTakeIt()
    {
        var afterlife = new CardInstance(RunNonCombatEffects.NamedCard("Afterlife"), Upgraded: false);
        var strike = new CardInstance(RunNonCombatEffects.NamedCard("StrikeIronclad"), false);

        Assert.True(Enchantments.CanEnchant(afterlife, Enchantment.SoulsPower));
        Assert.False(Enchantments.CanEnchant(strike, Enchantment.SoulsPower));
    }

    /// <summary>
    /// Confront's locked variant is `!HasEnchantableCards`; Accept is never locked.
    /// </summary>
    [Fact]
    public void ConfrontIsLockedForADeckThatNeverExhausts()
    {
        var engine = At(withAnExhaustingCard: false);
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        Assert.Equal(0, mask[0]);
        Assert.Equal(1, mask[1]);

        // CONFRONT_LOCKED is an option with a NULL action, so the step has to refuse it
        // too -- a locked Confront must not eat the curse and pay nothing.
        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));
        Assert.DoesNotContain(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.NamedCard("Decay")
        );
    }

    /// <summary>
    /// The whole EVENT is gated on the same test, not just the option: `IsAllowed` is
    /// `All(HasEnchantableCards)`, so a deck with nothing that exhausts never sees the
    /// grave at all -- the locked variant only exists for a deck that lost the card
    /// between the roll and the visit.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void TheEventItselfNeedsAnExhaustingCard(bool hasOne, bool allowed)
    {
        var engine = At(withAnExhaustingCard: hasOne);

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForRun(
                engine.State,
                RunConstants.EventGraveOfTheForgotten
            )
        );
    }
}
