using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Three events whose emulator options matched nothing in their models.
/// </summary>
/// <remarks>
/// Found by chasing the last five `UpgradeFirstCard` / `TransformFirstCard` call sites,
/// which were the emulator choosing a card for the player. Reading each model to see what
/// the real choice was turned up something bigger: all three events were placeholders.
/// Zen Weaver was heal / upgrade a card / +5 max HP against a model that sells removals;
/// Reflections was transform-a-card / upgrade-a-card against a model that reshuffles
/// upgrades or clones the whole deck; Tinker Time offered three options on a page the
/// model gives ONE, and builds a card over three pages.
/// </remarks>
public class ZenWeaverReflectionsTinkerTests
{
    private static RunEngine At(int eventId, string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = eventId;
        return engine;
    }

    private static int Choose(RunEngine engine, int option) =>
        engine.Step(option, -1, out _, out _, out _);

    private static int[] Mask(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return mask;
    }

    // ---- Zen Weaver -------------------------------------------------------------

    [Fact]
    public void BreathingTechniquesBuysTwoEnlightenmentsForFifty()
    {
        var engine = At(RunConstants.EventZenWeaver);
        engine.State.Gold = 300;
        int before = engine.State.Deck.Count;

        Assert.Equal(0, Choose(engine, 0));

        Assert.Equal(250, engine.State.Gold);
        Assert.Equal(before + 2, engine.State.Deck.Count);
        Assert.Equal(
            2,
            engine.State.Deck.Count(card => card.DefId == RunConstants.CardEnlightenment)
        );
    }

