using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The events that answer a choice with another choice.
///
/// A live capture stops at the first page, so everything past it is invisible to
/// <c>EventOutcomeTests</c> -- and for these events the first page is the part that
/// matters least. Immersing in the Abyssal Baths is not a decision; the decision is how
/// many times to Linger while the price climbs. Paying the Doll Room fifteen HP buys a
/// CHOICE of doll, and the emulator took the HP and ended the event, charging the player
/// for something they never got.
///
/// So the whole shape was missing rather than wrong, which is the failure mode a capture
/// is least able to report: every fixture passed while half of each event did not exist.
/// </summary>
[CoversEvent("AbyssalBaths")]
[CoversEvent("DollRoom")]
[CoversEvent("SlipperyBridge")]
[CoversEvent("WarHistorianRepy")]
public class MultiPageEventTests
{
    private static RunEngine At(int eventId, string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    private static bool StillInTheEvent(RunState state) =>
        state.Phase == RunPhase.Event && state.EventId != RunConstants.EventResultPending;

    // ── The Abyssal Baths ────────────────────────────────────────────────────

    /// <summary>
    /// OnImmerse gains 2 Max HP, takes the damage, and then raises that damage by one --
    /// so the sequence is 3, 4, 5. The Max HP lands first and carries current HP up with
    /// it, which is why the first dip is a net loss of only one.
    /// </summary>
    [Fact]
    public void ImmersingCostsOneMoreEveryTime()
    {
        var engine = At(RunConstants.EventAbyssalBaths);
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(82, engine.State.PlayerMaxHp);
        Assert.Equal(63, engine.State.PlayerHp); // 64 +2 -3

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(84, engine.State.PlayerMaxHp);
        Assert.Equal(61, engine.State.PlayerHp); // 63 +2 -4

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        Assert.Equal(86, engine.State.PlayerMaxHp);
        Assert.Equal(58, engine.State.PlayerHp); // 61 +2 -5
    }

    [Fact]
    public void TheBathsKeepOfferingAnotherDip()
    {
        var engine = At(RunConstants.EventAbyssalBaths);

        for (int i = 0; i < 4; i++)
        {
            Assert.Contains(0, Offered(engine));
            Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
            Assert.True(StillInTheEvent(engine.State));
        }
    }

    /// <summary>
    /// The second page's options are Linger and Exit -- Abstain's heal belongs to the
    /// first page only, so climbing out must not hand over 10 HP.
    /// </summary>
    [Fact]
    public void ClimbingOutOfTheBathsHealsNothing()
    {
        var engine = At(RunConstants.EventAbyssalBaths);
        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
        int hp = engine.State.PlayerHp;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.False(StillInTheEvent(engine.State));
    }

    [Fact]
    public void AbstainingOnTheFirstPageStillHealsTen()
    {
        var engine = At(RunConstants.EventAbyssalBaths);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(74, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);
    }

    /// <summary>Lingering can kill, and the run ends when it does.</summary>
    [Fact]
    public void LingeringTooLongEndsTheRun()
    {
        var engine = At(RunConstants.EventAbyssalBaths);
        engine.State.PlayerHp = 4;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _)); // 4 +2 -3 = 3
        Assert.Equal(3, engine.State.PlayerHp);

        Assert.Equal(0, engine.Step(0, -1, out _, out bool terminal, out _)); // 3 +2 -4 = 1
        Assert.Equal(1, engine.State.PlayerHp);
        Assert.False(terminal);

