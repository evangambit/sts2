using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Byrdpip, and the three-hop chain that reaches it: Byrdonis Nest gives the Byrdonis Egg
/// card, the egg's `TryModifyRestSiteOptions` adds HATCH at a rest site, and taking that
/// grants the relic — whose whole mechanic is turning the egg into a Byrd Swoop.
/// </summary>
/// <remarks>
/// The relic was called unreachable because the chain is invisible to a query over event
/// sources: Byrdonis Nest never says "Byrdpip". That is the third distinct hole in the
/// reachability predicate (E389 found the first two), and each has been a different shape.
///
/// HATCH is the only rest option a CARD puts on the screen rather than a relic, which is
/// why its mask arm reads the deck.
/// </remarks>
public class ByrdpipTests
{
    private static RunEngine AtARestSite(bool withEgg)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        if (withEgg)
        {
            engine.State.Deck.Add(
                new CardInstance(RunNonCombatEffects.ByrdonisEggCard, Upgraded: false)
            );
        }

        engine.State.Phase = RunPhase.Rest;
        engine.State.RestOptionsTaken = 0;
        return engine;
    }

    private static int[] Mask(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return mask;
    }

    [Fact]
    public void AnEggInTheDeckPutsHatchOnTheRestScreen()
    {
        Assert.Equal(1, Mask(AtARestSite(withEgg: true))[RunConstants.RestHatchAction]);
    }

    [Fact]
    public void WithoutAnEggThereIsNoHatch()
    {
        Assert.Equal(0, Mask(AtARestSite(withEgg: false))[RunConstants.RestHatchAction]);
        Assert.Equal(
            -1,
            AtARestSite(withEgg: false).Step(RunConstants.RestHatchAction, -1, out _, out _, out _)
        );
    }

    [Fact]
    public void HatchingGrantsTheRelic()
    {
        var engine = AtARestSite(withEgg: true);

        Assert.Equal(0, engine.Step(RunConstants.RestHatchAction, -1, out _, out _, out _));
        Assert.Contains(engine.State.Relics, relic => relic.DefId == RelicEffects.Byrdpip);
    }

    /// <summary>
    /// The relic's `AfterObtained` turns EVERY egg in the deck into a Byrd Swoop -- a
    /// 0-cost, 14-damage Attack. That is the whole mechanic.
    /// </summary>
    [Fact]
    public void TheEggBecomesAByrdSwoop()
    {
        var engine = AtARestSite(withEgg: true);
        int deck = engine.State.Deck.Count;

        engine.Step(RunConstants.RestHatchAction, -1, out _, out _, out _);

        Assert.Equal(deck, engine.State.Deck.Count);
        Assert.DoesNotContain(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.ByrdonisEggCard
        );
        Assert.Contains(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.ByrdSwoopCard
        );
    }

    /// <summary>
    /// The transformation is the RELIC's, not the rest site's, so it lands however the
    /// relic arrives -- and it catches every egg, not just one.
    /// </summary>
    [Fact]
    public void EveryEggHatchesHoweverTheRelicArrives()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        for (int i = 0; i < 3; i++)
        {
            engine.State.Deck.Add(
                new CardInstance(RunNonCombatEffects.ByrdonisEggCard, Upgraded: false)
            );
        }

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.Byrdpip);

        Assert.Equal(
            3,
            engine.State.Deck.Count(c => c.DefId == RunNonCombatEffects.ByrdSwoopCard)
        );
        Assert.DoesNotContain(
            engine.State.Deck,
            c => c.DefId == RunNonCombatEffects.ByrdonisEggCard
        );
    }

    /// <summary>
    /// HATCH is a one-off, like every other rest option: the screen is answered once and
    /// the next step leaves the room. The relic is gone from the mask either way, because
    /// the egg it reads is no longer in the deck.
    /// </summary>
    [Fact]
    public void TheRestSiteIsSpentAfterHatching()
    {
        var engine = AtARestSite(withEgg: true);
        engine.Step(RunConstants.RestHatchAction, -1, out _, out _, out _);

        Assert.True(engine.State.RestResultPending);
        Assert.Equal(0, Mask(engine)[RunConstants.RestHatchAction]);

        engine.Step(RunConstants.RestHatchAction, -1, out _, out _, out _);
        Assert.NotEqual(RunPhase.Rest, engine.State.Phase);
    }

    /// <summary>
    /// The pet the relic also summons is deliberately not modelled: 9999 HP, an invisible
    /// health bar, and a `NOTHING_MOVE` state machine whose move returns a completed task.
    /// It is an animation anchor for Byrd Swoop, and modelling it would be modelling
    /// nothing.
    /// </summary>
    [Fact]
    public void ThePetChangesNothingAboutACombat()
    {
        var plain = Fight.WithRelics();
        var withPet = Fight.WithRelics(RelicEffects.Byrdpip);

        Assert.Equal(plain.State.Enemies.Count, withPet.State.Enemies.Count);
        Assert.Equal(plain.State.PlayerHp, withPet.State.PlayerHp);
        Assert.Equal(plain.State.OstyHp, withPet.State.OstyHp);
    }
}