    [Fact]
    public void EmotionalAwarenessOpensARemovalTheLPlayerAnswers()
    {
        var engine = At(RunConstants.EventZenWeaver);
        engine.State.Gold = 200;
        int before = engine.State.Deck.Count;

        Assert.Equal(0, Choose(engine, 1));

        // The screen, not a card the emulator picked.
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(DeckSelection.Remove, engine.State.PendingSelectionKind);
        Assert.Equal(before, engine.State.Deck.Count);
        Assert.Equal(75, engine.State.Gold);

        Assert.Equal(0, Choose(engine, 2));
        Assert.Equal(before - 1, engine.State.Deck.Count);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void ArachnidAcupunctureTakesTwoCardsForTwoFifty()
    {
        var engine = At(RunConstants.EventZenWeaver);
        engine.State.Gold = 250;
        int before = engine.State.Deck.Count;

        Assert.Equal(0, Choose(engine, 2));
        Assert.Equal(0, engine.State.Gold);

        Assert.Equal(0, Choose(engine, 0));
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Assert.Equal(0, Choose(engine, 0));

        Assert.Equal(before - 2, engine.State.Deck.Count);
        Assert.Equal(RunPhase.Event, engine.State.Phase);
    }

    [Fact]
    public void TheTwoRemovalsAreLockedBelowTheirPrice()
    {
        // CreateLockedOption: the row is shown and does nothing. Breathing Techniques is
        // never locked -- and cannot be, since IsAllowed already needs 125 gold.
        var engine = At(RunConstants.EventZenWeaver);
        engine.State.Gold = 130;

        var mask = Mask(engine);
        Assert.Equal(1, mask[0]);
        Assert.Equal(1, mask[1]);
        Assert.Equal(0, mask[2]);
        Assert.Equal(-1, Choose(engine, 2));

        engine.State.Gold = 250;
        Assert.Equal(1, Mask(engine)[2]);
    }

    [Fact]
    public void TheWeaverOnlyTurnsUpForARunThatCanAffordIt()
    {
        var engine = At(RunConstants.EventZenWeaver);

        engine.State.Gold = 124;
        Assert.False(RunNonCombatEffects.IsEventAllowed(engine.State, RunConstants.EventZenWeaver));

        engine.State.Gold = 125;
        Assert.True(RunNonCombatEffects.IsEventAllowed(engine.State, RunConstants.EventZenWeaver));
    }

    // ---- Reflections ------------------------------------------------------------

    [Fact]
    public void TouchAMirrorDowngradesTwoAndUpgradesFour()
    {
        var engine = At(RunConstants.EventReflections);
        // A deck where the two loops cannot be confused: everything starts upgraded, so
        // two come down and then four go up -- and the four are drawn from a list that
        // INCLUDES the two just knocked down.
        for (int i = 0; i < engine.State.Deck.Count; i++)
        {
            engine.State.Deck[i] = engine.State.Deck[i] with { Upgraded = true };
        }

        int size = engine.State.Deck.Count;
        Assert.Equal(0, Choose(engine, 0));

        Assert.Equal(size, engine.State.Deck.Count);
        int upgraded = engine.State.Deck.Count(card => card.Upgraded);
        // Two down, then up to four back up from a pool that includes them: never worse
        // than it started, and never better.
        Assert.InRange(upgraded, size - 2, size);
    }

    [Fact]
    public void TouchAMirrorOnAnUnupgradedDeckJustUpgradesFour()
    {
        var engine = At(RunConstants.EventReflections);
        Assert.All(engine.State.Deck, card => Assert.False(card.Upgraded));

        Assert.Equal(0, Choose(engine, 0));

        // Nothing to downgrade, so that loop breaks immediately and four go up.
        Assert.Equal(4, engine.State.Deck.Count(card => card.Upgraded));
    }

    [Fact]
    public void ShatterCopiesTheWholeDeckAndAddsOneBadLuck()
    {
        var engine = At(RunConstants.EventReflections);
        int size = engine.State.Deck.Count;

        Assert.Equal(0, Choose(engine, 1));

        // Every card copied once, plus the curse -- and the copies are not copied.
        Assert.Equal(size * 2 + 1, engine.State.Deck.Count);
        Assert.Equal(1, engine.State.Deck.Count(card => card.DefId == RunConstants.CardBadLuck));
    }

    [Fact]
    public void ShatterKeepsWhatEachCardWas()
    {
        var engine = At(RunConstants.EventReflections);
        engine.State.Deck[0] = engine.State.Deck[0] with { Upgraded = true };
        var original = engine.State.Deck[0];

        Assert.Equal(0, Choose(engine, 1));

        // CloneCard, not a fresh card of the same def: the upgrade rides along.
        Assert.Equal(2, engine.State.Deck.Count(card => card == original));
    }

    // ---- Tinker Time ------------------------------------------------------------

    [Fact]
    public void TinkerTimeOpensWithExactlyOneOption()
    {
        var engine = At(RunConstants.EventTinkerTime);

        var mask = Mask(engine);
        Assert.Equal(1, mask[0]);
        Assert.Equal(0, mask[1]);
        Assert.Equal(0, mask[2]);
        Assert.Equal(-1, Choose(engine, 1));
    }

    [Fact]
    public void TheThreePagesEndInAMadScienceCard()
    {
        var engine = At(RunConstants.EventTinkerTime);
        int before = engine.State.Deck.Count;

        Assert.Equal(0, Choose(engine, 0));
        Assert.Equal(1, engine.State.EventPage);
        // TakeRandom(2, Rng) over three card types.
        Assert.Equal(2, engine.State.EventRandomOffer.Length);
        Assert.Equal(2, engine.State.EventRandomOffer.Distinct().Count());
        Assert.All(
            engine.State.EventRandomOffer,
            type =>
                Assert.Contains(
                    (CardType)type,
                    new[] { CardType.Attack, CardType.Skill, CardType.Power }
                )
        );

        var chosenType = (CardType)engine.State.EventRandomOffer[0];
        Assert.Equal(0, Choose(engine, 0));
        Assert.Equal(2, engine.State.EventPage);
        Assert.Equal(chosenType, engine.State.TinkerCardType);

        // The riders offered belong to the type that was chosen, and only to it.
        var expected = chosenType switch
        {
            CardType.Attack => new[]
            {
                TinkerRider.Sapping,
                TinkerRider.Violence,
                TinkerRider.Choking,
            },
            CardType.Skill => new[]
            {
                TinkerRider.Energized,
                TinkerRider.Wisdom,
                TinkerRider.Chaos,
            },
            _ => new[] { TinkerRider.Expertise, TinkerRider.Curious, TinkerRider.Improvement },
        };
        Assert.Equal(2, engine.State.EventRandomOffer.Length);
        Assert.All(
            engine.State.EventRandomOffer,
            rider => Assert.Contains((TinkerRider)rider, expected)
        );

        var chosenRider = (TinkerRider)engine.State.EventRandomOffer[1];
        Assert.Equal(0, Choose(engine, 1));

        Assert.Equal(before + 1, engine.State.Deck.Count);
        var built = engine.State.Deck[^1];
        Assert.Equal(RunConstants.CardMadScience, built.DefId);
        Assert.Equal(chosenType, built.TinkerType);
        Assert.Equal(chosenRider, built.TinkerRider);
    }

    [Fact]
    public void TheOfferedPairIsWhatTheMaskOffers()
    {
        var engine = At(RunConstants.EventTinkerTime);
        Choose(engine, 0);

        var mask = Mask(engine);
        Assert.Equal(1, mask[0]);
        Assert.Equal(1, mask[1]);
        Assert.Equal(0, mask[2]);
        // The third candidate was shuffled out, so there is no third row to click.
        Assert.Equal(-1, Choose(engine, 2));
    }

    [Fact]
    public void EveryOtherCardCarriesNoTinkerChoice()
    {
        var engine = At(RunConstants.EventTinkerTime);

        Assert.All(
            engine.State.Deck,
            card =>
            {
                Assert.Equal(CardType.None, card.TinkerType);
                Assert.Equal(TinkerRider.None, card.TinkerRider);
            }
        );
    }
}
