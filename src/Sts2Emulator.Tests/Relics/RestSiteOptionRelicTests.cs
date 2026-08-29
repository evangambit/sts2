using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

// Girya and Shovel, the two relics that add a REST SITE OPTION. They needed the action
// space widened rather than a relic body, which is why they were left out of the rare
// batch: `TryModifyRestSiteOptions` is a different kind of hook from anything the shared
// pool had needed so far.

public class GiryaTests
{
    private static RunEngine AtARestSite(params int[] relicIds)
    {
        var engine = new RunEngine();
        engine.Reset("GIRYA");
        engine.State.Phase = RunPhase.Rest;
        foreach (int id in relicIds)
        {
            engine.State.Relics.Add(new RelicInstance(id));
        }

        return engine;
    }

    private static int[] RestActions(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return [.. Enumerable.Range(0, mask.Length).Where(i => mask[i] != 0)];
    }

    [Fact]
    public void TheLiftOptionIsOfferedOnlyWhileTheRelicIsHeld()
    {
        Assert.DoesNotContain(RunConstants.RestLiftAction, RestActions(AtARestSite()));
        Assert.Contains(
            RunConstants.RestLiftAction,
            RestActions(AtARestSite(RelicEffects.Girya))
        );
    }

    /// <summary>
    /// `TryModifyRestSiteOptions` returns FALSE at three lifts, so the option leaves the
    /// screen entirely rather than becoming a no-op the player can still pick.
    /// </summary>
    [Fact]
    public void TheOptionLeavesTheScreenAfterThreeLifts()
    {
        var engine = AtARestSite(RelicEffects.Girya);
        for (int i = 0; i < 3; i++)
        {
            Assert.Contains(RunConstants.RestLiftAction, RestActions(engine));
            engine.Step(RunConstants.RestLiftAction, -1, out _, out _, out _);
            // A NEW rest visit: each option can be taken once per visit now that Miniature
            // Tent can keep the screen open, so the per-visit mask resets with the visit.
            engine.State.Phase = RunPhase.Rest;
            engine.State.RestResultPending = false;
            engine.State.RestOptionsTaken = 0;
        }

        Assert.DoesNotContain(RunConstants.RestLiftAction, RestActions(engine));
    }

    /// <summary>Each lift is a point of Strength at the start of every later combat.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void EachLiftIsAPointOfStrengthInEveryCombat(int lifts, int strength)
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            TestDeck.StarterDeckIds,
            1,
            [RelicEffects.Girya]
        );
        int index = state.Relics.FindIndex(relic => relic.DefId == RelicEffects.Girya);
        state.Relics[index] = state.Relics[index] with { Counter = lifts };

        // Re-run the combat-start hooks now the counter is set, the way the run does when
        // it copies its relic counters onto a fresh combat.
        var fresh = new CombatState();
        CombatFactory.Reset(fresh, new Random(0), TestDeck.StarterDeckIds, 1, []);
        fresh.Relics.Add(new RelicInstance(RelicEffects.Girya, lifts));
        RelicEffects.ApplyCombatStart(fresh, new Random(0));

        Assert.Equal(strength, BuffSystem.Get(fresh.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>
    /// The lift count is RUN state, and a combat is handed relic ids — so the run has to
    /// copy its counters across or the Strength never arrives. That was the whole reason
    /// this relic needed more than a body.
    /// </summary>
    [Fact]
    public void TheLiftCountReachesTheCombat()
    {
        var engine = AtARestSite(RelicEffects.Girya);
        engine.Step(RunConstants.RestLiftAction, -1, out _, out _, out _);

        int index = engine.State.Relics.FindIndex(relic => relic.DefId == RelicEffects.Girya);
        Assert.Equal(1, engine.State.Relics[index].Counter);
    }
}

public class ShovelTests
{
    private static RunEngine AtARestSite(params int[] relicIds)
    {
        var engine = new RunEngine();
        engine.Reset("SHOVEL");
        engine.State.Phase = RunPhase.Rest;
        foreach (int id in relicIds)
        {
            engine.State.Relics.Add(new RelicInstance(id));
        }

        return engine;
    }

    private static int[] RestActions(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return [.. Enumerable.Range(0, mask.Length).Where(i => mask[i] != 0)];
    }

    [Fact]
    public void TheDigOptionIsOfferedOnlyWhileTheRelicIsHeld()
    {
        Assert.DoesNotContain(RunConstants.RestDigAction, RestActions(AtARestSite()));
        Assert.Contains(
            RunConstants.RestDigAction,
            RestActions(AtARestSite(RelicEffects.Shovel))
        );
    }

    /// <summary>
    /// Digging pulls from the FRONT of the player's grab bag — the same queue and the same
    /// end an elite reward uses, so a dug relic is one the run will not offer again.
    /// </summary>
    [Fact]
    public void DiggingGivesARelicFromTheFrontOfTheBag()
    {
        var engine = AtARestSite(RelicEffects.Shovel);
        int before = engine.State.Relics.Count;

        engine.Step(RunConstants.RestDigAction, -1, out _, out _, out _);

        Assert.Equal(before + 1, engine.State.Relics.Count);
    }

    /// <summary>Unlike Girya's, the option never runs out — it is offered every rest.</summary>
    [Fact]
    public void ItCanBeDugMoreThanOnce()
    {
        var engine = AtARestSite(RelicEffects.Shovel);
        engine.Step(RunConstants.RestDigAction, -1, out _, out _, out _);
        // A new visit, for the same reason as above.
        engine.State.Phase = RunPhase.Rest;
        engine.State.RestResultPending = false;
        engine.State.RestOptionsTaken = 0;

        Assert.Contains(RunConstants.RestDigAction, RestActions(engine));
    }
}