        Assert.Equal(0, engine.Step(0, -1, out _, out terminal, out _)); // 1 +2 -5 = 0
        Assert.Equal(0, engine.State.PlayerHp);
        Assert.True(terminal);
        Assert.Equal(RunPhase.Complete, engine.State.Phase);
    }

    // ── The Doll Room ────────────────────────────────────────────────────────

    /// <summary>
    /// Picking at random hands a doll over on the spot; the two paid options buy a choice
    /// on a second page and hand over nothing yet.
    /// </summary>
    [Fact]
    public void PickingAtRandomTakesADollImmediately()
    {
        var engine = At(RunConstants.EventDollRoom);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(2, engine.State.Relics.Count);
        Assert.False(StillInTheEvent(engine.State));
    }

    [Theory]
    [InlineData(1, 5, 2)]
    [InlineData(2, 15, 3)]
    public void PayingBuysAChoiceOfDollsAndNotADoll(int option, int hpCost, int dolls)
    {
        var engine = At(RunConstants.EventDollRoom);

        Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));

        Assert.Equal(64 - hpCost, engine.State.PlayerHp);
        Assert.Single(engine.State.Relics); // nothing yet -- the choice is the purchase
        Assert.Equal(Enumerable.Range(0, dolls), Offered(engine));

        Assert.Equal(0, engine.Step(dolls - 1, -1, out _, out _, out _));
        Assert.Equal(2, engine.State.Relics.Count);
        Assert.False(StillInTheEvent(engine.State));
    }

    /// <summary>
    /// Whatever the player picks, it is one of the three dolls -- never a relic rolled
    /// from the reward pool, which is what the emulator used to hand over.
    /// </summary>
    [Fact]
    public void EveryDollOnOfferIsOneOfTheThree()
    {
        var dolls = new[] { "DaughterOfTheWind", "MrStruggles", "BingBong" }
            .Select(RunNonCombatEffects.NamedRelic)
            .ToHashSet();

        foreach (string seed in new[] { "ABCDEF", "AAB", "UNS55LCMKP", "HEADLESS1" })
        {
            foreach (int option in new[] { 0, 1, 2 })
            {
                var engine = At(RunConstants.EventDollRoom, seed);
                Assert.Equal(0, engine.Step(option, -1, out _, out _, out _));
                if (StillInTheEvent(engine.State))
                {
                    Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));
                }

                Assert.Contains(engine.State.Relics[^1].DefId, dolls);
            }
        }
    }

    /// <summary>Examining shows all three; waiting a little shows two of them.</summary>
    [Fact]
    public void ExaminingShowsEveryDollAndWaitingShowsTwo()
    {
        var two = RunNonCombatEffects.DollRoomOffer(At(RunConstants.EventDollRoom).State, 2);
        var three = RunNonCombatEffects.DollRoomOffer(At(RunConstants.EventDollRoom).State, 3);

        Assert.Equal(2, two.Count);
        Assert.Equal(3, three.Count);
        Assert.Equal(3, three.Distinct().Count());
        Assert.Equal(two, three.Take(2));
    }

    // ── The Slippery Bridge ──────────────────────────────────────────────────

    /// <summary>
    /// CurrentHpLoss is 3 + NumberOfHoldOns, charged before the counter moves: 3, then 4,
    /// then 5. A flat 3 makes holding on free to repeat, which is the whole tension.
    /// </summary>
    [Fact]
    public void HoldingOnCostsOneMoreEachTime()
    {
        var engine = At(RunConstants.EventSlipperyBridge);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(61, engine.State.PlayerHp); // -3

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(57, engine.State.PlayerHp); // -4

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(52, engine.State.PlayerHp); // -5
    }

    /// <summary>
    /// Each hold re-rolls which card the bridge is threatening -- that is what the HP
    /// buys. A bridge that keeps naming the same card is one the player would never pay.
    /// </summary>
    [Fact]
    public void HoldingOnRerollsTheThreatenedCard()
    {
        var engine = At(RunConstants.EventSlipperyBridge);
        var named = new List<int>();

        for (int i = 0; i < 5; i++)
        {
            int index = RunNonCombatEffects.SlipperyBridgeCardIndex(engine.State);
            named.Add(engine.State.Deck[index].DefId);
            Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        }

        Assert.True(named.Distinct().Count() > 1, "the bridge named the same card every time");
    }

    [Fact]
    public void OvercomingLosesTheCardTheBridgeNamed()
    {
        var engine = At(RunConstants.EventSlipperyBridge);
        int index = RunNonCombatEffects.SlipperyBridgeCardIndex(engine.State);
        int named = engine.State.Deck[index].DefId;
        int copies = engine.State.Deck.Count(card => card.DefId == named);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(copies - 1, engine.State.Deck.Count(card => card.DefId == named));
    }

    // ── War Historian Repy ───────────────────────────────────────────────────

    private static RunEngine AtRepy(int keys)
    {
        var engine = At(RunConstants.EventWarHistorianRepy);
        for (int i = 0; i < keys; i++)
        {
            engine.State.Deck.Add(
                new CardInstance(RunNonCombatEffects.LanternKeyCard, Upgraded: false)
            );
        }

        return engine;
    }

    [Fact]
    public void OpeningADoorSpendsALanternKey()
    {
        var engine = AtRepy(keys: 1);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.DoesNotContain(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.LanternKeyCard
        );
    }

    /// <summary>
    /// A run holding two keys opens both doors: the first choice spends one, and Repy
    /// answers with the other door rather than finishing.
    /// </summary>
    [Fact]
    public void TwoKeysOpenBothDoors()
    {
        var engine = AtRepy(keys: 2);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.True(StillInTheEvent(engine.State), "Repy should offer the second door");
        Assert.Equal(new[] { 1 }, Offered(engine));
        Assert.Single(
            engine.State.Deck.Where(card => card.DefId == RunNonCombatEffects.LanternKeyCard)
        );

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));
        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
    }

    [Fact]
    public void OneKeyOpensOneDoorAndTheEventEnds()
    {
        var engine = AtRepy(keys: 1);

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.False(StillInTheEvent(engine.State));
    }
}
