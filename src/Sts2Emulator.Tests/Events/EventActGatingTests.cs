using System.Reflection;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Nine events refuse to appear in Act 1, and the emulator models Act 1 and nothing else
/// -- a run ends at its boss. Rolling one of them puts a room in front of an agent that
/// the game would never have shown it, on a floor where the real game has something else.
///
/// Eight were not gated at all. The ninth, the Crystal Sphere, was gated on
/// <c>state.Act &gt; ActOvergrowth</c> -- but Act is WHICH of the two Act-1 acts the run
/// drew (Overgrowth 1, Underdocks 2), not an act index, so that test was true for every
/// Underdocks run and let the sphere into half of Act 1. A gate that reads the right
/// field the wrong way is worse than no gate: it looks handled.
///
/// The rule these all come from is <c>IsAllowed</c> reading <c>CurrentActIndex</c>, which
/// is 0 in Act 1:
///
/// <list type="bullet">
/// <item>Crystal Sphere, Potion Courier, Symbiote: <c>CurrentActIndex &gt; 0</c></item>
/// <item>Doll Room, Welcome to Wongos, Stone of All Time: <c>CurrentActIndex == 1</c></item>
/// <item>Ranwid the Elder, Relic Trader: <c>if (CurrentActIndex == 0) return false</c></item>
/// <item>Fake Merchant: <c>if (CurrentActIndex &lt; 1) return false</c></item>
/// </list>
/// </summary>
public class EventActGatingTests
{
    /// <summary>
    /// The events whose own IsAllowed refuses act index 0, transcribed from the
    /// decompiled source rather than from the emulator's own list -- so this is a check
    /// and not a restatement.
    /// </summary>
    private static readonly string[] NeverInActOne =
    [
        "CrystalSphere",
        "DollRoom",
        "FakeMerchant",
        "PotionCourier",
        "RanwidTheElder",
        "RelicTrader",
        "StoneOfAllTime",
        "Symbiote",
        "WelcomeToWongos",
    ];

    /// <summary>
    /// Events from the same shared pool whose act test DOES admit Act 1, so the gate is
    /// not simply excluding everything shared.
    /// </summary>
    private static readonly string[] AllowedInActOne =
    [
        "BrainLeech",
        "RoomFullOfCheese",
        "TeaMaster",
        "TheLegendsWereTrue",
    ];

    private static int EventId(string name) =>
        (int)
            typeof(RunConstants)
                .GetField($"Event{name}", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;

    /// <summary>
    /// A run generous enough that nothing else could be the reason an event is refused:
    /// gold, potions, relics and a full deck.
    /// </summary>
    private static RunState WellStocked(string seed)
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Gold = 999;
        engine.State.PotionSlots[0] = RunNonCombatEffects.NamedPotion("FoulPotion");
        engine.State.PotionSlots[1] = RunNonCombatEffects.NamedPotion("Ashwater");
        foreach (string relic in new[] { "Akabeko", "Anchor", "BagOfMarbles", "Bellows", "BeltBuckle" })
        {
            RunNonCombatEffects.ApplyRelicPickup(engine.State, RunNonCombatEffects.NamedRelic(relic));
        }

        return engine.State;
    }

    [Fact]
    public void NoActTwoEventIsAllowedInActOne()
    {
        var state = WellStocked("ABCDEF");

        foreach (string name in NeverInActOne)
        {
            Assert.False(
                RunNonCombatEffects.IsEventAllowedForTests(state, EventId(name)),
                $"{name} refuses Act 1 in the game and must not be offered here"
            );
        }
    }

    /// <summary>
    /// Both Act-1 acts, because the Crystal Sphere's broken gate only misfired on one of
    /// them -- an Overgrowth-only check would have called it fixed.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.ActOvergrowth)]
    [InlineData(RunConstants.ActUnderdocks)]
    public void TheGateHoldsInBothActOneActs(int act)
    {
        var state = WellStocked("ABCDEF");
        state.Act = act;

        foreach (string name in NeverInActOne)
        {
            Assert.False(
                RunNonCombatEffects.IsEventAllowedForTests(state, EventId(name)),
                $"{name} slipped into act {act}"
            );
        }
    }

    [Fact]
    public void TheGateDoesNotExcludeTheSharedEventsThatDoBelongHere()
    {
        var state = WellStocked("ABCDEF");

        foreach (string name in AllowedInActOne)
        {
            Assert.True(
                RunNonCombatEffects.IsEventAllowedForTests(state, EventId(name)),
                $"{name} is allowed in Act 1 and must still be offered"
            );
        }
    }

    /// <summary>
    /// The sequence a run actually walks must not contain one either -- the gate is only
    /// worth anything at the point an event is chosen.
    /// </summary>
    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("AAB")]
    [InlineData("UNS55LCMKP")]
    [InlineData("HEADLESS1")]
    public void NoRunEverEntersAnActTwoEvent(string seed)
    {
        var banned = NeverInActOne.Select(EventId).ToHashSet();
        var state = WellStocked(seed);

        for (int i = 0; i < 40; i++)
        {
            RunNonCombatEffects.EnterEvent(state);
            Assert.DoesNotContain(state.EventId, banned);
        }
    }
}
