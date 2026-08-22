using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Tablet of Truth: read it five times or stop while you still have a run.
///
/// Each Decipher costs Max HP and upgrades a card, and the price doubles before the last
/// one asks for everything -- 3, 6, 12, 24, then MaxHp - 1. The fifth upgrades the WHOLE
/// deck, which is what the first four are paying for.
///
/// The emulator read exactly one secret, for a flat 3, and upgraded whichever card came
/// first. Every fixture passed: a capture stops at the first page, and the first page's
/// first option costs exactly what the emulator charged. The event's entire decision -- how
/// far to read before it takes the run -- was missing rather than wrong.
/// </summary>
public class TabletOfTruthTests
{
    private static RunEngine AtTheTablet(string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = RunConstants.EventTabletOfTruth;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static bool StillReading(RunState state) =>
        state.Phase == RunPhase.Event && state.EventId != RunConstants.EventResultPending;

    private static int Upgraded(RunState state) => state.Deck.Count(card => card.Upgraded);

    [Fact]
    public void EachSecretCostsMoreThanTheLast()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 200;
        engine.State.PlayerHp = 200;

        foreach (int cost in new[] { 3, 6, 12, 24 })
        {
            int maxHp = engine.State.PlayerMaxHp;
            Assert.Equal(cost, RunNonCombatEffects.TabletOfTruthCost(engine.State));

            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

            Assert.Equal(maxHp - cost, engine.State.PlayerMaxHp);
        }
    }

    /// <summary>The fifth reading takes all but one point of Max HP.</summary>
    [Fact]
    public void TheLastSecretCostsAlmostEverything()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 200;
        engine.State.PlayerHp = 200;

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        }

        Assert.Equal(
            engine.State.PlayerMaxHp - 1,
            RunNonCombatEffects.TabletOfTruthCost(engine.State)
        );

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(1, engine.State.PlayerMaxHp);
        Assert.Equal(1, engine.State.PlayerHp);
    }

    /// <summary>
    /// The first four readings upgrade one card each; the fifth upgrades every upgradable
    /// card in the deck, which is the payoff the escalation is buying.
    /// </summary>
    [Fact]
    public void TheLastSecretUpgradesTheWholeDeck()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 200;
        engine.State.PlayerHp = 200;
        int upgradable = engine.State.Deck.Count(RunConstants.IsRunCardUpgradable);
        Assert.True(upgradable > 4, "the starter deck should have more than four upgradables");

        for (int i = 1; i <= 4; i++)
        {
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
            Assert.Equal(i, Upgraded(engine.State));
        }

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(upgradable, Upgraded(engine.State));
        Assert.DoesNotContain(engine.State.Deck, RunConstants.IsRunCardUpgradable);
    }

    [Fact]
    public void TheTabletRunsOutAfterFiveSecrets()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 200;
        engine.State.PlayerHp = 200;

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
            Assert.True(StillReading(engine.State), $"the tablet stopped after {i + 1}");
        }

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.False(StillReading(engine.State));
    }

    /// <summary>
    /// A tablet the run cannot afford ends it: LoseMaxHpAndUpgrade takes MaxHp - 1 and
    /// then calls Kill outright when the price is not less than Max HP.
    /// </summary>
    [Fact]
    public void ReadingWhatYouCannotAffordKillsYou()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 3;
        engine.State.PlayerHp = 3;

        Assert.Equal(3, RunNonCombatEffects.TabletOfTruthCost(engine.State));
        Assert.Equal(0, engine.Step(0, -1, out _, out bool terminal, out _));

        Assert.Equal(0, engine.State.PlayerHp);
        Assert.True(terminal);
        Assert.Equal(RunPhase.Complete, engine.State.Phase);
    }

    /// <summary>Smashing the tablet heals 20 and reads nothing.</summary>
    [Fact]
    public void SmashingHealsAndEndsIt()
    {
        var engine = AtTheTablet();
        engine.State.PlayerHp = 40;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(60, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);
        Assert.Equal(0, Upgraded(engine.State));
        Assert.False(StillReading(engine.State));
    }

    /// <summary>
    /// On a later page the second option is Give Up, not Smash -- so walking away from a
    /// half-read tablet must not hand over the 20 HP the first page offered.
    /// </summary>
    [Fact]
    public void GivingUpPartWayThroughHealsNothing()
    {
        var engine = AtTheTablet();
        engine.State.PlayerMaxHp = 200;
        engine.State.PlayerHp = 100;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        int hp = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.False(StillReading(engine.State));
    }
}
