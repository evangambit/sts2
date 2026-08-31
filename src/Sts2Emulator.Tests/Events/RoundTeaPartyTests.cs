using System.Linq;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Round Tea Party: Royal Poison and a heal to FULL, or 11 unblockable for a pool relic.
/// </summary>
/// <remarks>
/// The emulator healed 18 on one side and paid 80 gold on the other -- neither number,
/// neither relic, and no second page. Picking the fight is a two-step option: the first
/// page only warns, and CONTINUE_FIGHT on the second is what charges and pays.
/// `.ThatWontSaveToChoiceHistory()` on that option is the tell -- it marks a continuation
/// of a choice already made rather than a choice of its own.
/// </remarks>
[CoversEvent("RoundTeaParty")]
public class RoundTeaPartyTests
{
    private static RunEngine At()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventRoundTeaParty;
        return engine;
    }

    [Fact]
    public void TeaIsRoyalPoisonAndAFullHeal()
    {
        var engine = At();
        engine.State.PlayerHp = 20;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Contains(
            engine.State.Relics,
            relic => relic.DefId == RunNonCombatEffects.NamedRelic("RoyalPoison")
        );
        Assert.Equal(engine.State.PlayerMaxHp, engine.State.PlayerHp);
    }

    /// <summary>
    /// `Heal(MaxHp - CurrentHp)` runs AFTER `RelicCmd.Obtain`, so a relic that raises the
    /// cap is healed up to the NEW one. Royal Poison does not, but the ordering is the
    /// event's and a heal-then-obtain would be a different event.
    /// </summary>
    [Fact]
    public void TheHealIsToWhateverTheCapIsAfterTheRelicLands()
    {
        var engine = At();
        engine.State.PlayerHp = 1;
        int max = engine.State.PlayerMaxHp;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(engine.State.PlayerMaxHp, engine.State.PlayerHp);
        Assert.True(engine.State.PlayerHp >= max);
    }

    /// <summary>Picking the fight only turns the page -- nothing is charged yet.</summary>
    [Fact]
    public void PickingTheFightCostsNothingUntilTheSecondPage()
    {
        var engine = At();
        int hp = engine.State.PlayerHp;
        int relics = engine.State.Relics.Count;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(1, engine.State.EventPage);
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(relics, engine.State.Relics.Count);
    }

    /// <summary>The warning page carries ONE option, not two.</summary>
    [Fact]
    public void TheWarningPageOffersOnlyContinue()
    {
        var engine = At();
        engine.Step(1, -1, out _, out _, out _);

        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        Assert.Equal(1, mask[0]);
        Assert.Equal(0, mask[1]);
        Assert.Equal(0, mask[2]);
    }

    [Fact]
    public void ContinuingChargesElevenAndPaysAPoolRelic()
    {
        var engine = At();
        int hp = engine.State.PlayerHp;
        int relics = engine.State.Relics.Count;

        engine.Step(1, -1, out _, out _, out _);
        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(hp - 11, engine.State.PlayerHp);
        Assert.Equal(relics + 1, engine.State.Relics.Count);
        Assert.DoesNotContain(
            engine.State.Relics,
            relic => relic.DefId == RunNonCombatEffects.NamedRelic("RoyalPoison")
        );
    }

    /// <summary>
    /// Unblockable and Unpowered: no block and no Vulnerable touch it. At 11 it could end
    /// a run walking in under 12, which is what the event's own `CurrentHp >= 12` gate is
    /// for.
    /// </summary>
    [Theory]
    [InlineData(12, true)]
    [InlineData(11, false)]
    public void ItOnlyTurnsUpForARunThatCanSurviveTheFight(int hp, bool allowed)
    {
        var engine = At();
        engine.State.PlayerHp = hp;

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForRun(engine.State, RunConstants.EventRoundTeaParty)
        );
    }

    /// <summary>
    /// `PullNextRelicFromFront` is the pool queue, the same one a combat reward reads --
    /// so the fight's payout is a real run relic, not one of the event's named two.
    /// </summary>
    [Fact]
    public void TheFightPaysTheSameQueueACombatRewardWouldHaveDrawn()
    {
        var expected = At();
        int fromQueue = RunRewardGenerator.NextRelic(expected.State);

        var engine = At();
        engine.Step(1, -1, out _, out _, out _);
        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(fromQueue, engine.State.Relics.Last().DefId);
    }
}
